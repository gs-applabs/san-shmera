namespace Chronikologio.Core;

/// <summary>One worker and at most one waiting value. Repeated requests replace the pending
/// value rather than retaining a task chain and obsolete stores in memory.</summary>
public sealed class LatestWork<T> : IDisposable where T : class
{
    private readonly object _gate = new();
    private readonly Action<T> _work;
    private T? _pending;
    private Task? _worker;
    private bool _closed;

    public LatestWork(Action<T> work) => _work = work ?? throw new ArgumentNullException(nameof(work));

    public void Submit(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            if (_closed) return;
            _pending = value;
            _worker ??= Task.Run(Process);
        }
    }

    private void Process()
    {
        while (true)
        {
            T? value;
            lock (_gate)
            {
                value = _pending;
                _pending = null;
                if (value is null) { _worker = null; return; }
            }
            try { _work(value); }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                System.Diagnostics.Debug.WriteLine("Chronikologio background publication failed: " + ex.Message);
            }
        }
    }

    /// <summary>Reject new submissions and finish the current/latest operation.</summary>
    public void Dispose()
    {
        Task? worker;
        lock (_gate)
        {
            _closed = true;
            worker = _worker;
        }
        worker?.GetAwaiter().GetResult();
    }
}
