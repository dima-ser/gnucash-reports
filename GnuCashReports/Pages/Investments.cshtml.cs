using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Reflection;

namespace GnuCashReports.Pages
{

    public class InvestmentsModel : PageModel
    {
        private readonly DatabaseService _dbService;
        private readonly AppSettings _appSettings;
        public AssetAllocation TargetAssetAllocation { get; set; }
        public AssetAllocation? ActualAssetAllocation { get; set; } 
        public List<AssetAllocation> InvestmentAssetAllocations { get; set; }
        public List<AssetAllocationItem> AssetAllocationData { get; set; } = new();
        public List<string>? ExcludedAccounts { get; set; }
        public decimal TargetAmountUS, TargetAmountIntnl, TargetAmountBonds;
        public decimal ActualAmountUS, ActualAmountIntnl, ActualAmountBonds;
        public decimal TotalAmount, TotalPreviousAmount, NetChange, NetPercentageChange;
        public decimal RebalanceRelativePercentage;
        public bool RebalanceUS, RebalanceIntnl, RebalanceBonds;
        //public string NetChangeInterval, NetChangeInterval2, NetChangeIntervalUserFriendly, NetChangeIntervalUserFriendly2;
        [BindProperty (SupportsGet = true)]
        public DateOnly Date {get; set;} = DateOnly.FromDateTime(DateTime.Now);
        public InvestmentsModel(DatabaseService dbService, IOptions<AppSettings> appSettings)
        {
            if (appSettings.Value.InvestmentSettings == null)
                throw new Exception("Missing configuration: " + typeof(InvestmentSettings).ToString());
            _dbService = dbService;
            _appSettings = appSettings.Value;
            TargetAssetAllocation = _appSettings.InvestmentSettings.TargetAssetAllocation;
            InvestmentAssetAllocations = _appSettings.InvestmentSettings.InvestmentAssetAllocations;
            ExcludedAccounts = _appSettings.InvestmentSettings.ExcludedAccounts;
            RebalanceRelativePercentage = _appSettings.InvestmentSettings.RebalanceRelativePercentage;
            //NetChangeInterval = _appSettings.InvestmentSettings!.NetChangeInterval;
            //NetChangeInterval2 = _appSettings.InvestmentSettings!.NetChangeInterval2;

            //UpdateFriendlyNetChangeInterval(NetChangeInterval, out NetChangeIntervalUserFriendly);
            //UpdateFriendlyNetChangeInterval(NetChangeInterval2, out NetChangeIntervalUserFriendly2);

            // if the current time is before the cutoff, use yesterday's date
            if (DateTime.Now.TimeOfDay < _appSettings.InvestmentSettings!.TimeOfDayCutoff)
                Date = Date.AddDays(-1);
        }

        // public void UpdateFriendlyNetChangeInterval(string netChangeInterval, out string netChangeIntervalUserFriendly)
        // {
        //     netChangeIntervalUserFriendly = netChangeInterval;
        //     if (netChangeInterval.StartsWith('-'))
        //     {
        //         string[] parts = netChangeInterval.Split(' ');
        //         if (parts.Length == 2 && int.TryParse(parts[0], out int value))
        //         {
        //             value = Math.Abs(value);
        //             if (parts[1] == "day" || parts[1] == "days")
        //                 netChangeIntervalUserFriendly = value + " day" + (value > 1 ? "s" : "") + " ago";
        //             else if (parts[1] == "month" || parts[1] == "months")
        //                 netChangeIntervalUserFriendly = value + " month" + (value > 1 ? "s" : "") + " ago";
        //             else if (parts[1] == "year" || parts[1] == "years")
        //                 netChangeIntervalUserFriendly = value + " year" + (value > 1 ? "s" : "") + " ago";
        //         }
        //     }
        // }

