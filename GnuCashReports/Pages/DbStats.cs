using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;

namespace GnuCashReports.Pages
{

    public class DbStatsModel : PageModel
    {
        private readonly DatabaseService _dbService;

        public DatabaseStats DbStats { get; set; } = new DatabaseStats();

        public DbStatsModel(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task OnGetAsync()
        {
            DbStats = await _dbService.GetDatabaseStatsAsync();
        }
    }

}
