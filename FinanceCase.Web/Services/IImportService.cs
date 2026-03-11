using Microsoft.AspNetCore.Http;

namespace FinanceCase.Web.Services;

public interface IImportService
{
    Task<ImportSummary> ImportAsync(IFormFile assetFile, IFormFile inflationFile);
}

public sealed record ImportSummary(
    int AssetCount,
    int InflationCount,
    int SyncedExchangeRateCount,
    DateTime StartPeriod,
    DateTime EndPeriod);
