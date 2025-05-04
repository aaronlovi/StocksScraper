using System.Collections.Generic;

namespace Stocks.Shared;

public static class DictionaryUtils {
    public static TValue GetOrCreateEntry<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        where TValue : new() {
        if (!dictionary.TryGetValue(key, out TValue? value))
            value = dictionary[key] = new TValue();

        return value;
    }
}
