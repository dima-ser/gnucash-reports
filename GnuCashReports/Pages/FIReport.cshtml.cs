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
            List<string> guids = _appSettings.FISettings.AssetAccountGuids ?? new List<string>();
            if (guids.Count == 0)
            {
                throw new InvalidOperationException("No asset account GUIDs configured for FI report. Please check your appsettings.json configuration.");
            }
            LiquidAssetsData = await _dbService.GetBalanceSheetAsync(guids);
            TotalLiquidAssets = LiquidAssetsData.Sum(item => item.Balance);
            AverageAnnualExpenses = await _dbService.GetAverageAnnualExpenses();
            SafeWithdrawalRate = _appSettings.FISettings.SafeWithdrawalRate;
            TotalNeededForFI = AverageAnnualExpenses / SafeWithdrawalRate;
            AvergeExpensesYearsLookback = _appSettings.FISettings.AverageExpensesYearsLookback;
        }
    }

}
