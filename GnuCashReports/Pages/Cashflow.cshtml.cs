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
            if (String.IsNullOrWhiteSpace(_appSettings.CashFlowSettings?.ParentCashAccount))
                throw new Exception("Missing configuration \"ParentCashAccount\"");
            List<CashFlowItem> cashFlowItemsYtd  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, 
                new DateTime(DateTime.Now.Year, 1, 1), 
                new DateTime(DateTime.Now.Year + 1, 1, 1));
            
            foreach (var item in cashFlowItemsYtd.Where(c => c.Inflow > 0))
            {
                bool isOverridden = false;
                if (_appSettings.CashFlowSettings.InflowCategories != null)
                {
                    foreach (var category in _appSettings.CashFlowSettings.InflowCategories)
                    {
                        foreach (var pattern in category.Value){
                            if (item.AccountPath.StartsWith(pattern))
                            {
                                decimal total = InflowsYtd.ContainsKey(category.Key) ? InflowsYtd[category.Key] : 0;
                                total += item.Inflow;
                                InflowsYtd.Remove(category.Key);
                                InflowsYtd.Add(category.Key, total);
                                isOverridden = true;
                            }
                        }
                    }
                }
                if (!isOverridden)
                    InflowsYtd.Add(item.AccountPath, item.Inflow);
            }
            foreach (var item in cashFlowItemsYtd.Where(c => c.Outflow > 0))
            {
                bool isOverridden = false;
                if (_appSettings.CashFlowSettings.OutflowCategories != null)
                {
                    foreach (var category in _appSettings.CashFlowSettings.OutflowCategories)
                    {
                        foreach (var pattern in category.Value){
                            if (item.AccountPath.StartsWith(pattern))
                            {
                                decimal total = OutflowsYtd.ContainsKey(category.Key) ? OutflowsYtd[category.Key] : 0;
                                total += item.Outflow;
                                OutflowsYtd.Remove(category.Key);
                                OutflowsYtd.Add(category.Key, total);
                                isOverridden = true;
                            }
                        }
                    }
                }
                if (!isOverridden)
                    OutflowsYtd.Add(item.AccountPath, item.Outflow);
            }
        }
    }

}
