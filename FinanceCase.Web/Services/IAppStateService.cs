using FinanceCase.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Web.Services;

public interface IAppStateService
{
    Task<bool> IsDataReadyAsync();
}

// menüdeki ekranları göstermeden önce temel verilerin hazır olduğunu kontrol eder
public class AppStateService(ApplicationDbContext dbContext) : IAppStateService
{
    public async Task<bool> IsDataReadyAsync()
    {
        var hasAssets = await dbContext.AssetRecords.AnyAsync();
        if (!hasAssets)
        {
            return false;
        }

        var hasInflationIndexes = await dbContext.InflationIndexRecords.AnyAsync();
        if (!hasInflationIndexes)
        {
            return false;
        }

        return await dbContext.ExchangeRates.AnyAsync();
    }
}
