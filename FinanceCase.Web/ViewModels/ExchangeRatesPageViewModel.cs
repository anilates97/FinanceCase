namespace FinanceCase.Web.ViewModels;

public class ExchangeRatesPageViewModel
{
    public DateTime? LastRateDataAt { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<Models.ExchangeRate> Rates { get; set; } = [];
}
