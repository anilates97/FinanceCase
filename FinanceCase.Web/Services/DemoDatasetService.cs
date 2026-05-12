using FinanceCase.Web.Constants;
using FinanceCase.Web.Data;
using FinanceCase.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FinanceCase.Web.Services;

public class DemoDatasetService(ApplicationDbContext dbContext) : IDemoDatasetService
{
    public async Task<ImportSummary> LoadDemoDatasetAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var startPeriod = new DateTime(2024, 1, 1);
        var monthCount = 24;

        var assetRecords = BuildAssetRecords(startPeriod, monthCount);
        var inflationRecords = BuildInflationRecords(startPeriod, monthCount);
        var exchangeRates = BuildExchangeRates(startPeriod, monthCount);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var assetSummary = await UpsertAssetRecordsAsync(assetRecords);
            var inflationSummary = await UpsertInflationRecordsAsync(inflationRecords);
            var exchangeSummary = await UpsertExchangeRatesAsync(exchangeRates);

            await transaction.CommitAsync();
            stopwatch.Stop();

            return new ImportSummary(
                assetSummary.InsertedCount,
                assetSummary.UpdatedCount,
                assetSummary.SkippedCount,
                inflationSummary.InsertedCount,
                inflationSummary.UpdatedCount,
                inflationSummary.SkippedCount,
                exchangeSummary.InsertedCount,
                exchangeSummary.UpdatedCount,
                exchangeSummary.SkippedCount,
                stopwatch.Elapsed,
                assetRecords.Min(x => x.Period),
                assetRecords.Max(x => x.Period));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Demo dataset could not be loaded. All changes were rolled back; no partial demo data was written.", ex);
        }
    }

    private async Task<UpsertSummary> UpsertAssetRecordsAsync(List<AssetRecord> incomingRecords)
    {
        var periods = incomingRecords.Select(x => x.Period).ToList();
        var existingRecords = await dbContext.AssetRecords
            .Where(x => periods.Contains(x.Period))
            .ToListAsync();

        var existingByPeriod = existingRecords.ToDictionary(x => x.Period);
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in incomingRecords)
        {
            if (existingByPeriod.TryGetValue(incoming.Period, out var existing))
            {
                existing.AssetAmount = incoming.AssetAmount;
                updatedCount++;
                continue;
            }

            await dbContext.AssetRecords.AddAsync(incoming);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();
        return new UpsertSummary(insertedCount, updatedCount, 0);
    }

    private async Task<UpsertSummary> UpsertInflationRecordsAsync(List<InflationIndexRecord> incomingRecords)
    {
        var periods = incomingRecords.Select(x => x.Period).ToList();
        var existingRecords = await dbContext.InflationIndexRecords
            .Where(x => periods.Contains(x.Period))
            .ToListAsync();

        var existingByPeriod = existingRecords.ToDictionary(x => x.Period);
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in incomingRecords)
        {
            if (existingByPeriod.TryGetValue(incoming.Period, out var existing))
            {
                existing.Year = incoming.Year;
                existing.Month = incoming.Month;
                existing.IndexValue = incoming.IndexValue;
                updatedCount++;
                continue;
            }

            await dbContext.InflationIndexRecords.AddAsync(incoming);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();
        return new UpsertSummary(insertedCount, updatedCount, 0);
    }

    private async Task<ExchangeRateSyncSummary> UpsertExchangeRatesAsync(List<ExchangeRate> incomingRates)
    {
        var minDate = incomingRates.Min(x => x.CurrentDate);
        var maxDate = incomingRates.Max(x => x.CurrentDate);

        var existingRates = await dbContext.ExchangeRates
            .Where(x => x.CurrentDate >= minDate && x.CurrentDate <= maxDate)
            .ToListAsync();

        var existingByKey = existingRates
            .GroupBy(x => BuildExchangeRateKey(x.BaseCurrencyCode, x.ForeignCurrencyCode, x.CurrentDate))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(rate => rate.Id).First());
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in incomingRates)
        {
            var key = BuildExchangeRateKey(incoming.BaseCurrencyCode, incoming.ForeignCurrencyCode, incoming.CurrentDate);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.ChangeRate = incoming.ChangeRate;
                existing.ExchangeRateValue = incoming.ExchangeRateValue;
                existing.CashChangeRate = incoming.CashChangeRate;
                existing.CashExchangeRate = incoming.CashExchangeRate;
                existing.CentralBankChangeRate = incoming.CentralBankChangeRate;
                existing.CentralBankExchangeRate = incoming.CentralBankExchangeRate;
                existing.CrossRate = incoming.CrossRate;
                updatedCount++;
                continue;
            }

            await dbContext.ExchangeRates.AddAsync(incoming);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();
        return new ExchangeRateSyncSummary(insertedCount, updatedCount, 0);
    }

    private static List<AssetRecord> BuildAssetRecords(DateTime startPeriod, int monthCount)
    {
        var records = new List<AssetRecord>();
        var value = 18_750_000m;

        for (var index = 0; index < monthCount; index++)
        {
            var period = startPeriod.AddMonths(index);
            var seasonalAdjustment = index % 6 == 0 ? 385_000m : 0m;
            var monthlyGrowth = 1.018m + ((index % 4) * 0.002m);
            value = (value * monthlyGrowth) + seasonalAdjustment;

            records.Add(new AssetRecord
            {
                Period = period,
                AssetAmount = decimal.Round(value, 2)
            });
        }

        return records;
    }

    private static List<InflationIndexRecord> BuildInflationRecords(DateTime startPeriod, int monthCount)
    {
        var records = new List<InflationIndexRecord>();
        var indexValue = 2_900m;

        for (var index = 0; index < monthCount; index++)
        {
            var period = startPeriod.AddMonths(index);
            indexValue *= 1.024m + ((index % 5) * 0.001m);

            records.Add(new InflationIndexRecord
            {
                Year = period.Year,
                Month = period.Month,
                Period = period,
                IndexValue = decimal.Round(indexValue, 2)
            });
        }

        return records;
    }

    private static List<ExchangeRate> BuildExchangeRates(DateTime startPeriod, int monthCount)
    {
        var records = new List<ExchangeRate>();
        var usdTry = 29.45m;

        for (var index = 0; index < monthCount; index++)
        {
            var period = startPeriod.AddMonths(index);
            var currentDate = new DateTime(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month), 16, 30, 0);
            var previousRate = usdTry;
            usdTry *= 1.017m + ((index % 3) * 0.003m);
            var changeRate = (usdTry - previousRate) / previousRate;

            records.Add(new ExchangeRate
            {
                BaseCurrencyCode = CurrencyCodes.Usd,
                ForeignCurrencyCode = CurrencyCodes.Try,
                ChangeRate = decimal.Round(changeRate, 6),
                ExchangeRateValue = decimal.Round(usdTry - 0.03m, 6),
                CashChangeRate = decimal.Round(changeRate, 6),
                CashExchangeRate = decimal.Round(usdTry, 6),
                CentralBankChangeRate = decimal.Round(changeRate * 0.98m, 6),
                CentralBankExchangeRate = decimal.Round(usdTry - 0.015m, 6),
                CrossRate = 1,
                CurrentDate = currentDate
            });
        }

        return records;
    }

    private static string BuildExchangeRateKey(int baseCurrencyCode, int foreignCurrencyCode, DateTime currentDate)
    {
        return $"{baseCurrencyCode}:{foreignCurrencyCode}:{currentDate.Ticks}";
    }
}
