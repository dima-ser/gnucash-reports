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

        public List<BalanceSheetItem> BalanceSheetData { get; set; } = new();

        public FIReportModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            List<string> guids = _appSettings.FISettings?.AssetAccountGuids ?? new List<string>();
            if (guids.Count == 0)
            {
                throw new InvalidOperationException("No asset account GUIDs configured for FI report. Please check your appsettings.json configuration.");
            }
            BalanceSheetData = await _dbService.GetBalanceSheetAsync(guids);
        }
    }

}
