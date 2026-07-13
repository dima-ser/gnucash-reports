namespace GnuCashReports.Models
{
    public class ReportItem
    {
        public required string AccountType { get; set; }
        public required string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

}
