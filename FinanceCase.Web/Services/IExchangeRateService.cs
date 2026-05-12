using FinanceCase.Web.Dtos;

namespace FinanceCase.Web.Services;

public interface IExchangeRateService
{
    Task<List<ExchangeRateApiDto>> GetCurrentRatesAsync();
    Task<int> FetchAndSaveCurrentRatesAsync();
    Task<int> FetchAndSaveRatesAsync(DateTime startDate, DateTime endDate);
    Task<ExchangeRateSyncSummary> FetchAndSaveRatesWithSummaryAsync(DateTime startDate, DateTime endDate);
}

public sealed record ExchangeRateSyncSummary(
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount)
{
    public int TotalChangedCount => InsertedCount + UpdatedCount;
}
