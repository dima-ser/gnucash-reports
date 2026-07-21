using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GnuCashReports.Pages
{

    public class BalanceSheetModel : PageModel
    {
        private readonly DatabaseService _dbService;
        private readonly AppSettings _appSettings;

        public List<ReportItem> BalanceSheetData { get; set; } = new();
        public SelectList DateList;
        [BindProperty (SupportsGet = true)]
        public DateOnly Date {get; set;} = DateOnly.FromDateTime(DateTime.Now);

        public BalanceSheetModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;

            List<string> dates = new List<string>();

            int currentYear = DateTime.Now.Year;
            for (int i = 0; i < _appSettings.NumYearsAvailable; i++)
            {
                if (i==0)
                    dates.Add(DateTime.Now.ToString("yyyy-MM-dd"));
                else
                    dates.Add(new DateOnly(currentYear-i, 12, 31).ToString("yyyy-MM-dd"));
            }
            DateList = new SelectList(dates);
        }

        public async Task OnGetAsync()
        {
            BalanceSheetData = await _dbService.GetBalanceSheetAsync(await _dbService.GetRootBalanceSheetAccountGuids(), Date);
        }
    }

}
