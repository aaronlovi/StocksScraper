using System;
using System.Threading;

namespace Utilities;

internal sealed class SemaphoreGuard(SemaphoreSlim _semaphore) : IDisposable
{
    public void Dispose() => _semaphore.Release();
}
