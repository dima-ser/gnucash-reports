using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace GnuCashReports.Models
{
    /// <summary>
    /// Used to store application settings and deserialize them from appsettings.json
    /// </summary>
    public class AppSettings
    {
        //public static decimal SQLITE_FLOATING_POINT_MARGIN = 0.0001M;
        public static int MIN_NUM_YEARS_AVAILABLE = 2;
        public static int DEFAULT_NET_WORTH_YEARS_MAX = 10;
        public required string GnuCashDbConnectionString { get; set; }
        public required string RootAccountName { get; set; } = "Root Account";
        public string? ClosingEntriesPattern { get; set; }
        public decimal TargetSavingsPercentage { get; set; } = 50;
        public int NumYearsAvailable {get; set; }
        public bool IncludeFutureTransactionsInPL { get; set; } = true;
        public List<string>? ExcludedIncomeAccountsFromSavingRate { get; set; }
        public Dictionary<string, string>? ExpenseAccountEmojis { get; set; }

        public int NetWorthMaxYears { get; set; } 
        public List<string>? DashboardLayout {get; set;}
        public InvestmentSettings? InvestmentSettings { get; set; }

        public FISettings? FISettings { get; set; }

        public CashFlowSettings? CashFlowSettings {get; set;}
        
        }
    public class CashFlowSettings
    {
        public string? ParentCashAccount {get; set; }
        public Dictionary<string, List<string>>? InflowCategories { get; set; }
        public Dictionary<string, List<string>>? OutflowCategories { get; set; }
    }
    public class InvestmentSettings
    {
        public required List<string> InvestmentParentAccounts { get; set;}
        public required List<AssetAllocation> InvestmentAssetAllocations { get; set; }
        public required AssetAllocation TargetAssetAllocation { get; set; }
        public required List<string>? ExcludedAccounts { get; set; }
        public decimal RebalanceRelativePercentage { get; set; }
        public required TimeSpan TimeOfDayCutoff { get; set; }
    }

    public class  FISettings
    {
        public required List<string> LiquidAssetParentAccounts { get; set; }
        public required decimal SafeWithdrawalRate { get; set; }

        public required int AverageExpensesYearsLookback { get; set; }
    }
    public class AppSettingsValidation : IValidateOptions<AppSettings>
    {
        public ValidateOptionsResult Validate(string? name, AppSettings settings)
        {
            if (settings == null)
            {
                return ValidateOptionsResult.Fail("AppSettings configuration is null.");
            }
            if (settings.GnuCashDbConnectionString == null)
            {
                return ValidateOptionsResult.Fail("Missing configuration: GnuCashDbConnectionString");
            }
            if (String.IsNullOrEmpty(settings.RootAccountName))
            {
                return ValidateOptionsResult.Fail("Missing configuration: RootAccountName");
            }
            if (settings.TargetSavingsPercentage < 0 || settings.TargetSavingsPercentage > 100)
                return ValidateOptionsResult.Fail("Invalid configuration: TargetSavingsPercentage must be between 0 and 100");
            //if (settings.NumYearsAvailable < 2 || settings.NumYearsAvailable > 100)
            //        return ValidateOptionsResult.Fail("Invalid configuration: NumYearsAvailable must be between 2 and 100");

            // if (settings.NetWorthMaxYears < 1)
            //     return ValidateOptionsResult.Fail("Invalid configuration: NetWorthYearsToDisplay must be positive");
            if (settings.InvestmentSettings != null)
            {

                if (settings.InvestmentSettings.InvestmentParentAccounts == null || settings.InvestmentSettings.InvestmentParentAccounts.Count < 1)
                {
                    return ValidateOptionsResult.Fail("Missing configuration: InvestmentParentAccounts (at least 1 is required)");
                }
                if (settings.InvestmentSettings.InvestmentAssetAllocations == null)
                {
                    return ValidateOptionsResult.Fail("Missing configuration: InvestmentAssetAllocations");
                }
                foreach (AssetAllocation item in settings.InvestmentSettings.InvestmentAssetAllocations)
                {
                    if (!item.PercentagesAddUpTo100())
                        return ValidateOptionsResult.Fail(item.Name + ": asset allocation percentages must up to 100");
                }
                if (settings.InvestmentSettings.TargetAssetAllocation == null)
                {
                    return ValidateOptionsResult.Fail("Missing configuration: TargetAssetAllocation");
                }
                if (!settings.InvestmentSettings.TargetAssetAllocation.PercentagesAddUpTo100())
                    return ValidateOptionsResult.Fail("Invalid configuration: TargetAssetAllocation: asset allocation percentages must up to 100");

                if (settings.InvestmentSettings.RebalanceRelativePercentage < 1 || settings.InvestmentSettings.RebalanceRelativePercentage > 100)
                    return ValidateOptionsResult.Fail("Invalid configuration: RebalanceRelativePercentage must be between 1 and 100");
            }
            if (settings.FISettings != null)
            {
                if (settings.FISettings.LiquidAssetParentAccounts == null || settings.FISettings.LiquidAssetParentAccounts.Count < 1)
                {
                    return ValidateOptionsResult.Fail("Missing configuration: FISettings.LiquidAssetParentAccounts (at least 1 is required)");
                }
                if (settings.FISettings.SafeWithdrawalRate <= 0 || settings.FISettings.SafeWithdrawalRate >= 1)
                    return ValidateOptionsResult.Fail("Invalid configuration: FISettings.SafeWithdrawalRate must be between 0 and 1");
                if (settings.FISettings.AverageExpensesYearsLookback <= 0)
                    return ValidateOptionsResult.Fail("Invalid configuration: FISettings.AverageExpensesYearsLookback must be positive");
            }


            return ValidateOptionsResult.Success;
        }

    }
}
