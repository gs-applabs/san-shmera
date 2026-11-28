using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronikologio.Core;

/// <summary>A newer release found on GitHub: its version, the page, the installer and the file with its SHA-256.</summary>
public sealed record UpdateInfo(string Tag, Version Version, string PageUrl, string InstallerUrl, string HashUrl, string Notes, long Size);

/// <summary>What a check found.</summary>
public enum UpdateOutcome { UpToDate, Available, Skipped, TooSoon, NoNetwork, BadAnswer, NoInstaller }

public sealed record UpdateCheckResult(UpdateOutcome Outcome, UpdateInfo? Update = null, string? Detail = null);

/// <summary>What is remembered between runs: when the last check was, the ETag of the answer, the version the user skipped.</summary>
public sealed class UpdateState
{
    public DateTime LastCheckUtc { get; set; }
    public string ETag { get; set; } = "";
    public string CachedJson { get; set; } = "";
    public string SkippedTag { get; set; } = "";

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronikologio", "update-state.json");

    private const int MaxStateBytes = 16 * 1024 * 1024;

    public static UpdateState Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return new UpdateState();
            using var stream = File.OpenRead(path);
            if (stream.Length > MaxStateBytes) return new UpdateState();
            var state = JsonSerializer.Deserialize<UpdateState>(stream) ?? new UpdateState();
            state.ETag ??= ""; state.CachedJson ??= ""; state.SkippedTag ??= "";
            if (state.CachedJson.Length > 2 * 1024 * 1024 || state.ETag.Length > 1024 || state.SkippedTag.Length > 128)
                return new UpdateState();
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new UpdateState(); }
    }

    public void Save(string? path = null)
    {
        string? temp = null;
        try
        {
            path = Path.GetFullPath(path ?? DefaultPath);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(this);
            if (bytes.Length > MaxStateBytes) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        finally { if (temp is not null) try { File.Delete(temp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }
}

/// <summary>
/// Looks for a newer version in the releases of the official repository (GitHub), never blocking: it is async, has a timeout and a size cap, and asks at most once
/// every 24 hours (unless the user asks by hand). A drafts and pre-releases are ignored; the answer is cached with its ETag (HTTP 304 costs nothing).
/// </summary>
public sealed class UpdateChecker
{
    public const string Repository = "gs-applabs/san-shmera";
    /// <summary>The name the account had before: GitHub redirects it, and releases published then keep this address.</summary>
    public const string LegacyRepository = "giorstergiopoulos-design/san-shmera";
    public const string InstallerName = "ChronikologioSetup.exe";
    public static readonly TimeSpan MinInterval = TimeSpan.FromHours(24);
    private const int MaxJsonBytes = 2 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly string? _statePath;
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <param name="http">The client (the tests give one with a fake handler); null makes the real one.</param>
    /// <param name="statePath">Where the state is kept (null = in %AppData%).</param>
    public UpdateChecker(HttpClient? http = null, string? statePath = null)
    {
        _http = http ?? SharedHttp;
        _statePath = statePath;
    }

    public static Version Current => typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Major.minor.build: the tag "v0.8.0" and the assembly version 0.8.0.0 are the same version.</summary>
    public static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public static string ApiUrl => $"https://api.github.com/repos/{Repository}/releases/latest";

    /// <summary>Only the files of the releases of our repository are accepted for download.</summary>
    public static bool IsTrustedDownloadUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com"
        && (u.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal)
            || u.AbsolutePath.StartsWith($"/{LegacyRepository}/releases/download/", StringComparison.Ordinal));

    internal static bool IsTrustedAssetUrl(string url, string tag, string asset)
    {
        if (!IsTrustedDownloadUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u) ||
            !u.IsDefaultPort || u.UserInfo.Length != 0 || u.Query.Length != 0 || u.Fragment.Length != 0 ||
            !TryParseTag(tag, out _)) return false;
        var suffix = "/releases/download/" + Uri.EscapeDataString(tag) + "/" + asset;
        return u.AbsolutePath == "/" + Repository + suffix || u.AbsolutePath == "/" + LegacyRepository + suffix;
    }

    /// <summary>After the redirects the file must come from GitHub or its CDN.</summary>
    public static bool IsTrustedRedirectTarget(Uri? u) =>
        u is { Scheme: "https", IsDefaultPort: true, UserInfo.Length: 0, Fragment.Length: 0 } &&
        u.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com" or "github-releases.githubusercontent.com";

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var t = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(t, out var v) && (version = v) is not null;
    }

    /// <summary>Asks GitHub (when it is time) and says what was found. <paramref name="manual"/> = the user asked: no waiting time and a skipped version is shown again.</summary>
    public async Task<UpdateCheckResult> CheckAsync(Version current, bool manual = false, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var state = UpdateState.Load(_statePath);
        if (!manual && state.LastCheckUtc != default && now - state.LastCheckUtc < MinInterval) return new UpdateCheckResult(UpdateOutcome.TooSoon);

        string json;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("Chronikologio-UpdateCheck");
            if (state.ETag.Length > 0 && state.CachedJson.Length > 0) request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && state.CachedJson.Length > 0) json = state.CachedJson;
            else if (!response.IsSuccessStatusCode) return new UpdateCheckResult(UpdateOutcome.NoNetwork, null, $"HTTP {(int)response.StatusCode}");
            else
            {
                if (response.Content.Headers.ContentLength is > MaxJsonBytes) return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, "too large");
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                await using var body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                using var data = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = await body.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
                {
                    if (data.Length + read > MaxJsonBytes) return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, "too large");
                    data.Write(buffer, 0, read);
                }
                json = Encoding.UTF8.GetString(data.ToArray());
                if (json.Length > MaxJsonBytes) return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, "too large");
                state.ETag = response.Headers.ETag?.ToString() ?? "";
                state.CachedJson = json;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            return new UpdateCheckResult(UpdateOutcome.NoNetwork, null, ex.Message);   // offline: nothing is remembered, the next try may come soon
        }

        state.LastCheckUtc = now;
        var parsed = Parse(json, current);
        if (parsed.Outcome == UpdateOutcome.Available && !manual && parsed.Update!.Tag == state.SkippedTag)
            parsed = new UpdateCheckResult(UpdateOutcome.Skipped, parsed.Update);
        state.Save(_statePath);
        return parsed;
    }

    /// <summary>Reads the answer of GitHub: the newest published release, its installer and the file with the hash.</summary>
    public static UpdateCheckResult Parse(string json, Version current)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, "not an object");
            if ((root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) || (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True))
                return new UpdateCheckResult(UpdateOutcome.UpToDate);
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (!TryParseTag(tag, out var version)) return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, "bad tag");
            if (Norm(version) <= Norm(current)) return new UpdateCheckResult(UpdateOutcome.UpToDate);
            string installer = "", hash = ""; long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (name == InstallerName && url is not null) { installer = url; size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0; }
                    else if (name == InstallerName + ".sha256" && url is not null) hash = url;
                }
            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            if (!IsTrustedAssetUrl(installer, tag!, InstallerName) || !IsTrustedAssetUrl(hash, tag!, InstallerName + ".sha256")) return new UpdateCheckResult(UpdateOutcome.NoInstaller, null, "no trusted installer and hash file");
            return new UpdateCheckResult(UpdateOutcome.Available, new UpdateInfo(tag!, version, page, installer, hash, notes, size));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return new UpdateCheckResult(UpdateOutcome.BadAnswer, null, ex.Message); }
    }

    /// <summary>The user chose "skip this version": it is not offered again (a manual check shows it anyway).</summary>
    public void Skip(string tag)
    {
        var state = UpdateState.Load(_statePath);
        state.SkippedTag = tag;
        state.Save(_statePath);
    }
}

