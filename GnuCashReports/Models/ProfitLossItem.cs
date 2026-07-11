namespace GnuCashReports.Models
{
    public class ProfitLossItem
    {
        public required string AccountType { get; set; }
        public required string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

}
