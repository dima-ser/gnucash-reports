using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{

    public class CashflowModel : PageModel
    {
        private readonly DatabaseService _dbService;

        private readonly AppSettings _appSettings;
        public Dictionary<string, decimal> InflowsYtd = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> OutflowsYtd = new Dictionary<string, decimal>();

        public CashflowModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            if (String.IsNullOrWhiteSpace(_appSettings.ParentCashAccount))
                throw new Exception("Missing configuration \"ParentCashAccount\"");
            List<CashFlowItem> cashFlowItemsYtd  = await _dbService.GetCashFlowStatement(
                _appSettings.ParentCashAccount, 
                new DateTime(DateTime.Now.Year, 1, 1), 
                new DateTime(DateTime.Now.Year + 1, 1, 1));
            
            foreach (var item in cashFlowItemsYtd.Where(c => c.Inflow > 0))
            {
                InflowsYtd.Add(item.AccountPath, item.Inflow);
            }
            foreach (var item in cashFlowItemsYtd.Where(c => c.Outflow > 0))
            {
                OutflowsYtd.Add(item.AccountPath, item.Outflow);
            }
        }
    }

}
