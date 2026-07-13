using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{

    public class FIReportModel : PageModel
    {
        private readonly DatabaseService _dbService;
        private readonly AppSettings _appSettings;

        private List<BalanceSheetItem> LiquidAssetsData { get; set; } = new();

        public decimal TotalLiquidAssets { get; set; }
        public decimal AverageAnnualExpenses { get; set; }
        public decimal TotalNeededForFI { get; set; }
        public decimal ProgressTowardsFI { get; set; }

        public decimal SafeWithdrawalRate { get; set; }
        public decimal AvergeExpensesYearsLookback { get; set; }

        public FIReportModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            if (_appSettings.FISettings == null)
            {
                throw new InvalidOperationException("FISettings section is missing in appsettings.json. Please check your configuration.");
            }
            List<string> liquidAssetParentAccounts = _appSettings.FISettings.LiquidAssetParentAccounts ?? new List<string>();
            if (liquidAssetParentAccounts.Count == 0)
            {
                throw new InvalidOperationException("No LiquidAssetParentAccounts configured for FI report. Please check your appsettings.json configuration.");
            }
            List<string> guids = new List<string>();
            foreach (string accountName in liquidAssetParentAccounts)
            {
                guids.Add(await _dbService.GetAccountGuid(accountName));
            }
            LiquidAssetsData = await _dbService.GetBalanceSheetAsync(guids, DateOnly.FromDateTime(DateTime.Now) );
            TotalLiquidAssets = LiquidAssetsData.Sum(item => item.Balance);
            AverageAnnualExpenses = await _dbService.GetAverageAnnualExpenses();
            SafeWithdrawalRate = _appSettings.FISettings.SafeWithdrawalRate;
            TotalNeededForFI = AverageAnnualExpenses / SafeWithdrawalRate;
            AvergeExpensesYearsLookback = _appSettings.FISettings.AverageExpensesYearsLookback;

            if (TotalNeededForFI > 0)
                ProgressTowardsFI = TotalLiquidAssets / TotalNeededForFI;
            else
                ProgressTowardsFI = 1; // you're ready to retire since your average expenses are 0
        }
    }

}
