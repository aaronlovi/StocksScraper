using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Stocks.Shared;

public static class LogUtils
{
    public const string CikContext = "CIK";
    public const string CompanyIdContext = "CompanyId";

    private const int DefaultMaxItems = 5;

    /// <summary>
    /// Generates a string representation of a collection, with a limit on the number of items included.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="list">The collection of items to be logged.</param>
    /// <param name="maxItems">The maximum number of items to include in the log string. Defaults to 5.</param>
    /// <returns>A string representation of the collection, with up to <paramref name="maxItems"/> items included. 
    /// If the collection contains more than <paramref name="maxItems"/> items, the string will indicate the number of additional items.</returns>
    /// <remarks>
    /// If an item in the collection is null, "null" is included in the string representation for that item.
    /// </remarks>
    public static string GetLogStr<T>(IReadOnlyCollection<T> list, int maxItems = DefaultMaxItems)
    {
        if (maxItems < 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "The maximum number of items must be non-negative.");

        if (list is null || list.Count == 0)
            return "[]";

        var sb = new StringBuilder();
        sb.Append('[');

        int i = 0;
        foreach (var item in list)
        {
            if (i >= maxItems)
            {
                sb.Append($", and {list.Count - maxItems} more...");
                break;
            }
            if (i > 0)
                sb.Append(',');
            sb.Append(item?.ToString() ?? "null");
            ++i;
        }
        sb.Append(']');

        return sb.ToString();
    }
}