        public async Task OnGetAsync()
        {
            List<string> investmentParentAccounts = _appSettings.InvestmentSettings!.InvestmentParentAccounts;
            List<string> investmentParentGuids = new List<string>();
            foreach (string accountName in investmentParentAccounts)
            {
                investmentParentGuids.Add(await _dbService.GetAccountGuid(accountName));
            }
            List<ReportItem> investmentData = await _dbService.GetInvestmentsAsync(investmentParentGuids, Date);

            foreach (var investmentItem in investmentData)
            {
                if (InvestmentAssetAllocations.Where(i => i.Name == investmentItem.AccountName).Count() > 0)
                {
                    var assetAllocationItem = new AssetAllocationItem(investmentItem, InvestmentAssetAllocations.Where(i => i.Name == investmentItem.AccountName).First());
                    AssetAllocationData.Add(assetAllocationItem);
                }
                // we only need asset allocations for accounts with current balance over 0 as we don't track previous asset allocations
                // also exclude accounts specified in the ExcludedAccounts configuration
                // don't think we need this anymore, remove?
                else if (investmentItem.Amount < AppSettings.SQLITE_FLOATING_POINT_MARGIN || (ExcludedAccounts != null && ExcludedAccounts.Contains(investmentItem.AccountName)))
                {
                    var assetAllocationItem = new AssetAllocationItem(investmentItem, new AssetAllocation("Dummy", 0, 0, 0));
                    AssetAllocationData.Add(assetAllocationItem);
                }
                else
                    throw new Exception("No asset allocation configuration found for \"" + investmentItem.AccountName + "\". " +
                        "Add an asset allocation for this account under \"InvestmentAssetAllocations\" or add it to \"ExcludedAccounts\" to ignore it.");
            }

            TargetAmountUS = AssetAllocationData.Sum(i => i.InvestmentItem.Amount) * (TargetAssetAllocation.US / 100);
            TargetAmountIntnl = AssetAllocationData.Sum(i => i.InvestmentItem.Amount) * (TargetAssetAllocation.INTNL / 100);
            TargetAmountBonds = AssetAllocationData.Sum(i => i.InvestmentItem.Amount) * (TargetAssetAllocation.BND / 100);
            ActualAmountUS = AssetAllocationData.Sum(i => i.USAmount);
            ActualAmountIntnl = AssetAllocationData.Sum(i => i.IntnlAmount);
            ActualAmountBonds = AssetAllocationData.Sum(i => i.BondAmount);
            TotalAmount = AssetAllocationData.Sum(i => i.InvestmentItem.Amount);

            List<ReportItem> prevBalanceSheet = await _dbService.GetBalanceSheetAsync(investmentParentGuids, Date.AddDays(-1));
            TotalPreviousAmount = prevBalanceSheet.Sum(i=>i.Amount);
            NetChange = TotalAmount - TotalPreviousAmount;
            if (TotalPreviousAmount != 0)
                NetPercentageChange = NetChange / TotalPreviousAmount;
            else
                NetPercentageChange = Decimal.MaxValue;

            // TotalPreviousAmount2 = AssetAllocationData.Sum(i => i.InvestmentItem.PreviousBalance2);
            // NetChange2 = TotalAmount - TotalPreviousAmount2;
            // if (TotalPreviousAmount2 != 0)
            //     NetPercentageChange2 = NetChange2 / TotalPreviousAmount2;
            // else
            //     NetPercentageChange2 = Decimal.MaxValue;

            ActualAssetAllocation = new AssetAllocation("Actual Asset Allocation", ActualAmountUS / TotalAmount * 100,
                    ActualAmountIntnl / TotalAmount * 100, ActualAmountBonds / TotalAmount * 100);
            RebalanceUS = Math.Abs((ActualAmountUS - TargetAmountUS) / TargetAmountUS * 100) >= RebalanceRelativePercentage;
            RebalanceIntnl = Math.Abs((ActualAmountIntnl - TargetAmountIntnl) / TargetAmountIntnl * 100) >= RebalanceRelativePercentage;
            RebalanceBonds = Math.Abs((ActualAmountBonds - TargetAmountBonds) / TargetAmountBonds * 100) >= RebalanceRelativePercentage;

            
        }
    }

}
