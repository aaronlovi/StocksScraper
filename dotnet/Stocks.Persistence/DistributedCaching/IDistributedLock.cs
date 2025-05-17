using System;

namespace Stocks.Persistence.DistributedCaching;

public interface IDistributedLock : IAsyncDisposable {
    /// <summary>True if the lock was successflly obtained.</summary>
    bool IsAcquired { get; }
}
