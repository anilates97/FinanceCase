namespace FinanceCase.Web.Models;

public class InflationIndexRecord
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime Period { get; set; }
    public decimal IndexValue { get; set; }
}
