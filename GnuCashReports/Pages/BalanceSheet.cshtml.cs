using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{

    public class BalanceSheetModel : PageModel
    {
        private readonly DatabaseService _dbService;
        private readonly AppSettings _appSettings;

        public List<BalanceSheetItem> BalanceSheetData { get; set; } = new();

        public BalanceSheetModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            BalanceSheetData = await _dbService.GetBalanceSheetAsync();
        }
    }

}
