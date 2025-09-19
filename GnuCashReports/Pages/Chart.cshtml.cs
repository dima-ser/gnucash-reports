using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GnuCashReports.Models;
using GnuCashReports.Services;
using System.Reflection;

namespace GnuCashReports.Pages
{

    public class ChartModel : PageModel
    {
        private readonly DatabaseService _plService;

        public List<ProfitLossItem> ProfitLossData { get; set; } = new();
        public decimal percentSpentPrevYear, percentSavedPrevYear;
        public decimal percentSpentYTD, percentSavedYTD;

        public ChartModel(DatabaseService plService)
        {
            _plService = plService;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync();
            decimal spentPrevYear = ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE).Sum(i => i.TotalAmountPrevYear);
            decimal totalPrevYear = -ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_INCOME).Sum(i => i.TotalAmountPrevYear);
            percentSpentPrevYear = Math.Round((spentPrevYear / totalPrevYear) * 100, 1);
            percentSavedPrevYear = 100 - percentSpentPrevYear;
            decimal spentYTD = ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE).Sum(i => i.TotalAmountYTD);
            decimal totalYTD = -ProfitLossData.Where(i => i.AccountType == AppSettings.ACCOUNT_TYPE_INCOME).Sum(i => i.TotalAmountYTD);
            percentSpentYTD = Math.Round((spentYTD / totalYTD) * 100, 1);
            percentSavedYTD = 100 - percentSpentYTD;
        }
    }

}
