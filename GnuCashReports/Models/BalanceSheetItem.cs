namespace GnuCashReports.Models
{
    public class BalanceSheetItem
    {
        public required string AccountType { get; set; }
        public required string AccountName { get; set; }
        public decimal Balance { get; set; }
    }

}
