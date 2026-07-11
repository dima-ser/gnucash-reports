using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;

namespace GnuCashReports.Pages
{
    public class CashFlowCombinedItem
    {
        public string Category { get; set; } = String.Empty;
        public decimal Amount1 { get; set; }
        public decimal Amount2 { get; set; }

        public bool Equals(CashFlowCombinedItem? other)
        {
            if (other is null) return false;
            return string.Equals(Category, other.Category, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as CashFlowCombinedItem);

        public override int GetHashCode() =>
            Category?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;
    }

    public class CashflowModel : PageModel
    {
        public enum CashFlowType  {Inflow, Outflow}
        
        private readonly DatabaseService _dbService;

        private readonly AppSettings _appSettings;

        public List<CashFlowCombinedItem> Inflows = new List<CashFlowCombinedItem>();
        public List<CashFlowCombinedItem> Outflows = new List<CashFlowCombinedItem>();
        [BindProperty (SupportsGet = true)]
        public int Year {get; set;} = DateTime.Now.Year;
        [BindProperty (SupportsGet = true)]
        public int CompareYear {get; set;} = DateTime.Now.Year - 1;

        public decimal TotalInflowsYear1(){ return Inflows.Sum(i=>i.Amount1);}
        public decimal TotalOutflowsYear1(){  return Outflows.Sum(i=>i.Amount1);}
        public decimal TotalInflowsYear2(){ return Inflows.Sum(i=>i.Amount2); }
        public decimal TotalOutflowsYear2(){ return Outflows.Sum(i=>i.Amount2); }
        public decimal NetChangeYear1(){ return TotalInflowsYear1() - TotalOutflowsYear1(); }
        public decimal NetChangeYear2(){ return TotalInflowsYear2() - TotalOutflowsYear2(); }
        public CashflowModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
        }

        public async Task OnGetAsync()
        {
            if (String.IsNullOrWhiteSpace(_appSettings.CashFlowSettings?.ParentCashAccount))
                throw new Exception("Missing configuration \"ParentCashAccount\"");
            Dictionary<string, decimal> inflows1 = new Dictionary<string, decimal>();
            Dictionary<string, decimal> outflows1 = new Dictionary<string, decimal>();
            Dictionary<string, decimal> inflows2 = new Dictionary<string, decimal>();
            Dictionary<string, decimal> outflows2 = new Dictionary<string, decimal>();
            List<CashFlowItem> cashFlowItems1  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, 
                new DateTime(Year, 1, 1), 
                new DateTime(Year + 1, 1, 1));
            inflows1 = RewriteCashflowCategories(
                cashFlowItems1.Where(c => c.Inflow > 0).ToList(), 
                _appSettings.CashFlowSettings.InflowCategories, 
                CashFlowType.Inflow);
            outflows1 = RewriteCashflowCategories(
                cashFlowItems1.Where(c => c.Outflow > 0).ToList(), 
                _appSettings.CashFlowSettings.OutflowCategories, 
                CashFlowType.Outflow);
            List<CashFlowItem> cashFlowItems2  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, 
                new DateTime(CompareYear, 1, 1), 
                new DateTime(CompareYear + 1, 1, 1));
            inflows2 = RewriteCashflowCategories(
                cashFlowItems2.Where(c => c.Inflow > 0).ToList(), 
                _appSettings.CashFlowSettings.InflowCategories, 
                CashFlowType.Inflow);
            outflows2 = RewriteCashflowCategories(
                cashFlowItems2.Where(c => c.Outflow > 0).ToList(), 
                _appSettings.CashFlowSettings.OutflowCategories, 
                CashFlowType.Outflow);

            Inflows = CombineCashFlows(inflows1, inflows2);
            Outflows = CombineCashFlows(outflows1, outflows2);

        }

        public List<CashFlowCombinedItem> CombineCashFlows(Dictionary<string, decimal> cashFlows1, Dictionary<string, decimal> cashFlows2)
        {
            List<CashFlowCombinedItem> combinedItems = new List<CashFlowCombinedItem>();
            foreach(var item1 in cashFlows1)
            {
                bool foundMatch = false;
                foreach(var item2 in cashFlows2)
                {
                    if (item1.Key == item2.Key)
                    {
                        foundMatch = true;
                        combinedItems.Add(new CashFlowCombinedItem{ Category = item1.Key, Amount1 = item1.Value, Amount2 = item2.Value});
                        break;
                    }
                }
                if (!foundMatch)
                    combinedItems.Add(new CashFlowCombinedItem{ Category = item1.Key, Amount1 = item1.Value, Amount2 = 0});
            }
            // add remaining items from cashFlows2 that didn't have a match
            foreach(var item2 in cashFlows2)
            {
                var combinedItem = new CashFlowCombinedItem { Category = item2.Key, Amount1 = 0, Amount2 = item2.Value };
                if (!combinedItems.Contains(combinedItem))
                {
                    combinedItems.Add(combinedItem);
                }
            }

            return combinedItems
                .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
