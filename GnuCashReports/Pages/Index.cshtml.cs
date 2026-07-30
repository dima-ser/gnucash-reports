using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        public List<string>? DashboardLayout {get; set; }

        public IndexModel(ILogger<IndexModel> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;

            DashboardLayout = appSettings.Value.DashboardLayout;
            
        }

        public void OnGet()
        {

        }
    }
}
