using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stocks.Shared;

public static class Utilities {
    /// <summary>
    /// Safely performs division, returning a default value when the divisor is zero.
    /// </summary>
    /// <param name="numerator">The numerator in the division operation.</param>
    /// <param name="denominator">The denominator in the division operation. If this value is zero, the method returns the default value.</param>
    /// <param name="defaultVal">The default value to return when the denominator is zero.</param>
    /// <returns>The result of the division operation if the denominator is not zero; otherwise, the default value.</returns>
    public static decimal DivSafe(decimal numerator, decimal denominator, decimal defaultVal = decimal.MaxValue)
        => denominator != 0 ? numerator / denominator : defaultVal;

    /// <summary>
    /// Determines whether the specified string contains the specified search string.
    /// </summary>
    /// <param name="str">The string to search within. If this string is null, the method returns false.</param>
    /// <param name="searchString">The string to search for.</param>
    /// <returns>true if the search string is found in the string; otherwise, false.</returns>
    public static bool Includes(this string str, string searchString)
        => (str?.IndexOf(searchString, StringComparison.Ordinal) ?? -1) >= 0;

    /// <summary>
    /// Safely disposes of the provided object if it implements IDisposable.
    /// </summary>
    /// <param name="o">The object to dispose. If the object is null or does not implement IDisposable, the method does nothing.</param>
    public static void SafeDispose(object? o) {
        if (o is IDisposable d)
            d.Dispose();
    }

    /// <summary>
    /// Creates a CancellationTokenSource that is linked to the provided CancellationToken and CancellationTokenSource.
    /// </summary>
    /// <param name="cts">The CancellationTokenSource to link. If null, a new CancellationTokenSource is created that is only linked to the provided CancellationToken.</param>
    /// <param name="ct">The CancellationToken to link.</param>
    /// <returns>A CancellationTokenSource that is linked to the provided CancellationToken and, if provided, CancellationTokenSource.</returns>
    public static CancellationTokenSource CreateLinkedTokenSource(CancellationTokenSource? cts, CancellationToken ct) {
        return cts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(ct);
    }

    /// <summary>
    /// Calculates the time difference between two DateTime objects.
    /// </summary>
    /// <param name="startTime">The start time of the interval.</param>
    /// <param name="endTime">The end time of the interval. If null, the start time is used as the end time.</param>
    /// <returns>The time difference as a TimeSpan. If the calculated difference is less than or equal to zero, a TimeSpan representing one millisecond is returned.</returns>
    public static TimeSpan CalculateTimeDifference(DateTime startTime, DateTime? endTime) {
        TimeSpan difference = endTime.GetValueOrDefault(startTime) - startTime;
        return difference > TimeSpan.Zero ? difference : TimeSpan.FromMilliseconds(1);
    }

    /// <summary>
    /// Creates a task that completes when the provided CancellationToken is cancelled.
    /// </summary>
    public static Task CreateCancellationTask(CancellationToken ct) {
        var tcs = new TaskCompletionSource<bool>();
        _ = ct.Register(() => tcs.SetResult(true));
        return tcs.Task;
    }

    public static bool EqualsInvariant(this string str, string other)
        => str.Equals(other, StringComparison.InvariantCulture);

    public static bool EqualsOrdinalIgnoreCase(this string str, string other)
        => str.Equals(other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the given decimal value is an integer.
    /// </summary>
    /// <param name="value">The decimal value to check.</param>
    /// <returns>True if the value is an integer; otherwise, false.</returns>
    public static bool IsInteger(this decimal value) => value % 1 == 0;
}
