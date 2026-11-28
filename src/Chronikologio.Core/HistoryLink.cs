namespace Chronikologio.Core;

/// <summary>Portable internal reference, not a command, file path or registered OS protocol.
/// Only existing catalogue IDs are resolved by the caller.</summary>
public static class HistoryLink
{
    private const string Prefix="chronikologio://entry/";
    public static bool IsValidId(string? id)
    {
        if(string.IsNullOrEmpty(id)||id.Length>100)return false;
        var parts=id.Split(':');
        if(parts.Length!=4||parts[0] is not ("event" or "birth" or "death"))return false;
        var day=parts[1];
        if(day.Length!=5||day[2]!='-'||!int.TryParse(day[..2],out var month)||!int.TryParse(day[3..],out var number)||
            month<1||month>12||number<1||number>DateTime.DaysInMonth(2000,month))return false;
        if(!day.All(c=>c=='-'||c is >= '0' and <= '9'))return false;
        if(parts[2]!="x"&&(!int.TryParse(parts[2],System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out var year)||
            year==0||year.ToString(System.Globalization.CultureInfo.InvariantCulture)!=parts[2]))return false;
        return parts[3].Length==8&&parts[3].All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
    public static string Create(string id)
    {
        if(!IsValidId(id))throw new ArgumentException("Invalid catalogue identifier.",nameof(id));
        return Prefix+Uri.EscapeDataString(id);
    }
    public static bool TryParse(string? text,out string id)
    {
        id="";
        if(string.IsNullOrWhiteSpace(text)||text.Length>300)return false;
        text=text.Trim();
        if(IsValidId(text)){id=text;return true;}
        if(!text.StartsWith(Prefix,StringComparison.Ordinal))return false;
        var encoded=text[Prefix.Length..];
        if(encoded.Contains('/')||encoded.Contains('?')||encoded.Contains('#')||encoded.Contains('\\'))return false;
        var decoded=Uri.UnescapeDataString(encoded);
        if(!IsValidId(decoded)||!string.Equals(Uri.EscapeDataString(decoded),encoded,StringComparison.OrdinalIgnoreCase))return false;
        id=decoded;return true;
    }
    /// <summary>Only direct HTTPS evidence URLs, no credentials or non-default port.</summary>
    public static Uri? SourceUri(string? value)
        => Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps&&
            uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&!string.IsNullOrWhiteSpace(uri.Host)
            ? uri:null;
}
