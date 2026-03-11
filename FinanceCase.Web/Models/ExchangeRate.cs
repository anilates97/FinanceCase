namespace FinanceCase.Web.Models;

public class ExchangeRate
{
    public int Id { get; set; }
    public int BaseCurrencyCode { get; set; }
    public int ForeignCurrencyCode { get; set; }
    public decimal ChangeRate { get; set; }
    public decimal ExchangeRateValue { get; set; }
    public decimal CashChangeRate { get; set; }
    public decimal CashExchangeRate { get; set; }
    public decimal CentralBankChangeRate { get; set; }
    public decimal CentralBankExchangeRate { get; set; }
    public decimal CrossRate { get; set; }
    public DateTime CurrentDate { get; set; }
}
