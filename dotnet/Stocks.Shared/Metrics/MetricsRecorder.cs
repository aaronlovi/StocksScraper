using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Stocks.Shared.Metrics;

/// <summary>
/// A utility class for recording metrics using histograms.
/// </summary>
public class MetricsRecorder : IDisposable {
    private bool _isDisposed;
    private readonly Meter _meter; // MetricsRecorder does not own the Meter instance. Do not dispose it.
    private readonly Stopwatch stopwatch;
    private readonly Histogram<double> _histogram;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsRecorder"/> class.
    /// </summary>
    /// <param name="meter">The <see cref="Meter"/> instance used to create the histogram.</param>
    /// <param name="name">The name of the histogram.</param>
    /// <param name="unit">The unit of measurement for the histogram (optional).</param>
    /// <param name="description">The descrition of the histogram (optional).</param>
    /// <param name="tags">Optional tags to attach to the histogram.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="meter"/> is null.</exception>
    public MetricsRecorder(Meter meter, string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) {
        ArgumentNullException.ThrowIfNull(meter);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("The metric name cannot be null or empty.", nameof(name));
        _meter = meter;
        _histogram = _meter.CreateHistogram<double>(name, unit, description, tags);
        stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Stops the timer and records the elapsed time in the histogram.
    /// </summary>
    public void Dispose() {
        if (_isDisposed)
            return;
        stopwatch.Stop();
        _histogram.Record(stopwatch.Elapsed.TotalMilliseconds);
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
