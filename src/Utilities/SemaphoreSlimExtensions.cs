using System;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities;

public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> AcquireAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new SemaphoreGuard(semaphore);
    }
}
