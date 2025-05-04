using System;
using System.Threading;

namespace Stocks.Shared;

internal sealed class SemaphoreGuard(SemaphoreSlim _semaphore) : IDisposable
{
    public void Dispose() => _semaphore.Release();
}
