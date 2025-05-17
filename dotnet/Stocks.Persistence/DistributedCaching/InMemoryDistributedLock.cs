using System.Threading.Tasks;

namespace Stocks.Persistence.DistributedCaching;

internal class InMemoryDistributedLock : IDistributedLock {
    private bool _isDisposed;
    private readonly string _lockKey;
    private readonly InMemoryDistributedLockService _lockService;

    public InMemoryDistributedLock(string lockKey, bool isAcquired, InMemoryDistributedLockService lockService) {
        _lockKey = lockKey;
        IsAcquired = isAcquired;
        _lockService = lockService;
    }

    public bool IsAcquired { get; init; }

    public async ValueTask DisposeAsync() {
        if (_isDisposed || !IsAcquired)
            return;

        await _lockService.ReleaseAsync(_lockKey);
        _isDisposed = true;
    }
}
