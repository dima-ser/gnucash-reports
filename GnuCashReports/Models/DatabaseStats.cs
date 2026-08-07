namespace GnuCashReports.Models
{
    public class DatabaseStats
    {
        public DateTime LastUpdatedDate { get; set; }
        public DateOnly LastPriceDate { get; set; }

        public DateOnly OldestTransactionDate { get; set; }

        public int TransactionCount {get; set; }

        public int AllAccountCount {get; set; }
        public int ActiveAccountCount {get; set; }

        /// <summary>
        /// Returns the number of calendar years (including partial years) worth of data in the database to use in reports. 
        /// Returns AppSettings.MIN_NUM_YEARS_AVAILABLE if there are less than AppSettings.MIN_NUM_YEARS_AVAILABLE years worth of data.
        /// </summary>
        public int YearsAvailableForReports
        {
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                int years = today.Year - OldestTransactionDate.Year + 1; // adding 1 to include current year

                if (years < AppSettings.MIN_NUM_YEARS_AVAILABLE)
                    years = AppSettings.MIN_NUM_YEARS_AVAILABLE;

                return years;
            }
        }
        public int CompleteYearsSinceOldestTransaction { 
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
