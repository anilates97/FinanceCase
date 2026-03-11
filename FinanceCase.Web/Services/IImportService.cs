using Microsoft.AspNetCore.Http;

namespace FinanceCase.Web.Services;

public interface IImportService
{
    Task<int> ImportAssetRecordsAsync(IFormFile assetFile);
    Task<int> ImportInflationIndexRecordsAsync(IFormFile inflationFile);
}
