using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;

namespace GnuCashReports.Pages
{

    public class LastUpdatedModel : PageModel
    {
        private readonly DatabaseService _dbService;

        public LastUpdated LastUpdated { get; set; } = new LastUpdated();

        public LastUpdatedModel(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task OnGetAsync()
        {
            LastUpdated = await _dbService.GetLastUpdatedAsync();
        }
    }

}
