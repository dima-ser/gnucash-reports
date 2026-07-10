namespace GnuCashReports.Models
{
    public class CashFlowItem
    {
        public required string AccountPath { get; set; }
        public decimal Inflow { get; set; }
        public decimal Outflow { get; set; }
    }

}
