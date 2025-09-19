namespace GnuCashReports.Models
{
    public class LastUpdated
    {
        public DateTime LastTransactionDate { get; set; }
        public DateTime LastPriceDate { get; set; }

        public LastUpdated()
        {
            LastTransactionDate = DateTime.MinValue;
            LastPriceDate = DateTime.MinValue;
        }
    }

}
