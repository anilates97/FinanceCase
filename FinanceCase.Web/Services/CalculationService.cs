using FinanceCase.Web.Constants;
using FinanceCase.Web.Data;
using FinanceCase.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Web.Services;

public class CalculationService(ApplicationDbContext dbContext) : ICalculationService
{
    public async Task<List<CalculationResultRowViewModel>> CalculateAsync(DateTime? startPeriod = null, DateTime? endPeriod = null)
    {
        var assetQuery = dbContext.AssetRecords.AsQueryable();

        if (startPeriod.HasValue)
        {
            assetQuery = assetQuery.Where(x => x.Period >= startPeriod.Value);
        }

        if (endPeriod.HasValue)
        {
            assetQuery = assetQuery.Where(x => x.Period <= endPeriod.Value);
        }

        var assets = await assetQuery
            .OrderBy(x => x.Period)
            .ToListAsync();

        var exchangeRates = await dbContext.ExchangeRates
            .Where(x => x.BaseCurrencyCode == CurrencyCodes.Usd && x.ForeignCurrencyCode == CurrencyCodes.Try)
            .OrderBy(x => x.CurrentDate)
            .ToListAsync();

        var inflationIndexes = await dbContext.InflationIndexRecords
            .OrderBy(x => x.Period)
            .ToListAsync();

        if (assets.Count == 0)
        {
            return [];
        }

        // sadece hem kur hem endeks bulunan aylar hesaplamaya dahil edilir
        var calculablePeriods = assets
            .Select(x => new DateTime(x.Period.Year, x.Period.Month, 1))
            .Where(period =>
                GetInflationIndex(inflationIndexes, period) > 0 &&
                GetMonthEndUsdRate(exchangeRates, period) > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (calculablePeriods.Count == 0)
        {
            return [];
        }

        var reportPeriod = ResolveReportPeriod(calculablePeriods, endPeriod);
        var reportUsdRate = GetMonthEndUsdRate(exchangeRates, reportPeriod);
        var reportInflationIndex = GetInflationIndex(inflationIndexes, reportPeriod);

        var results = new List<CalculationResultRowViewModel>();

        foreach (var asset in assets.Where(x => x.Period <= reportPeriod && calculablePeriods.Contains(new DateTime(x.Period.Year, x.Period.Month, 1))))
        {
            var previousResult = results.LastOrDefault();
            var assetMonthUsdRate = GetMonthEndUsdRate(exchangeRates, asset.Period);
            var inflationIndex = GetInflationIndex(inflationIndexes, asset.Period);

            var previousMonthAssetIncrease = previousResult is null
                ? 0
                : asset.AssetAmount - previousResult.AssetAmount;

            var assetChangeRate = previousResult is null || previousResult.AssetAmount == 0
                ? 0
                : previousMonthAssetIncrease / previousResult.AssetAmount;

            var dollarizedAssetAmount = assetMonthUsdRate == 0 || reportUsdRate == 0
                ? 0
                : (reportUsdRate * asset.AssetAmount) / assetMonthUsdRate;

            var previousMonthDollarizedIncrease = previousResult is null
                ? 0
                : dollarizedAssetAmount - previousResult.DollarizedAssetAmount;

            var dollarizedChangeRate = previousResult is null || previousResult.DollarizedAssetAmount == 0
                ? 0
                : previousMonthDollarizedIncrease / previousResult.DollarizedAssetAmount;

            var dollarizationEffectRate = dollarizedChangeRate - assetChangeRate;

            var inflationAdjustedAssetAmount = inflationIndex == 0 || reportInflationIndex == 0
                ? 0
                : (reportInflationIndex * asset.AssetAmount) / inflationIndex;

            var previousMonthInflationAdjustedIncrease = previousResult is null
                ? 0
                : inflationAdjustedAssetAmount - previousResult.InflationAdjustedAssetAmount;

            var inflationAdjustedChangeRate = previousResult is null || previousResult.InflationAdjustedAssetAmount == 0
                ? 0
                : previousMonthInflationAdjustedIncrease / previousResult.InflationAdjustedAssetAmount;

            var inflationEffectRate = inflationAdjustedChangeRate - assetChangeRate;

            results.Add(new CalculationResultRowViewModel
            {
                Period = asset.Period,
                AssetAmount = asset.AssetAmount,
                PreviousMonthAssetIncrease = previousMonthAssetIncrease,
                AssetChangeRate = assetChangeRate,
                AssetMonthUsdRate = assetMonthUsdRate,
                DollarizedAssetAmount = dollarizedAssetAmount,
                PreviousMonthDollarizedIncrease = previousMonthDollarizedIncrease,
                DollarizedChangeRate = dollarizedChangeRate,
                DollarizationEffectRate = dollarizationEffectRate,
                InflationIndex = inflationIndex,
                InflationAdjustedAssetAmount = inflationAdjustedAssetAmount,
                PreviousMonthInflationAdjustedIncrease = previousMonthInflationAdjustedIncrease,
                InflationAdjustedChangeRate = inflationAdjustedChangeRate,
                InflationEffectRate = inflationEffectRate
            });
        }

        return results;
    }

    private static DateTime ResolveReportPeriod(List<DateTime> calculablePeriods, DateTime? requestedEndPeriod)
    {
        if (requestedEndPeriod.HasValue)
        {
            var requestedMonth = new DateTime(requestedEndPeriod.Value.Year, requestedEndPeriod.Value.Month, 1);

            // clamp future user selections to the latest available data range
            var clampedPeriod = calculablePeriods.LastOrDefault(x => x <= requestedMonth);
            return clampedPeriod == default ? calculablePeriods.First() : clampedPeriod;
        }

        return calculablePeriods.Last();
    }

    private static decimal GetMonthEndUsdRate(List<Models.ExchangeRate> exchangeRates, DateTime period)
    {
        var monthStart = new DateTime(period.Year, period.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // monthly calculations use the latest exchange-rate record in the period
        var monthlyRate = exchangeRates
            .Where(x => x.CurrentDate >= monthStart && x.CurrentDate < monthEnd)
            .OrderByDescending(x => x.CurrentDate)
            .FirstOrDefault();

        return monthlyRate?.CashExchangeRate ?? 0;
    }

    private static decimal GetInflationIndex(List<Models.InflationIndexRecord> inflationIndexes, DateTime period)
    {
        var record = inflationIndexes
            .FirstOrDefault(x => x.Period.Year == period.Year && x.Period.Month == period.Month);

        return record?.IndexValue ?? 0;
    }
}
