using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;

namespace GnuCashReports.Pages
{

    public class BalanceSheetModel : PageModel
    {
        private readonly DatabaseService _dbService;

        public List<BalanceSheetItem> BalanceSheetData { get; set; } = new();

        public BalanceSheetModel(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task OnGetAsync()
        {
            BalanceSheetData = await _dbService.GetBalanceSheetAsync();
        }
    }

}
