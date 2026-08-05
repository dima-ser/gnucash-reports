using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Rendering;
using Htmx;

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
        public int YearRight {get; set;} = DateTime.Now.Year;
        [BindProperty (SupportsGet = true)]
        public int YearLeft {get; set;} = DateTime.Now.Year - 1;
        public decimal StartBalanceLeft, StartBalanceRight;
        public decimal EndBalanceLeft, EndBalanceRight;
        public SelectList YearListRight, YearListLeft;
        
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
            for (int i = 0; i < _appSettings.NumYearsAvailable; i++)
            {
                years.Add((currentYear-i).ToString());
            }
            YearListRight = new SelectList(years, currentYear.ToString());
            YearListLeft = new SelectList(years, (currentYear-1).ToString());
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (String.IsNullOrWhiteSpace(_appSettings.CashFlowSettings?.ParentCashAccount))
                throw new Exception("Missing configuration \"ParentCashAccount\"");
            Dictionary<string, decimal> inflowsRight = new Dictionary<string, decimal>();
            Dictionary<string, decimal> outflowsRight = new Dictionary<string, decimal>();
            Dictionary<string, decimal> inflowsLeft = new Dictionary<string, decimal>();
            Dictionary<string, decimal> outflowsLeft = new Dictionary<string, decimal>();

            
            DateOnly currentDate = DateOnly.FromDateTime(DateTime.Now);
            int currentYear = currentDate.Year;
            DateOnly startDateLeft = new DateOnly(YearLeft, 1, 1);
            // only include YTD transactions for the current year
            DateOnly endDateLeft = YearLeft == currentYear ? currentDate : new DateOnly(YearLeft, 12, 31);
            List<CashFlowItem> cashFlowItemsLeft  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, startDateLeft, endDateLeft);
            inflowsLeft = RewriteCashflowCategories(
                cashFlowItemsLeft.Where(c => c.Inflow > 0).ToList(), 
                _appSettings.CashFlowSettings.InflowCategories, 
                CashFlowType.Inflow);
            outflowsLeft = RewriteCashflowCategories(
                cashFlowItemsLeft.Where(c => c.Outflow > 0).ToList(), 
                _appSettings.CashFlowSettings.OutflowCategories, 
                CashFlowType.Outflow);

            DateOnly startDateRight = new DateOnly(YearRight, 1, 1);
            DateOnly endDateRight = YearRight == currentYear ? currentDate : new DateOnly(YearRight, 12, 31);
            List<CashFlowItem> cashFlowItemsRight  = await _dbService.GetCashFlowStatement(
                _appSettings.CashFlowSettings.ParentCashAccount, startDateRight, endDateRight);
            inflowsRight = RewriteCashflowCategories(
                cashFlowItemsRight.Where(c => c.Inflow > 0).ToList(), 
                _appSettings.CashFlowSettings.InflowCategories, 
                CashFlowType.Inflow);
            outflowsRight = RewriteCashflowCategories(
                cashFlowItemsRight.Where(c => c.Outflow > 0).ToList(), 
                _appSettings.CashFlowSettings.OutflowCategories, 
                CashFlowType.Outflow);

            Inflows = ThreeColumnReportItem.CombineItems(inflowsLeft, inflowsRight);
            Outflows = ThreeColumnReportItem.CombineItems(outflowsLeft, outflowsRight);
            
            string cashAccountGuid = await _dbService.GetAccountGuid(_appSettings.CashFlowSettings.ParentCashAccount);
            // since start date is inclusive, need to subtract a day to get the cash balance as of 12/31 the previous year
            StartBalanceLeft = (await _dbService.GetBalanceSheetAsync(
                new List<string> {cashAccountGuid}, startDateLeft.AddDays(-1))).Sum(i=>i.Amount);
            EndBalanceLeft = (await _dbService.GetBalanceSheetAsync(
                new List<string> {cashAccountGuid}, endDateLeft)).Sum(i=>i.Amount);
            StartBalanceRight = (await _dbService.GetBalanceSheetAsync(
                new List<string> {cashAccountGuid}, startDateRight.AddDays(-1))).Sum(i=>i.Amount);
            EndBalanceRight = (await _dbService.GetBalanceSheetAsync(
                new List<string> {cashAccountGuid}, endDateRight)).Sum(i=>i.Amount);

            if (!Request.IsHtmx())
                return Page();
            return Partial("CashflowPartial", this);
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
