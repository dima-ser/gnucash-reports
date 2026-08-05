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

        public decimal percentSpentLeftYear, percentSavedLeftYear;
        public decimal percentSpentRightYear, percentSavedRightYear;
        [BindProperty (SupportsGet = true)]
        public int YearRight {get; set;} = DateTime.Now.Year;
        [BindProperty (SupportsGet = true)]
        public int YearLeft {get; set;} = DateTime.Now.Year - 1;
        public SelectList YearListRight, YearListLeft;

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

            List<string> exludedIncomeAccounts = _appSettings.ExcludedIncomeAccountsFromSavingRate ?? new List<string>();

            decimal spentLeftYear = profitLossLeft.Where(i => i.AccountType == AccountType.EXPENSE).Sum(i => i.Amount);
            decimal incomeLeftYear = profitLossLeft.Where(i => i.AccountType == AccountType.INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.Amount);
            if (incomeLeftYear != 0)
                percentSpentLeftYear = Math.Round((spentLeftYear / incomeLeftYear) * 100, 1);
            else
                percentSpentLeftYear = 100; // to avoid division by zero.
            percentSavedLeftYear = 100 - percentSpentLeftYear;

            decimal spentRightYear = profitLossRight.Where(i => i.AccountType == AccountType.EXPENSE).Sum(i => i.Amount);
            decimal incomeRightYear = profitLossRight.Where(i => i.AccountType == AccountType.INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.Amount);
            if (incomeRightYear > 0 )
                percentSpentRightYear = Math.Round((spentRightYear / incomeRightYear) * 100, 1);
            else
                percentSpentRightYear = 100; // to avoid division by zero. Saving percentage will show 0% until we have positive income in a year
            percentSavedRightYear = 100 - percentSpentRightYear;

            if (!Request.IsHtmx())
                return Page();

            // AJAX response. Creates a custom "refreshChart" event in the "HX-Trigger" header that HTMX can intercept 
            // and create a DOM trigger which we can then listen on with JS
            var chartPayload = new
            {
                data = new[] { percentSpentLeftYear, percentSavedLeftYear},
                data2 = new[] { percentSpentRightYear, percentSavedRightYear }
            };
            string jsonHeader = JsonSerializer.Serialize(new {
                refreshChart = chartPayload // "refreshChart" is your custom event name
            });
            Response.Headers.Append("HX-Trigger", jsonHeader);

            // Return No Content (204) so HTMX doesn't swap any HTML on the page
            return new NoContentResult();
        }
    }

}
