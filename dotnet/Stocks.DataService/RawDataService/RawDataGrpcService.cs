using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataService.RawData;
using Stocks.Protocols;
using static Stocks.Protocols.RawDataService;

namespace Stocks.DataService.RawDataService;

internal class RawDataGrpcService : RawDataServiceBase {
    private readonly ILogger<RawDataGrpcService> _logger;
    private readonly RawDataQueryProcessor _processor;

    public RawDataGrpcService(ILogger<RawDataGrpcService> logger, RawDataQueryProcessor processor) {
        _logger = logger;
        _processor = processor;
    }

    public override async Task<GetCompaniesDataReply> GetCompaniesData(GetCompaniesDataRequest request, ServerCallContext context) {
        if (request is null)
            return FailureAsWarning("Request is null");
        if (context is null)
            return FailureAsWarning("Context is null");

        try {
            using IDisposable? reqIdLogContext = _logger.BeginScope("RequestId: {RequestId}", request.RequestId);
            _logger.LogInformation("GetCompaniesData");

            // Create a token source that gets canceled when the client disconnects or the call times out
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

            var paginationRequest = new DataModels.PaginationRequest(request.Pagination.PageNumber, request.Pagination.PageSize);
            using var inputs = new GetCompaniesMetadataInputs(
                request.RequestId,
                ModelsConstants.EdgarDataSource,
                paginationRequest,
                cts);

            _processor.Post(inputs);
            object? rawResponse = await inputs.Completed.Task;

            // Double-check that we got the right output type
            if (rawResponse is not GetCompaniesDataReply reply) {
                _logger.LogWarning("GetCompaniesData - Unexpected response type: {ResponseType}", rawResponse?.GetType().Name);
                return FailureAsWarning("Unexpected response type");
            }

            _logger.LogInformation("GetCompaniesData - Done");
            return reply;
        } catch (OperationCanceledException) {
            return FailureAsWarning("Cancelled");
        } catch (Exception ex) {
            return Failure(ex, "General Fault");
        }

        // Local helper methods

        GetCompaniesDataReply FailureAsWarning(string errMsg) => new() { Response = GetStandardErrorResponseAsWarning(errMsg) };
        GetCompaniesDataReply Failure(Exception ex, string errMsg) => new() { Response = GetStandardErrorResponse(ex, errMsg) };
    }

    private StandardResponse GetStandardErrorResponseAsWarning(string errMsg, [CallerMemberName] string callerFn = "") {
        _logger.LogWarning(callerFn + " - {ErrorMessage}", errMsg);
        return new() { Success = false, ErrorMessage = errMsg };
    }

    private StandardResponse GetStandardErrorResponse(Exception ex, string errMsg, [CallerMemberName] string callerFn = "") {
        _logger.LogError(ex, callerFn + " - {ErrorMessage}", errMsg);
        return new() { Success = false, ErrorMessage = errMsg };
    }
}
