namespace FinanceCase.Web.ViewModels;

public class CalculationResultRowViewModel
{
    public DateTime Period { get; set; }
    public decimal AssetAmount { get; set; }
    public decimal PreviousMonthAssetIncrease { get; set; }
    public decimal AssetChangeRate { get; set; }
    public decimal AssetMonthUsdRate { get; set; }
    public decimal DollarizedAssetAmount { get; set; }
    public decimal PreviousMonthDollarizedIncrease { get; set; }
    public decimal DollarizedChangeRate { get; set; }
    public decimal DollarizationEffectRate { get; set; }
    public decimal InflationIndex { get; set; }
    public decimal InflationAdjustedAssetAmount { get; set; }
    public decimal PreviousMonthInflationAdjustedIncrease { get; set; }
    public decimal InflationAdjustedChangeRate { get; set; }
    public decimal InflationEffectRate { get; set; }
}
