using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GnuCashReports.Pages
{

    public class CashflowModel : PageModel
    {
        public enum CashFlowType  {Inflow, Outflow}
        
        private readonly DatabaseService _dbService;

        private readonly AppSettings _appSettings;

        public List<ThreeColumnReportItem> Inflows = new List<ThreeColumnReportItem>();
        public List<ThreeColumnReportItem> Outflows = new List<ThreeColumnReportItem>();
        [BindProperty (SupportsGet = true)]
        public int Year {get; set;} = DateTime.Now.Year;
        public SelectList YearList, CompareList;
        [BindProperty (SupportsGet = true)]
        public int CompareYear {get; set;} = DateTime.Now.Year - 1;

        public decimal TotalInflowsYear1(){ return Inflows.Sum(i=>i.AmountRight);}
        public decimal TotalOutflowsYear1(){  return Outflows.Sum(i=>i.AmountRight);}
        public decimal TotalInflowsYear2(){ return Inflows.Sum(i=>i.AmountLeft); }
        public decimal TotalOutflowsYear2(){ return Outflows.Sum(i=>i.AmountLeft); }
        public decimal NetChangeYear1(){ return TotalInflowsYear1() - TotalOutflowsYear1(); }
        public decimal NetChangeYear2(){ return TotalInflowsYear2() - TotalOutflowsYear2(); }

        
        public CashflowModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            _dbService = dbService;
            _appSettings = appSettings.Value;
            if (_appSettings.CashFlowSettings == null)
                throw new Exception("Missing configuration \"CashFlowSettings\"");
            List<string> years = new List<string>();
            int currentYear = DateTime.Now.Year;
            for (int i = 0; i < _appSettings.CashFlowSettings.NumYearsAvailable; i++)
            {
                years.Add((currentYear-i).ToString());
            }
            YearList = new SelectList(years, currentYear.ToString());
            CompareList = new SelectList(years, (currentYear-1).ToString());
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

            Inflows = ThreeColumnReportItem.CombineItems(inflows1, inflows2);
            Outflows = ThreeColumnReportItem.CombineItems(outflows1, outflows2);

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
