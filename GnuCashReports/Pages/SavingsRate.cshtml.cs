using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace GnuCashReports.Pages
{

    public class SavingsRateModel : PageModel
    {
        private readonly DatabaseService _plService;
        private readonly AppSettings _appSettings;

        public List<ProfitLossItem> ProfitLossData { get; set; } = new();
        public decimal percentSpentPrevYear, percentSavedPrevYear;
        public decimal percentSpentYTD, percentSavedYTD;

        public SavingsRateModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync();
            List<string> exludedIncomeAccounts = _appSettings.ExcludedIncomeAccountsFromSavingRate ?? new List<string>();

            decimal spentPrevYear = ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE).Sum(i => i.TotalAmountPrevYear);
            decimal totalPrevYear = -ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.TotalAmountPrevYear);
            percentSpentPrevYear = Math.Round((spentPrevYear / totalPrevYear) * 100, 1);
            percentSavedPrevYear = 100 - percentSpentPrevYear;
            decimal spentYTD = ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE).Sum(i => i.TotalAmountYTD);
            decimal totalYTD = -ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.TotalAmountYTD);
            if (totalYTD > 0 )
                percentSpentYTD = Math.Round((spentYTD / totalYTD) * 100, 1);
            else
                percentSpentYTD = 100; // to avoid division by zero. Saving percentage will show 0% until we have positive income in a year
            percentSavedYTD = 100 - percentSpentYTD;
        }
    }

}
