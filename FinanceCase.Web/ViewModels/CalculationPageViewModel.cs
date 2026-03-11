namespace FinanceCase.Web.ViewModels;

public class CalculationPageViewModel
{
    public DateTime? StartPeriod { get; set; }
    public DateTime? EndPeriod { get; set; }
    public List<CalculationResultRowViewModel> Rows { get; set; } = [];
}
