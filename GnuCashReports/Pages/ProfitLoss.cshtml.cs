using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;

namespace GnuCashReports.Pages
{

    public class ProfitLossModel : PageModel
    {
        private readonly DatabaseService _plService;

        public List<ProfitLossItem> ProfitLossData { get; set; } = new();

        public ProfitLossModel(DatabaseService plService)
        {
            _plService = plService;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync();
        }
    }

}
