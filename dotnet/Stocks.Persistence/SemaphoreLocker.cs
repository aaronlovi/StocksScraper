using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stocks.Persistence;

public sealed class SemaphoreLocker : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _acquired;

    public SemaphoreLocker(SemaphoreSlim semaphore) => _semaphore = semaphore;

    public async Task Acquire(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        _acquired = true;
    }

    public void Dispose()
    {
        if (_acquired)
        {
            _ = _semaphore.Release();
            _acquired = false;
        }
    }
}
