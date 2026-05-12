using FinanceCase.Web.Data;
using FinanceCase.Web.Dtos;
using FinanceCase.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace FinanceCase.Web.Services;

public class ExchangeRateService(HttpClient httpClient, ApplicationDbContext dbContext, IConfiguration configuration) : IExchangeRateService
{
    public async Task<List<ExchangeRateApiDto>> GetCurrentRatesAsync()
    {
        var response = await httpClient.GetAsync(BuildEndpoint());
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponseDto>();

        return apiResponse?.ExchangeRates ?? [];
    }

    public async Task<int> FetchAndSaveCurrentRatesAsync()
    {
        var summary = await FetchAndSaveRatesInternalAsync(BuildEndpoint());
        return summary.TotalChangedCount;
    }

    public async Task<int> FetchAndSaveRatesAsync(DateTime startDate, DateTime endDate)
    {
        var summary = await FetchAndSaveRatesWithSummaryAsync(startDate, endDate);
        return summary.TotalChangedCount;
    }

    public Task<ExchangeRateSyncSummary> FetchAndSaveRatesWithSummaryAsync(DateTime startDate, DateTime endDate)
    {
        var endpoint = BuildEndpoint(startDate, endDate);
        return FetchAndSaveRatesInternalAsync(endpoint);
    }

    private string BuildEndpoint(DateTime? startDate = null, DateTime? endDate = null)
    {
        var apiKey = configuration["FinanceCase:ExchangeRateApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Exchange-rate API key is not configured. Set FinanceCase:ExchangeRateApiKey through environment-specific configuration.");
        }

        var endpoint = $"ExchangeRates?key={Uri.EscapeDataString(apiKey)}";
        if (startDate.HasValue && endDate.HasValue)
        {
            endpoint += $"&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
        }

        return endpoint;
    }

    private async Task<ExchangeRateSyncSummary> FetchAndSaveRatesInternalAsync(string endpoint)
    {
        var response = await httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponseDto>();
        var rates = NormalizeRates(apiResponse?.ExchangeRates ?? [], out var skippedCount);

        if (rates.Count == 0)
        {
            return new ExchangeRateSyncSummary(0, 0, skippedCount);
        }

        var minDate = rates.Min(x => x.CurrentDate);
        var maxDate = rates.Max(x => x.CurrentDate);

        var existingRates = await dbContext.ExchangeRates
            .Where(x => x.CurrentDate >= minDate && x.CurrentDate <= maxDate)
            .ToListAsync();

        var existingByKey = existingRates
            .GroupBy(x => BuildKey(x.BaseCurrencyCode, x.ForeignCurrencyCode, x.CurrentDate))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(rate => rate.Id).First());

        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in rates)
        {
            var key = BuildKey(incoming.BaseCurrencyCode, incoming.ForeignCurrencyCode, incoming.CurrentDate);

            if (existingByKey.TryGetValue(key, out var existing))
            {
                ApplyRateValues(existing, incoming);
                updatedCount++;
                continue;
            }

            await dbContext.ExchangeRates.AddAsync(MapToEntity(incoming));
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();

        return new ExchangeRateSyncSummary(insertedCount, updatedCount, skippedCount);
    }

    private static List<ExchangeRateApiDto> NormalizeRates(List<ExchangeRateApiDto> rates, out int skippedCount)
    {
        var normalizedRates = rates
            .GroupBy(x => BuildKey(x.BaseCurrencyCode, x.ForeignCurrencyCode, x.CurrentDate))
            .Select(x => x.Last())
            .ToList();

        skippedCount = rates.Count - normalizedRates.Count;
        return normalizedRates;
    }

    private static string BuildKey(int baseCurrencyCode, int foreignCurrencyCode, DateTime currentDate)
    {
        return $"{baseCurrencyCode}:{foreignCurrencyCode}:{currentDate.Ticks}";
    }

    private static ExchangeRate MapToEntity(ExchangeRateApiDto dto)
    {
        return new ExchangeRate
        {
            BaseCurrencyCode = dto.BaseCurrencyCode,
            ForeignCurrencyCode = dto.ForeignCurrencyCode,
            ChangeRate = dto.ChangeRate,
            ExchangeRateValue = dto.ExchangeRateValue,
            CashChangeRate = dto.CashChangeRate,
            CashExchangeRate = dto.CashExchangeRate,
            CentralBankChangeRate = dto.CentralBankChangeRate,
            CentralBankExchangeRate = dto.CentralBankExchangeRate,
            CrossRate = dto.CrossRate,
            CurrentDate = dto.CurrentDate
        };
    }

    private static void ApplyRateValues(ExchangeRate entity, ExchangeRateApiDto dto)
    {
        entity.ChangeRate = dto.ChangeRate;
        entity.ExchangeRateValue = dto.ExchangeRateValue;
        entity.CashChangeRate = dto.CashChangeRate;
        entity.CashExchangeRate = dto.CashExchangeRate;
        entity.CentralBankChangeRate = dto.CentralBankChangeRate;
        entity.CentralBankExchangeRate = dto.CentralBankExchangeRate;
        entity.CrossRate = dto.CrossRate;
        entity.CurrentDate = dto.CurrentDate;
    }
}
