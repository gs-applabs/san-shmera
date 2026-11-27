using System.Text.Json;

namespace Chronikologio.Windows;

/// <summary>Explicitly requested, local-only diagnostic. Counts native paint messages per
/// existing control without instrumenting normal application runs.</summary>
internal sealed class PaintAudit : IDisposable
{
    private readonly Form _form;
    private readonly string _output;
    private readonly List<Probe> _probes = new();
    private readonly System.Diagnostics.Stopwatch _elapsed = System.Diagnostics.Stopwatch.StartNew();
    private bool _disposed;

    private PaintAudit(Form form, string output)
    {
        _form = form;
        _output = output;
        _form.Shown += OnShown;
    }

    public static PaintAudit? AttachIfRequested(Form form)
    {
        var path = Environment.GetEnvironmentVariable("CHRON_PAINT_AUDIT");
        return string.IsNullOrWhiteSpace(path) ? null : new PaintAudit(form, Path.GetFullPath(path));
    }

    private void OnShown(object? sender, EventArgs e)
    {
        _form.Shown -= OnShown;
        Attach(_form, _form.GetType().Name);
    }

    private void Attach(Control control, string path)
    {
        if (_probes.Count >= 2000) return;
        _probes.Add(new Probe(control, path));
        var index = 0;
        foreach (Control child in control.Controls)
            Attach(child, path + "/" + child.GetType().Name + "[" + index++ + "]");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _form.Shown -= OnShown;
        var report = new
        {
            elapsedMs = _elapsed.ElapsedMilliseconds,
            animations = Chronikologio.UI.WaveClock.Enabled,
            controls = _probes.Select(p => new { p.Path, p.Paints, p.Erases, p.NonClientPaints, p.Invalidations, p.Layouts })
                .OrderByDescending(p => p.Paints).ToArray()
        };
        foreach (var probe in _probes) probe.Dispose();
        _probes.Clear();
        try { File.WriteAllText(_output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class Probe : NativeWindow, IDisposable
    {
        private readonly Control _control;
        public string Path { get; }
        public long Paints { get; private set; }
        public long Erases { get; private set; }
        public long NonClientPaints { get; private set; }
        public long Invalidations { get; private set; }
        public long Layouts { get; private set; }

        public Probe(Control control, string path)
        {
            _control = control;
            Path = path;
            control.HandleCreated += HandleCreated;
            control.HandleDestroyed += HandleDestroyed;
            control.Invalidated += Invalidated;
            control.Layout += Layout;
            if (control.IsHandleCreated) AssignHandle(control.Handle);
        }

        private void HandleCreated(object? sender, EventArgs e)
        {
            if (Handle == IntPtr.Zero) AssignHandle(_control.Handle);
        }
        private void HandleDestroyed(object? sender, EventArgs e)
        {
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }
        private void Invalidated(object? sender, InvalidateEventArgs e) => Invalidations++;
        private void Layout(object? sender, LayoutEventArgs e) => Layouts++;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x000F: Paints++; break; // WM_PAINT
                case 0x0014: Erases++; break; // WM_ERASEBKGND
                case 0x0085: NonClientPaints++; break; // WM_NCPAINT
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            _control.HandleCreated -= HandleCreated;
            _control.HandleDestroyed -= HandleDestroyed;
            _control.Invalidated -= Invalidated;
            _control.Layout -= Layout;
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }
    }
}
