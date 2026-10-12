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
        public List<ReportItem> ProfitLossData { get; set; } = new();
        public decimal availableToSpendThisRollingYear;
        public decimal budgetSavingsRatePercentage;


        public AvailableToSpendModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            //bool includeFutureTx = _appSettings.IncludeFutureTransactionsInPL;
            //DateOnly endDate = includeFutureTx ? new DateOnly(DateTime.Now.Year, 12, 31) : DateOnly.FromDateTime(DateTime.Now);
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync(today.AddYears(-1), today);
            budgetSavingsRatePercentage = _appSettings.TargetSavingsPercentage;
            List<string> exludedIncomeAccounts = _appSettings.ExcludedIncomeAccountsFromSavingRate ?? new List<string>();

            decimal spendingRate = (100 - budgetSavingsRatePercentage) /100;

            decimal expenseAmount = ProfitLossData.Where(i => i.AccountType == AccountType.EXPENSE).Sum(i => i.Amount);
            decimal incomeAmount = ProfitLossData.Where(i => i.AccountType == AccountType.INCOME).Where(i => !exludedIncomeAccounts.Contains(i.AccountName)).Sum(i => i.Amount);
            availableToSpendThisRollingYear = (incomeAmount * spendingRate) - expenseAmount;
        }
    }

}
