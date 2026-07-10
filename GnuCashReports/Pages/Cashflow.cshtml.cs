using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{

    public class CashflowModel : PageModel
    {
        public enum CashFlowType  {Inflow, Outflow}
        private readonly DatabaseService _dbService;

        private readonly AppSettings _appSettings;
        public Dictionary<string, decimal> Inflows1 = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> Outflows1 = new Dictionary<string, decimal>();

        public CashflowModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            if (String.IsNullOrWhiteSpace(_appSettings.CashFlowSettings?.ParentCashAccount))
                throw new Exception("Missing configuration \"ParentCashAccount\"");
            List<CashFlowItem> cashFlowItems1  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, 
                new DateTime(DateTime.Now.Year, 1, 1), 
                new DateTime(DateTime.Now.Year + 1, 1, 1));
            Inflows1 = RewriteCashflowCategories(
                cashFlowItems1.Where(c => c.Inflow > 0).ToList(), 
                _appSettings.CashFlowSettings.InflowCategories, 
                CashFlowType.Inflow);
            Outflows1 = RewriteCashflowCategories(
                cashFlowItems1.Where(c => c.Outflow > 0).ToList(), 
                _appSettings.CashFlowSettings.OutflowCategories, 
                CashFlowType.Outflow);



        }

        public Dictionary<string, decimal> RewriteCashflowCategories(
            List<CashFlowItem> inputList, 
            Dictionary<string, List<string>>? categories, 
            CashFlowType type)
        {
            Dictionary<string, decimal> cashFlows = new Dictionary<string, decimal>();
            foreach (var item in inputList)
            {
                bool isRewritten = false;
                if (categories != null)
                {
                    foreach (var category in categories)
                    {
                        foreach (var pattern in category.Value){
                            if (item.AccountPath.StartsWith(pattern))
                            {
                                decimal total = cashFlows.ContainsKey(category.Key) ? cashFlows[category.Key] : 0;
                                total += type == CashFlowType.Inflow ? item.Inflow : item.Outflow;
                                cashFlows.Remove(category.Key);
                                cashFlows.Add(category.Key, total);
                                isRewritten = true;
                            }
                        }
                    }
                }
                if (!isRewritten)
                    cashFlows.Add(item.AccountPath, type == CashFlowType.Inflow ? item.Inflow : item.Outflow);
            }
            return cashFlows;
        }
    }

}
