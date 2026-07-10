using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace GnuCashReports.Models
{
    /// <summary>
    /// Used to store application settings and deserialize them from appsettings.json
    /// </summary>
    public class AppSettings
    {
        // These are constants for account types in GnuCash database
        public static string ACCOUNT_TYPE_INCOME = "INCOME"; 
        public static string ACCOUNT_TYPE_EXPENSE = "EXPENSE";
        public static string ACCOUNT_TYPE_ASSET = "ASSET";
        public static string ACCOUNT_TYPE_LIABILITY = "LIABILITY";
        public static decimal SQLITE_FLOATING_POINT_MARGIN = 0.0001M;

        public static readonly Regex SqliteModifierValidator = new Regex(
            @"^(-\d+\s(?:day|days|month|months|year|years)|start\s+of\s+(day|month|year))$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        public required string GnuCashDbConnectionString { get; set; }
        public required string RootAccountName { get; set; }
        public string? ClosingEntriesPattern { get; set; }
        public decimal TargetSavingsPercentage { get; set; }
        public List<string>? ExcludedIncomeAccountsFromSavingRate { get; set; }
        public Dictionary<string, string>? ExpenseAccountEmojis { get; set; }

        public int NetWorthYearsToDisplay { get; set; }
        public InvestmentSettings? InvestmentSettings { get; set; }

        public FISettings? FISettings { get; set; }
        public string? ParentCashAccount {get; set; }
        }

    public class InvestmentSettings
    {
        public required List<string> InvestmentParentAccounts { get; set;}
        public required List<AssetAllocation> InvestmentAssetAllocations { get; set; }
        public required AssetAllocation TargetAssetAllocation { get; set; }
        public required List<string>? ExcludedAccounts { get; set; }
        public decimal RebalanceRelativePercentage { get; set; }
        public required string NetChangeInterval { get; set; }
        public required string NetChangeInterval2 { get; set; }
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
                return ValidateOptionsResult.Fail("TargetSavingsPercentage must be between 0 and 100");


            if (settings.NetWorthYearsToDisplay < 1)
                return ValidateOptionsResult.Fail("NetWorthYearsToDisplay must be positive");
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
                    return ValidateOptionsResult.Fail("TargetAssetAllocation: asset allocation percentages must up to 100");

                if (settings.InvestmentSettings.RebalanceRelativePercentage < 1 || settings.InvestmentSettings.RebalanceRelativePercentage > 100)
                    return ValidateOptionsResult.Fail("RebalanceRelativePercentage must be between 1 and 100");
                if (settings.InvestmentSettings.NetChangeInterval == null || settings.InvestmentSettings.NetChangeInterval2 == null)
                    return ValidateOptionsResult.Fail("Missing configuration: NetChangeInterval or NetChangeInterval2");
                if (!AppSettings.SqliteModifierValidator.IsMatch(settings.InvestmentSettings.NetChangeInterval) || !AppSettings.SqliteModifierValidator.IsMatch(settings.InvestmentSettings.NetChangeInterval2))
                    return ValidateOptionsResult.Fail("Invalid configuration: NetChangeInterval. Must be one of the following: \"-[n] day(s)|month(s)|year(s)\" or \"start of day|month|year\"");

            }
            if (settings.FISettings != null)
            {
                if (settings.FISettings.LiquidAssetParentAccounts == null || settings.FISettings.LiquidAssetParentAccounts.Count < 1)
                {
                    return ValidateOptionsResult.Fail("Missing configuration: FISettings.LiquidAssetParentAccounts (at least 1 is required)");
                }
                if (settings.FISettings.SafeWithdrawalRate <= 0 || settings.FISettings.SafeWithdrawalRate >= 1)
                    return ValidateOptionsResult.Fail("FISettings.SafeWithdrawalRate must be between 0 and 1");
                if (settings.FISettings.AverageExpensesYearsLookback <= 0)
                    return ValidateOptionsResult.Fail("FISettings.AverageExpensesYearsLookback must be positive");
            }

            return ValidateOptionsResult.Success;
        }

    }
}
