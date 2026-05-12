using Microsoft.AspNetCore.Http;

namespace FinanceCase.Web.Services;

public interface IImportService
{
    Task<ImportSummary> ImportAsync(IFormFile assetFile, IFormFile inflationFile);
}

public sealed record ImportSummary(
    int AssetInsertedCount,
    int AssetUpdatedCount,
    int AssetSkippedCount,
    int InflationInsertedCount,
    int InflationUpdatedCount,
    int InflationSkippedCount,
    int ExchangeRateInsertedCount,
    int ExchangeRateUpdatedCount,
    int ExchangeRateSkippedCount,
    TimeSpan Duration,
    DateTime StartPeriod,
    DateTime EndPeriod);

public sealed record UpsertSummary(
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount);
