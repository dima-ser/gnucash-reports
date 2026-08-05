using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Htmx;

namespace GnuCashReports.Pages
{

    public class ProfitLossModel : PageModel
    {
        private readonly DatabaseService _plService;
        private readonly AppSettings _appSettings;
        public List<ThreeColumnReportItem> Income = new List<ThreeColumnReportItem>();
        public List<ThreeColumnReportItem> Expenses = new List<ThreeColumnReportItem>();

        [BindProperty (SupportsGet = true)]
        public int YearRight {get; set;} = DateTime.Now.Year;
        [BindProperty (SupportsGet = true)]
        public int YearLeft {get; set;} = DateTime.Now.Year - 1;
        public SelectList YearListRight, YearListLeft;
        public Dictionary<string, string>? ExpenseAccountEmojis { get; set; }
        public decimal NetProfitLeft, NetProfitRight;
        public ProfitLossModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
            ExpenseAccountEmojis = appSettings.Value.ExpenseAccountEmojis;
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

            Dictionary<string, decimal> incomeRight = profitLossRight
                .Where(i => i.AccountType == AccountType.INCOME)
                .ToDictionary(i=>i.AccountName, i=>i.Amount);
            Dictionary<string, decimal> incomeLeft = profitLossLeft
                .Where(i => i.AccountType == AccountType.INCOME)
                .ToDictionary(i=>i.AccountName, i=>i.Amount);
            Income = ThreeColumnReportItem.CombineItems(incomeLeft, incomeRight);

            Dictionary<string, decimal> expensesRight = profitLossRight
                .Where(i => i.AccountType == AccountType.EXPENSE)
                .ToDictionary(i=>i.AccountName, i=>i.Amount);
            Dictionary<string, decimal> expensesLeft = profitLossLeft
                .Where(i => i.AccountType == AccountType.EXPENSE)
                .ToDictionary(i=>i.AccountName, i=>i.Amount);
            Expenses = ThreeColumnReportItem.CombineItems(expensesLeft, expensesRight);

            NetProfitLeft = Income.Sum(i => i.AmountLeft) - Expenses.Sum(i => i.AmountLeft);
            NetProfitRight = Income.Sum(i => i.AmountRight) - Expenses.Sum(i => i.AmountRight);

            if (!Request.IsHtmx())
                return Page();
            return Partial("ProfitLossPartial", this);
        }
    }

}
