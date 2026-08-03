namespace GnuCashReports.Models
{
    /// <summary>
    /// Contains static strings of (some of the) valid GnuCash account types
    /// </summary>
    public static class AccountType
    {
        public static readonly string ASSET = "ASSET";
        public static readonly string LIABILITY = "LIABILITY";
        public static readonly string INCOME = "INCOME";
        public static readonly string EXPENSE = "EXPENSE";
        public static readonly string EQUITY = "EQUITY";
    }
    /// <summary>
    /// Represents a GnuCash report line item.
    /// </summary>
    public class ReportItem
    {
        public required string AccountType { get; set; }
        public required string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

}
