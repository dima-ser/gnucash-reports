using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace GnuCashReports.Pages
{

    public class AvailableToSpendModel : PageModel
    {
        private readonly DatabaseService _plService;
        private readonly AppSettings _appSettings;
        public List<ProfitLossItem> ProfitLossData { get; set; } = new();
        public decimal availableToSpendThisYear;
        public decimal budgetSavingsRatePercentage;


        public AvailableToSpendModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync(
                new DateTime(DateTime.Now.Year, 1, 1),
                new DateTime(DateTime.Now.Year+1, 1, 1));
            budgetSavingsRatePercentage = _appSettings.TargetSavingsPercentage;
            List<string> exludedIncomeAccounts = _appSettings.ExcludedIncomeAccountsFromSavingRate ?? new List<string>();

            decimal spendingRate = (100 - budgetSavingsRatePercentage) /100;

            decimal spentYTD = ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE).Sum(i => i.Amount);
            decimal incomeYTD = -ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.Amount);
            availableToSpendThisYear = (incomeYTD * spendingRate) - spentYTD;
        }
    }

}