/// <summary>Downloads the installer of an update, proves it with the published SHA-256 and starts it. Nothing runs when the hash does not match.</summary>
public sealed class UpdateInstaller
{
    public const long MaxInstallerBytes = 700L * 1024 * 1024;   // the installer with the photographs is already close to 400 MB
    private readonly HttpClient _http;

    private static readonly HttpClient SharedHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(20) };
    public UpdateInstaller(HttpClient? http = null) => _http = http ?? SharedHttp;

    public static string UpdateFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chronikologio", "update");

    public static string LogPath => Path.Combine(UpdateFolder, "last-update.log");

    public static void Log(string text, string? path = null)
    {
        try
        {
            path ??= LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The hash in a ".sha256" file: "hash *name" or just the hash (64 hex digits), whatever else follows it.</summary>
    public static string? ParseHash(string text)
    {
        var first = text.Trim().Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is { Length: 64 } && first.All(Uri.IsHexDigit) ? first.ToLowerInvariant() : null;
    }

    /// <summary>Downloads installer and hash to <paramref name="folder"/>; the installer is kept only if its SHA-256 is the published one. Throws <see cref="UpdateException"/> otherwise.</summary>
    public sealed record VerifiedDownload(string Path, string Sha256);

    public async Task<string> DownloadAsync(UpdateInfo info, string folder, IProgress<int>? progress = null, CancellationToken ct = default)
        => (await DownloadVerifiedAsync(info, folder, progress, ct).ConfigureAwait(false)).Path;

    private async Task<HttpResponseMessage> GetAssetAsync(string url, string tag, string asset, CancellationToken ct)
    {
        var current = new Uri(url);
        for (int i = 0; i < 6; i++)
        {
            if (!UpdateChecker.IsTrustedRedirectTarget(current) ||
                (current.Host == "github.com" && !UpdateChecker.IsTrustedAssetUrl(current.AbsoluteUri, tag, asset)))
                throw new UpdateException(UpdateFailure.Untrusted);
            var response = await _http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new UpdateException(UpdateFailure.Untrusted);
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }
            if (!UpdateChecker.IsTrustedRedirectTarget(response.RequestMessage?.RequestUri))
            {
                response.Dispose();
                throw new UpdateException(UpdateFailure.Untrusted);
            }
            return response;
        }
        throw new UpdateException(UpdateFailure.Untrusted, "Too many release redirects.");
    }

    public async Task<VerifiedDownload> DownloadVerifiedAsync(UpdateInfo info, string folder, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (!UpdateChecker.IsTrustedAssetUrl(info.InstallerUrl, info.Tag, UpdateChecker.InstallerName) || !UpdateChecker.IsTrustedAssetUrl(info.HashUrl, info.Tag, UpdateChecker.InstallerName + ".sha256")) throw new UpdateException(UpdateFailure.Untrusted);
        Directory.CreateDirectory(folder);
        PruneOldDownloads(folder, DateTime.UtcNow);
        // Each download owns its files. Never delete another process's installer or partial download.

        string? expected;
        try
        {
            using var hashResponse = await GetAssetAsync(info.HashUrl, info.Tag, UpdateChecker.InstallerName + ".sha256", ct).ConfigureAwait(false);
            if (!hashResponse.IsSuccessStatusCode || !UpdateChecker.IsTrustedRedirectTarget(hashResponse.RequestMessage?.RequestUri)) throw new UpdateException(UpdateFailure.Network);
            var buffer = new byte[4096];
            await using var hs = await hashResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var n = 0;
            while (n < buffer.Length)
            {
                var read = await hs.ReadAsync(buffer.AsMemory(n), deadline.Token).ConfigureAwait(false);
                if (read == 0) break;
                n += read;
            }
            if (n == buffer.Length) throw new UpdateException(UpdateFailure.NoHash);
            expected = ParseHash(Encoding.UTF8.GetString(buffer, 0, n));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { throw new UpdateException(UpdateFailure.Network, ex.Message); }
        if (expected is null) throw new UpdateException(UpdateFailure.NoHash);

        var downloadDir = Path.Combine(folder, "download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(downloadDir);
        var part = Path.Combine(downloadDir, InstallerNamePart);
        var final = Path.Combine(downloadDir, UpdateChecker.InstallerName);
        try
        {
            using var response = await GetAssetAsync(info.InstallerUrl, info.Tag, UpdateChecker.InstallerName, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new UpdateException(UpdateFailure.Network, $"HTTP {(int)response.StatusCode}");
            if (!UpdateChecker.IsTrustedRedirectTarget(response.RequestMessage?.RequestUri)) throw new UpdateException(UpdateFailure.Untrusted);
            var total = response.Content.Headers.ContentLength ?? info.Size;
            if (total > MaxInstallerBytes) throw new UpdateException(UpdateFailure.TooLarge);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromMinutes(20));
            using var sha = SHA256.Create();
            await using (var src = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[81920]; long done = 0; int read; int lastProgress = -1;
                while ((read = await src.ReadAsync(buf, deadline.Token).ConfigureAwait(false)) > 0)
                {
                    done += read;
                    if (done > MaxInstallerBytes) throw new UpdateException(UpdateFailure.TooLarge);
                    sha.TransformBlock(buf, 0, read, null, 0);
                    await dst.WriteAsync(buf.AsMemory(0, read), deadline.Token).ConfigureAwait(false);
                    if (total > 0)
                    {
                        var percent = (int)Math.Clamp(done * 100 / total, 0, 100);
                        if (percent > lastProgress) { lastProgress = percent; progress?.Report(percent); }
                    }
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }
            var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                File.Delete(part);
                throw new UpdateException(UpdateFailure.HashMismatch, $"expected {expected}, got {actual}");
            }
            File.Move(part, final, false);
            return new VerifiedDownload(final, expected);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { }
            throw new UpdateException(UpdateFailure.Network, ex.Message);
        }
        finally
        {
            try { File.Delete(part); if (!File.Exists(final)) Directory.Delete(downloadDir, recursive: false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // Only prune exact names in old download GUID folders, never user files, links or active installers.
    internal static int PruneOldDownloads(string folder, DateTime nowUtc)
    {
        var removed = 0;
        try
        {
            var root = new DirectoryInfo(Path.GetFullPath(folder));
            for (DirectoryInfo? ancestor = root; ancestor != null; ancestor = ancestor.Parent)
                if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) return 0;
            foreach (var dir in root.EnumerateDirectories("download-*").Take(32))
            {
                try
                {
                    if (!Guid.TryParseExact(dir.Name["download-".Length..], "N", out _) ||
                        (dir.Attributes & FileAttributes.ReparsePoint) != 0 || dir.LastWriteTimeUtc >= nowUtc.AddDays(-30)) continue;
                    var entries = dir.EnumerateFileSystemInfos().Take(2).ToArray();
                    if (entries.Length != 1 || entries[0] is not FileInfo file ||
                        file.Name != UpdateChecker.InstallerName || (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                        file.LastWriteTimeUtc >= nowUtc.AddDays(-30)) continue;
                    using (var probe = file.Open(FileMode.Open, FileAccess.Read, FileShare.None)) { }
                    file.Delete();
                    dir.Delete(false);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        return removed;
    }

    public static bool LaunchVerified(VerifiedDownload download, Func<string, SignatureState>? signatureCheck = null)
    {
        using var stream = new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > MaxInstallerBytes ||
            !Convert.ToHexString(SHA256.HashData(stream)).Equals(download.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            Log("refused: installer changed after download");
            return false;
        }
        return Launch(download.Path, signatureCheck);
    }

    private const string InstallerNamePart = "ChronikologioSetup.exe.part";

    /// <summary>Starts the installer by the shell (it asks for administrator rights by itself when it needs them). No PowerShell, no hidden helper. False when the signature is invalid.</summary>
    public static bool Launch(string installerPath, Func<string, SignatureState>? signatureCheck = null)
    {
        var state = (signatureCheck ?? Authenticode.Check)(installerPath);
        if (state == SignatureState.Invalid) { Log($"refused: the signature of {installerPath} is not valid"); return false; }
        Log($"starting {installerPath} (signature: {state})");
        Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true, Arguments = "/CLOSEAPPLICATIONS" });
        return true;
    }
}

public enum UpdateFailure { Network, HashMismatch, NoHash, Untrusted, TooLarge }

public sealed class UpdateException : Exception
{
    public UpdateFailure Failure { get; }
    public UpdateException(UpdateFailure failure, string? detail = null) : base(detail ?? failure.ToString()) => Failure = failure;
}

public enum SignatureState { Unsigned, Valid, Invalid }

/// <summary>Asks Windows (WinVerifyTrust) whether a file carries a valid digital signature. An unsigned file is not refused (the SHA-256 is the proof); a signature that does not verify is.</summary>
public static class Authenticode
{
    public static SignatureState Check(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path)) return SignatureState.Unsigned;
        var result = Verify(path);
        return result switch { 0 => SignatureState.Valid, 0x800B0100 => SignatureState.Unsigned, _ => SignatureState.Invalid };   // TRUST_E_NOSIGNATURE
    }

    [System.Runtime.InteropServices.DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid action, ref WinTrustData data);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct WinTrustFileInfo { public uint Size; public string FilePath; public IntPtr File; public IntPtr KnownSubject; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size; public IntPtr PolicyCallback; public IntPtr SipClientData; public uint UIChoice; public uint RevocationChecks; public uint UnionChoice;
        public IntPtr FileInfo; public uint StateAction; public IntPtr StateData; public string? UrlReference; public uint ProvFlags; public uint UIContext; public IntPtr SignatureSettings;
    }

    private static uint Verify(string path)
    {
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");   // WINTRUST_ACTION_GENERIC_VERIFY_V2
        var fileInfo = new WinTrustFileInfo { FilePath = path };
        fileInfo.Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(fileInfo);
        var pFile = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(fileInfo));
        var initialized = false;
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(fileInfo, pFile, false);
            initialized = true;
            var data = new WinTrustData { UIChoice = 2 /* none */, RevocationChecks = 0, UnionChoice = 1 /* file */, FileInfo = pFile, StateAction = 0, ProvFlags = 0x10 /* no revocation UI */ };
            data.Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(data);
            return WinVerifyTrust(IntPtr.Zero, action, ref data);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return 0x800B0100; }
        finally { if (initialized) System.Runtime.InteropServices.Marshal.DestroyStructure<WinTrustFileInfo>(pFile); System.Runtime.InteropServices.Marshal.FreeHGlobal(pFile); }
    }
}
