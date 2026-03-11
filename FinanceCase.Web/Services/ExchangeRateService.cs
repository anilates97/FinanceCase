using FinanceCase.Web.Data;
using FinanceCase.Web.Dtos;
using FinanceCase.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace FinanceCase.Web.Services;

public class ExchangeRateService(HttpClient httpClient, ApplicationDbContext dbContext) : IExchangeRateService
{
    private const string ExchangeRateEndpoint = "ExchangeRates?key=Finmaks123";

    public async Task<List<ExchangeRateApiDto>> GetCurrentRatesAsync()
    {
        var response = await httpClient.GetAsync(ExchangeRateEndpoint);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponseDto>();

        return apiResponse?.ExchangeRates ?? [];
    }

    public Task<int> FetchAndSaveCurrentRatesAsync()
    {
        return FetchAndSaveRatesInternalAsync(ExchangeRateEndpoint);
    }

    public Task<int> FetchAndSaveRatesAsync(DateTime startDate, DateTime endDate)
    {
        var endpoint = $"ExchangeRates?key=Finmaks123&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
        return FetchAndSaveRatesInternalAsync(endpoint);
    }

    private async Task<int> FetchAndSaveRatesInternalAsync(string endpoint)
    {
        var response = await httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponseDto>();
        var rates = apiResponse?.ExchangeRates ?? [];

        if (rates.Count == 0)
        {
            return 0;
        }

        var incomingDates = rates
            .Select(x => x.CurrentDate.Date)
            .Distinct()
            .ToList();

        var existingRates = await dbContext.ExchangeRates
            .Where(x => incomingDates.Contains(x.CurrentDate.Date))
            .ToListAsync();

        // aynı gün için eski kayıtları silip yeni listeyi tekrar yazarız
        if (existingRates.Count > 0)
        {
            dbContext.ExchangeRates.RemoveRange(existingRates);
        }

        var entities = rates.Select(x => new ExchangeRate
        {
            BaseCurrencyCode = x.BaseCurrencyCode,
            ForeignCurrencyCode = x.ForeignCurrencyCode,
            ChangeRate = x.ChangeRate,
            ExchangeRateValue = x.ExchangeRateValue,
            CashChangeRate = x.CashChangeRate,
            CashExchangeRate = x.CashExchangeRate,
            CentralBankChangeRate = x.CentralBankChangeRate,
            CentralBankExchangeRate = x.CentralBankExchangeRate,
            CrossRate = x.CrossRate,
            CurrentDate = x.CurrentDate
        }).ToList();

        await dbContext.ExchangeRates.AddRangeAsync(entities);
        await dbContext.SaveChangesAsync();

        return entities.Count;
    }
}
