using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{

    public class ExpensesModel : PageModel
    {
        private readonly DatabaseService _plService;
        public Dictionary<string, string>? ExpenseAccountEmojis { get; set; }

        public List<ProfitLossItem> ProfitLossData { get; set; } = new();

        public ExpensesModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            ExpenseAccountEmojis = appSettings.Value.ExpenseAccountEmojis;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync();
        }
    }

}
