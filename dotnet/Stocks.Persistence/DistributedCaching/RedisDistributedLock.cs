using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using StackExchange.Redis;

namespace Stocks.Persistence.DistributedCaching;

public sealed class RedisDistributedLock : IDistributedLock {
    private static readonly LuaScript _releaseScript = LuaScript.Prepare(@"
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end");

    private static readonly LuaScript _renewalScript = LuaScript.Prepare(@"
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('pexpire', KEYS[1], ARGV[2])
else
    return 0
");

    private bool _isDisposed;
    private readonly IDatabase _db;
    private readonly string _key;
    private readonly string _value;
    private readonly TimeSpan _lockExtensionTime;
    private readonly CancellationTokenSource _cts;
    private readonly Task? _renewalTask;
    private readonly bool _enableAutoRenewal;

    internal RedisDistributedLock(IDatabase db, string key, string value, bool acquired, TimeSpan lockExtensionTime, bool enableAutoRenewal = true) {
        _db = db;
        _key = key;
        _value = value;
        IsAcquired = acquired;
        _lockExtensionTime = lockExtensionTime;
        _enableAutoRenewal = enableAutoRenewal;
        _cts = new();

        if (acquired && enableAutoRenewal)
            _renewalTask = StartAutoRenewalTask();
    }

    public bool IsAcquired { get; init; }

    public async ValueTask DisposeAsync() {
        if (_isDisposed || !IsAcquired)
            return;

        try {
            // Stop the auto-renewal task
            _cts.Cancel();
            if (_renewalTask is not null)
                await _renewalTask;

            // Release the lock
            var keys = new RedisKey[] { _key };
            var args = new RedisValue[] { _value };
            _ = await _db.ScriptEvaluateAsync(_releaseScript.ExecutableScript, keys, args);
        } finally {
            _isDisposed = true;
            _cts.Dispose();
        }
    }

    private Task StartAutoRenewalTask() {
        var renewalDelay = TimeSpan.FromMilliseconds(_lockExtensionTime.TotalMilliseconds / 2);

        // Allow for at most two renewals (total lease time = 3 * _lockExtensionTime)
        var maxLeaseTime = TimeSpan.FromMilliseconds(_lockExtensionTime.TotalMilliseconds * 3);
        var stopWatch = Stopwatch.StartNew();

        return Task.Run(async () => {
            while (!_isDisposed && !_cts.Token.IsCancellationRequested) {
                try {
                    await Task.Delay(renewalDelay, _cts.Token);

                    if (_isDisposed || _cts.Token.IsCancellationRequested)
                        break;

                    // Stop renewing if we've exceeded the max lease time
                    if (stopWatch.Elapsed >= maxLeaseTime)
                        break;

                    // Extend TTL for the lock (only if we still own the lock)
                    var keys = new RedisKey[] { _key };
                    var args = new RedisValue[] { _value, (int)_lockExtensionTime.TotalMilliseconds };
                    int result = (int)await _db.ScriptEvaluateAsync(_renewalScript.ExecutableScript, keys, args);

                    if (result == 0) {
                        // Lost ownership of the lock, stop renewing
                        break;
                    }
                } catch (TaskCanceledException) {
                    // Task was canceled, exit gracefully
                    break;
                } catch (OperationCanceledException) {
                    // Operation was canceled, exit gracefully
                    break;
                } catch (Exception ex) {
                    // Log the exception (optional)
                    Log.Error(ex, "Error while renewing Redis distributed lock");
                }
            }
        });
    }
}
