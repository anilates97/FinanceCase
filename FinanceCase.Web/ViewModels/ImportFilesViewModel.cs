using Microsoft.AspNetCore.Http;

namespace FinanceCase.Web.ViewModels;

public class ImportFilesViewModel
{
    public IFormFile? AssetFile { get; set; }
    public IFormFile? InflationFile { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ShouldRedirectToExchangeRates { get; set; }
    public int RedirectDelaySeconds { get; set; } = 3;
}
