using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace GnuCashReports.Pages
{

    public class ExpensesChartModel : PageModel
    {
        private readonly DatabaseService _plService;
        public IEnumerable<string> Labels { get; set; } = new List<string>();
        //public IEnumerable<string> LabelsPrevYear { get; set; } = new List<string>();
        public List<ProfitLossItem> ProfitLossData { get; set; } = new();
        public Dictionary<string, string>? ExpenseAccountEmojis { get; set; }
        public ExpensesChartModel(DatabaseService plService, IOptions<AppSettings> appSettings)
        {
            _plService = plService;
            ExpenseAccountEmojis = appSettings.Value.ExpenseAccountEmojis;
        }

        public async Task OnGetAsync()
        {
            ProfitLossData = await _plService.GetLevel2ProfitLossAsync();
            var tempLabels = ProfitLossData.Where(x => x.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE && x.TotalAmountYTD >= 0).OrderBy(x => x.AccountName);
            //var tempLabelsPrevYear = ProfitLossData.Where(x => x.AccountType == AppSettings.ACCOUNT_TYPE_EXPENSE && x.TotalAmountPrevYear >= 0).OrderBy(x => x.AccountName);
            if (ExpenseAccountEmojis != null)
            {
                Labels = tempLabels.Select(i => ExpenseAccountEmojis.ContainsKey(i.AccountName) ? ExpenseAccountEmojis[i.AccountName] : i.AccountName);
                //LabelsPrevYear = tempLabelsPrevYear.Select(i => ExpenseAccountEmojis.ContainsKey(i.AccountName) ? ExpenseAccountEmojis[i.AccountName] : i.AccountName);

            }
            else
            {
                Labels = tempLabels.Select(i => i.AccountName);
                //LabelsPrevYear = tempLabelsPrevYear.Select(i => i.AccountName);
            }
            
        }
    }

}
