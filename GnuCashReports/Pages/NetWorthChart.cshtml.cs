using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace GnuCashReports.Pages
{
    public class NetWorthItem
    {
        public DateOnly Date { get; set; }
        public decimal NetWorth { get; set; }
    }
    public class NetWorthIncrease
    {
        public DateOnly Date { get; set; }
        public decimal NetWorthDelta { get; set; }
    }
    public class NetWorthChartModel : PageModel
    {
        private readonly DatabaseService _plService;
        private readonly AppSettings _appSettings;
        public List<NetWorthItem> NetWorthItems { get; set; } = new List<NetWorthItem>();
        public List<NetWorthIncrease> NetWorthIncreases { get; set; } = new List<NetWorthIncrease>();
        public int NumYearsToDisplay { get; set; }
        public NetWorthChartModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            NumYearsToDisplay = _appSettings.NetWorthYearsToDisplay;
            DateTime currentYearEnd = new DateTime(DateTime.Now.Year, 12, 31);
            for (int i = NumYearsToDisplay; i >= 0; i--) {
                DateOnly date = DateOnly.FromDateTime(currentYearEnd.AddYears(-i));
                if (i == 0) // for current year, use YTD date instead of year end
                    date = DateOnly.FromDateTime(DateTime.Now);
                decimal netWorth = await _plService.GetNetWorthAsync(date);
                NetWorthItems.Add(new NetWorthItem { Date = date, NetWorth = Math.Round(netWorth) });
            }
            for (int i = 1; i < NetWorthItems.Count; i++)
            {
                NetWorthIncreases.Add(new NetWorthIncrease { Date = NetWorthItems[i].Date, NetWorthDelta = NetWorthItems[i].NetWorth - NetWorthItems[i-1].NetWorth });
            }
            NetWorthItems.Select(n => n.Date.Year).TakeLast(NumYearsToDisplay);
        }
    }

}
