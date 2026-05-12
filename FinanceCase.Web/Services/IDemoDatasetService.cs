namespace FinanceCase.Web.Services;

public interface IDemoDatasetService
{
    Task<ImportSummary> LoadDemoDatasetAsync();
}
