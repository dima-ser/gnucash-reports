namespace GnuCashReports.Models
{
    public class DatabaseStats
    {
        public DateOnly LastUpdatedDate { get; set; }
        public DateOnly LastPriceDate { get; set; }

        public DateOnly OldestTransactionDate { get; set; }

        public int TransactionCount {get; set; }

        public int AllAccountCount {get; set; }
        public int ActiveAccountCount {get; set; }

        public int YearsSinceOldestTransaction { 
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                int years = today.Year - OldestTransactionDate.Year;

                if (OldestTransactionDate > today.AddYears(-years))
                    years--;

                return years;
            }
        }
        public DatabaseStats()
        {
            
        }
    }

}
