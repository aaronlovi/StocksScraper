using System.Collections.Generic;

namespace Stocks.Shared;

public class NormalizedStringKeysHashMap<T> where T : struct {
    private readonly Dictionary<string, T> _dataMap;

    public NormalizedStringKeysHashMap() {
        _dataMap = [];
    }

    public T? this[string key] {
        get {
            bool res = _dataMap.TryGetValue(key.ToUpperInvariant(), out T val);
            return res ? val : null;
        }
        set {
            if (value is not null)
                _dataMap[key.ToUpperInvariant()] = value.Value;
            else
                _ = _dataMap.Remove(key.ToUpperInvariant());
        }
    }

    public bool HasValue(string key) => _dataMap.ContainsKey(key.ToUpperInvariant());

    public Dictionary<string, T>.KeyCollection Keys => _dataMap.Keys;
}
