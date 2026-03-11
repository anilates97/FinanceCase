using FinanceCase.Web.Dtos;

namespace FinanceCase.Web.Services;

public interface IExchangeRateService
{
    Task<List<ExchangeRateApiDto>> GetCurrentRatesAsync();
    Task<int> FetchAndSaveCurrentRatesAsync();
    Task<int> FetchAndSaveRatesAsync(DateTime startDate, DateTime endDate);
}
