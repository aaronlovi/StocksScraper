#pragma warning disable IDE0290 // Use primary constructor

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;

namespace Stocks.DataService.RawData;

internal abstract class RawDataQueryInputBase : IDisposable
{
    private bool _isDisposed;

    public RawDataQueryInputBase(long reqId, CancellationTokenSource? cancellationTokenSource)
    {
        ReqId = reqId;
        Completed = new();
        CancellationTokenSource = cancellationTokenSource;
    }

    public long ReqId { get; }
    [JsonIgnore] public TaskCompletionSource<object?> Completed { get; init; }
    [JsonIgnore] public CancellationTokenSource? CancellationTokenSource { get; init; }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            // Dispose managed state (managed objects)
            CancellationTokenSource?.Dispose();
        }

        // Free unmanaged resources (unmanaged objects) and override finalizer

        // Set large fields to null

        _isDisposed = true;
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~RawCollectorInputBase()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

internal class GetCompanyByIdInputs : RawDataQueryInputBase
{
    public GetCompanyByIdInputs(long reqId, ulong companyId, CancellationTokenSource? cancellationTokenSource)
        : base(reqId, cancellationTokenSource)
        => CompanyId = companyId;

    public ulong CompanyId { get; init; }
}

internal class GetCompaniesMetadataInputs : RawDataQueryInputBase
{
    public GetCompaniesMetadataInputs(
        long reqId,
        string dataSource,
        PaginationRequest paginationRequest,
        CancellationTokenSource? cancellationTokenSource)
        : base(reqId, cancellationTokenSource)
    {
        DataSource = dataSource;
        Pagination = paginationRequest;
    }

    public string DataSource { get; init; }
    public PaginationRequest Pagination { get; init; }
}
