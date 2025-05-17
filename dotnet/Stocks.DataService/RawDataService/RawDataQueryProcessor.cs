using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataService.RawData;
using Stocks.Persistence;
using Stocks.Protocols;
using Stocks.Shared;
using Stocks.Shared.ProtosExtensions;
using SharedConstants = Stocks.Shared.Constants;

namespace Stocks.DataService.RawDataService;

internal class RawDataQueryProcessor : BackgroundService {
    private readonly IServiceProvider _svp;
    private readonly ILogger<RawDataQueryProcessor> _logger;
    private readonly Channel<RawDataQueryInputBase> _inputChannel;
    private readonly IDbmService _dbm;

    public RawDataQueryProcessor(IServiceProvider svp) {
        _svp = svp;
        _logger = _svp.GetRequiredService<ILogger<RawDataQueryProcessor>>();
        _inputChannel = Channel.CreateUnbounded<RawDataQueryInputBase>();
        _dbm = _svp.GetRequiredService<IDbmService>();

        _logger.LogInformation("RawDataQueryService - Created");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _ = StartHeartbeat(_svp, stoppingToken);

        _logger.LogInformation("RawDataQueryService - Starting main loop");

        await foreach (RawDataQueryInputBase inputBase in _inputChannel.Reader.ReadAllAsync(stoppingToken)) {
            try {
                _logger.LogInformation("RawDataQueryService - Got a message {Input}", inputBase);

                switch (inputBase) {
                    case GetCompanyByIdInputs getCompanyByIdInputs: {
                        ProcessGetCompanyById(getCompanyByIdInputs, stoppingToken);
                        break;
                    }
                    case GetCompaniesMetadataInputs getAllCompanyMetadataInputs: {
                        ProcessGetCompaniesMetadata(getAllCompanyMetadataInputs, stoppingToken);
                        break;
                    }
                    default: {
                        _logger.LogError("RawDataQueryService main loop - Invalid request type received, dropping input");
                        break;
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "RawDataQueryService - Error processing input");
            }
        }

        _logger.LogInformation("RawDataQueryService - Exiting main loop");
    }

    private void ProcessGetCompanyById(GetCompanyByIdInputs inputs, CancellationToken stoppingToken) {
        _ = Task.Run(async () => {
            using IDisposable? reqIdLogContext = _logger.BeginScope("RequestId: {RequestId}", inputs.ReqId);
            using CancellationTokenSource thisRequestCts = Utilities.CreateLinkedTokenSource(inputs.CancellationTokenSource, stoppingToken);
            try {
                Result<Company> res = await _dbm.GetCompanyById(inputs.CompanyId, thisRequestCts.Token);
                LogResults(res);
                GetCompaniesDataReply reply = CreateCompaniesDataReply(res);
                inputs.Completed.SetResult(reply);
            } catch (Exception ex) {
                _logger.LogError(ex, "ProcessGetCompanyById - Error processing query");
                inputs.Completed.SetException(ex);
            }
        }, stoppingToken);

        // Local helper methods

        void LogResults(Result<Company> res) {
            if (res.IsSuccess)
                _logger.LogInformation("ProcessGetCompanyById Success - {CompanyId}", res.Value!.CompanyId);
            else
                _logger.LogInformation("ProcessGetCompanyById Failed - {Error}", res.ErrorMessage);
        }

        GetCompaniesDataReply CreateCompaniesDataReply(Result<Company> res) {
            Company? company = res.Value;
            var reply = new GetCompaniesDataReply {
                Response = new Protocols.StandardResponse { RequestId = inputs.ReqId, Success = res.IsSuccess, ErrorMessage = res.ErrorMessage },
                Pagination = ProtosUtils.CreateEmptyPaginationResponse(),
            };
            if (company != null) {
                reply.CompaniesList.Add(new GetCompaniesDataReplyItem {
                    CompanyId = (long)company.CompanyId,
                    Cik = (long)company.Cik,
                    DataSource = company.DataSource,
                });
            }
            return reply;
        }
    }

    private void ProcessGetCompaniesMetadata(GetCompaniesMetadataInputs inputs, CancellationToken stoppingToken) => _ = Task.Run(async () => await ProcessGetCompaniesMetadataTask(inputs, stoppingToken), stoppingToken);

    private async Task ProcessGetCompaniesMetadataTask(GetCompaniesMetadataInputs inputs, CancellationToken stoppingToken) {
        using IDisposable? reqIdLogContext = _logger.BeginScope("RequestId: {RequestId}", inputs.ReqId);
        using CancellationTokenSource thisRequestCts = Utilities.CreateLinkedTokenSource(inputs.CancellationTokenSource, stoppingToken);
        try {
            Result<PagedCompanies> res = await _dbm.GetPagedCompaniesByDataSource(inputs.DataSource, inputs.Pagination, thisRequestCts.Token);
            LogResults(res);
            GetCompaniesDataReply reply = CreateCompaniesDataReply(res);
            inputs.Completed.SetResult(reply);
        } catch (Exception ex) {
            _logger.LogError(ex, "ProcessGetCompaniesMetadata - Error processing query");
            inputs.Completed.SetException(ex);
        }

        // Local helper methods

        void LogResults(Result<PagedCompanies> res) {
            if (res.IsSuccess)
                _logger.LogInformation("ProcessGetCompaniesMetadata Success - {NumItems}", res.Value!.NumItems);
            else
                _logger.LogInformation("ProcessGetCompaniesMetadata Failed - {Error}", res.ErrorMessage);
        }

        GetCompaniesDataReply CreateCompaniesDataReply(Result<PagedCompanies> res) {
            PagedCompanies? pagedCompanies = res.Value;
            DataModels.PaginationResponse? paginationResponse = res.Value?.Pagination;
            Protocols.PaginationResponse? protosPaginationResponse = res.IsSuccess
                ? paginationResponse?.ToProtosPaginationResponse()
                : ProtosUtils.CreateEmptyPaginationResponse();
            var reply = new GetCompaniesDataReply {
                Response = new StandardResponse { RequestId = inputs.ReqId, Success = res.IsSuccess, ErrorMessage = res.ErrorMessage },
                Pagination = protosPaginationResponse,
            };

            foreach (Company company in res.Value?.Companies ?? []) {
                reply.CompaniesList.Add(new GetCompaniesDataReplyItem {
                    CompanyId = (long)company.CompanyId,
                    Cik = (long)company.Cik,
                    DataSource = company.DataSource,
                });
            }

            return reply;
        }
    }

    public void Post(RawDataQueryInputBase input) => _inputChannel.Writer.TryWrite(input);

    private static async Task StartHeartbeat(IServiceProvider svp, CancellationToken ct) {
        ILogger logger = svp.GetRequiredService<ILogger<RawDataQueryProcessor>>();
        while (!ct.IsCancellationRequested) {
            logger.LogInformation("RawDataQueryService heartbeat");
            await Task.Delay(SharedConstants.OneMinute, ct);
        }
    }
}
