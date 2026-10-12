using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Htmx;
using System.Text.Json;

namespace GnuCashReports.Pages
{

    public class SavingsRateModel : PageModel
    {
        private readonly DatabaseService _plService;
        private readonly AppSettings _appSettings;

        public decimal percentSpentLeftYear, percentSpentRightYear, percentSpentAllTime;
        [BindProperty (SupportsGet = true)]
        public int YearRight {get; set;} = DateTime.Now.Year;
        [BindProperty (SupportsGet = true)]
        public int YearLeft {get; set;} = DateTime.Now.Year - 1;
        public SelectList YearListRight, YearListLeft;
        public DateOnly StartDateForAllTimeSavingsRate;

        public SavingsRateModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
              List<string> years = new List<string>();
            int currentYear = DateTime.Now.Year;
            for (int i = 0; i < _appSettings.NumYearsAvailable; i++)
            {
                years.Add((currentYear-i).ToString());
            }
            YearListRight = new SelectList(years, currentYear.ToString());
            YearListLeft = new SelectList(years, (currentYear-1).ToString());
        }

        public async Task<IActionResult> OnGetAsync()
        {
            bool includeFutureTx = _appSettings.IncludeFutureTransactionsInPL;
            DateOnly currentDate = DateOnly.FromDateTime(DateTime.Now);
            int currentYear = currentDate.Year;
            DateOnly endDateRight =  new DateOnly(YearRight, 12, 31), endDateLeft = new DateOnly(YearLeft, 12, 31); 
            if (YearRight == currentYear && !includeFutureTx)
                endDateRight = currentDate;
            if (YearLeft == currentYear && !includeFutureTx)
                endDateLeft = currentDate;

            List<ReportItem> profitLossRight  = await _plService.GetLevel2ProfitLossAsync(
                new DateOnly(YearRight, 1, 1), endDateRight);
            List<ReportItem> profitLossLeft  = await _plService.GetLevel2ProfitLossAsync(
                new DateOnly(YearLeft, 1, 1), endDateLeft);

            
            List<string> excludedIncomeAccounts = _appSettings.ExcludedIncomeAccountsFromSavingRate ?? new List<string>();
            percentSpentLeftYear = GetPercentSpent(profitLossLeft, excludedIncomeAccounts);
            percentSpentRightYear = GetPercentSpent(profitLossRight, excludedIncomeAccounts);

            if (!Request.IsHtmx())
            {
                // the All-time savings rate won't change on AJAX requests, so no need to update it unless it's a full page load
                StartDateForAllTimeSavingsRate = _appSettings.StartDateForAllTimeSavingsRate; // will be DateOnly.MinValue if not configured
                List<ReportItem> profitLossAllTime = await _plService.GetLevel2ProfitLossAsync(
                    StartDateForAllTimeSavingsRate, includeFutureTx ? DateOnly.MaxValue : currentDate); 
                percentSpentAllTime = GetPercentSpent(profitLossAllTime, excludedIncomeAccounts);
                return Page();
            }

            // AJAX response. Creates a custom "refreshChart" event in the "HX-Trigger" header that HTMX can intercept 
            // and create a DOM trigger which we can then listen on with JS
            var chartPayload = new
            {
                data = new[] { percentSpentLeftYear, 100 - percentSpentLeftYear},
                data2 = new[] { percentSpentRightYear, 100 - percentSpentRightYear }
            };
            string jsonHeader = JsonSerializer.Serialize(new {
                refreshChart = chartPayload // "refreshChart" is your custom event name
            });
            Response.Headers.Append("HX-Trigger", jsonHeader);

            // Return No Content (204) so HTMX doesn't swap any HTML on the page
            return new NoContentResult();
        }

        /// <summary>
        /// Returns percentage spent in the specified PL. If income is 0, returns 100%
        /// </summary>
        /// <param name="plStatement"></param>
        /// <param name="excludedIncomeAccounts"></param>
        /// <returns></returns>
        private decimal GetPercentSpent(List<ReportItem> plStatement, List<string> excludedIncomeAccounts)
        {
            decimal expenseAmount = plStatement.Where(i => i.AccountType == AccountType.EXPENSE).Sum(i => i.Amount);
            decimal incomeAmount = plStatement.Where(i => i.AccountType == AccountType.INCOME).Where(i => !excludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.Amount);
            if (incomeAmount != 0) // to avoid division by zero. 
                return Math.Round((expenseAmount / incomeAmount) * 100, 1);
            else
                return 100; // If we have 0 income, we assume 100% spending rate/0% savings rate
        }
    }

}
