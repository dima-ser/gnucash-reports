# GnuCashReports
Provides various financial reports based on exising GnuCash database. Each report is meant to be used within Home Assistant as a "webpage"/(iframe) card, so they're designed to fit well on a small screen. However, you can also use this as a standalone web application that you can view in any browser/screen. 

# Installation (Docker)
The app can be run as a standalone container in [Docker](https://hub.docker.com/r/dimaser/gnucashreports) or as a Docker Compose stack (recommended). An example Docker compose file is below. 

`[/path/to/your/gnucash-sqlite-db]` should be the host path to your GnuCash database. Your database must be in Sqlite format.

`[/path/to/your/appsettings.json]` should be the host path to your appsettings.json configuration file. The documentation and example config file is provided below.
You can also replace port 8085 with a port of your choice if 8085 is not available on your host.

```yml
services:
  gnucashreports:
    image: dimaser/gnucashreports:latest
    container_name: gnucashreports
    ports:
      - 8085:8080
    volumes:
      - [/path/to/your/gnucash-sqlite-db]:/app/sqlite/gnucash.sqlite:ro
      - [/path/to/your/appsettings.json]:/app/appsettings.json
    environment:
      - TZ=[Your/Timezone]
```
Build and start the project with `docker compose up -d`. The web interface should be available on http://[your-docker-host-name]:8085. If you get any errors, check container logs and refer to configuration documentation below to resolve. For HTTPS (required if you want to embed reports in HomeAssistant), use a reverse proxy with TLS certificate. The app provides no authentication options. Use on internal network only. If you want to access externally, secure behind a VPN or reverse proxy with mTLS.

# Configuration
The application is configured via `appsettings.json` file. Rename the provided `appsettings.Sample.json` to `appsettings.json` and modify as needed.

`Logging` and `AllowedHosts` sections are standard .NET configs, can be left at default. All app-specifc configuration is under `AppSettings` section.

The only setting required for the app to run is `GnuCashDbConnectionString`. The rest are optional. The ones required for default reports have sensible default values. Some are only required for certain reports.

## GnuCashDbConnectionString [required]
Connection string to your GnuCash database (must be a Sqlite db). If running in Docker, set this to `Data Source=sqlite/gnucash.sqlite;Mode=ReadOnly` and map the path in your docker-compose file.

## ReportCurrency [optional]
(ISO_4217)[https://en.wikipedia.org/wiki/ISO_4217] currency code in which you want your reports to be calculated. Defaults to `USD` if not provided. Must be a valid currency in GnuCash. Will show zero balances if there is no price/exchange configured in GnuCash between acccount currency and report currency. The price used will always be the last price up through the report date. For P&L reports with both start and end dates, the last price before the end date will be used. Note that the cash flow report is currently not supporting this, so if you have accounts with multiple currencies as your "cash" accounts, the cash flow report will be inaccurate.

## Locale [optional]
The country and region code as per https://learn.microsoft.com/en-us/globalization/locale/standard-locale-names. Affects number formatting and currency symbols shown on reports. Default value: `en-US`.

## TargetSavingsPercentage [optional]
This is used in the "Available to Spend" report to show amount available to spend this year based on your desired savings percentage rate. For example, set this to 50 if you're targeting to save 50% of you income. If not provided, it will default to 50.

## NumYearsAvailable [optional]
Number of years (starting from the current year and going backwards) that are available in the dropdowns used in certain reports for comparison. Minimum valid value is 2 as most reports require at least 2 different years to compare. If omitted, will default to the number of years worth of data in your database or 2, whichever is greater.

## ExcludedIncomeAccountsFromSavingRate [optional]
Names of income accounts to exclude from "Savings Rate" report. Useful when you have income (such as dividends and capital gains) inside retirement accounts that you don't want to count towards your income for the purposes of calculating savings rate.

## IncludeFutureTransactionsInPL [optional]
If set to `true`, all P&L reports (Profit & Loss, Available to Spend and Savings Rate) will include future-dated transaction for the current year (up through the end of year). If set to `false`, these reports will only include transactions to date for the current year. Default: `true`.

## ExpenseAccountEmojis [optional]
These are used in your P&L report to prepend emojis to account names to make them easier to recognize at a glance. This section is optional.

## NetWorthMaxYears [optional]
Number of years to display on net worth chart (including current year to date). Default value: 2 or the number of years worth of data in your database (up to the maximum of 10), whichever is greater.

## DashboardLayout [optional]
An array of strings that defines which reports are shown on the home page and in which order. Report identifiers are the same as their relative URL endpoints (`balancesheet` for Balance Sheet, etc). Refer to "Available Reports" below for list of all reports and their identifiers. Reports are rendered top-to-bottom, then left-to-right (like newspaper columns). The number of columns adjusts automatically based on the screen width (maximum of 3 columns). If omitted, defaults to the following layout with common reports only: 
```json
[`profitloss`, `balancesheet`, `networthchart`, `dbstats`]
```

## InvestmentSettings [optional]
These are required for the "Investments" report. If you don't track investments in GnuCash, you can omit this whole section. However, if any of these are configured, the rest are required as well.

### InvestmentParentAccounts
Full colon-delimited paths to all your parent investement accounts in GnuCash/accounts you want to treat as "Investments" and be included in the "Investments" report. For example: `Assets:Investments:Brokerage`. All child accounts are included automatically, so only parents are required. At least one is required, but you can have as many as needed. Note that account names are case-sensitive.

### InvestmentAssetAllocations
This section is used to let the application know what portion of each investment account is US Stock, International Stock and Bonds. All your investment accounts (including children of `InvestmentParentAccounts`) need to be configured here. Use account name for the "Name" property (can be the same as stock/fund ticker, but not necesserily). For example, if you hold VTSAX (which is 100% US Stock), you'd configure it like so (assuming account name in GnuCash is also "VTSAX"):
```json
 {
    "Name": "VTSAX",
    "US": 100,
    "INTNL": 0,
    "BND": 0
  }
```
This structure also allows for accounts containing multiple asset classes, such as target date funds (you'll need to know your TDF's current asset distribution and update it as it changes over time):
```json
{
    "Name": "VFFVX",
    "US": 54,
    "INTNL": 36,
    "BND": 10
}
```
The total percentage for each account must add up to 100 (the app will throw an error if it doesn't). If you have multiple accounts with the same name and allocation, only one config entry is required.

### TargetAssetAllocation
Use this to set your overall desired asset allocation between all investment accounts. This will be used to determine whether you need to rebalance. The "Name" property can be anything here. Just like with individual accounts, the total percentage must add up to 100.

### ExcludedAccounts
List account names (without the full path) you want to exclude from being counted as investments. Useful when you have temporary/sweep accounts under your investment root accounts. Note: this only excludes accounts themselves, not children.

### RebalanceRelativePercentage
This is the **relative** percentage each asset class can deviate from your target allocation before the report will tell you to rebalance (see https://www.bogleheads.org/wiki/Rebalancing). For example, set this to 20 if you want to rebalance any time an asset class deviates from target by 20% relative to its target. Only relative percentage is supported at this time. The report will also tell you how far off you are in absolute percentage as well as dollar amounts, so you can still use the report to see if you need to rebalance even if you use a different rebalancing strategy.

### TimeOfDayCutoff
The investment balance is assumed to only update once a day. This feature also relies on the assumption you update prices in GnuCash at the same time every day (either manually or via a script). In order to accurately calculate daily balance changes, the program needs to know when your prices are updated. Set this to a time of day right after your prices are updated so that the program can tell whether it's looking at yesterday's or today's prices. If you hold mutual funds that update prices daily, you'll want to update your prices after the stock market closes for the day. For example, if you update your prices at 3:50pm (15:50) daily, set this to "16:00" so that the program can start showing the net daily balance change at 4pm. 

## FISettings [optional]
These are used for FI (Financial Independence) Report which shows your progress towards financial independence. You can remove this section if you won't use the FI Report. However, if this section is present, all sub-settings will be required.

### LiquidAssetParentAccounts
List of colon-delimited full account paths that you want included into your "total liquid assets" number. These would typically be all your cash and investments accounts, excluding any fixed assets like house. For example, `Assets:Cash:Checking`, `Assets:Investments:Brokerage`.
### SafeWithdrawalRate
Percentage of your portfolio you intend to withdraw yearly once you're retired/financialy independent. The typical number for this is 0.04 (4%). This (in combination with `AverageExpensesYearsLookback`) is used to calculate how far along you are towards reaching the number when your annual expenses are 4% of your portfolio.
### AverageExpensesYearsLookback
Number of years to look back to determine your average annual expenses. This (in combinatin with `SafeWithdrawalRate`) will be used to calculate how far along you are towards your FIRE number.

## CashFlowSettings [optional]
These are used for the Cash Flow report. Can be omitted if you don't use this report.

### ParentCashAccount
Set this to full account path of whatever account you consider "cash". All child accounts will be also included automatically. For example, if you have `Assets:Cash` and `Assets:Cash:Wallet`, `Assets:Cash:Checking`, set this to `Assets:Cash`. This determines what will show on the cash flow statement as cash inflows and outflows.

### InflowCategories, OutflowCategories
If these are omitted, the cash flow report will simply list all accounts that the cash either flows into or out of, similar to GnuCash's built-in Cash Flow report. This can be very verbose if you have many accounts. Use this to group your accounts and re-label them in a way that makes sense to you. For example, if you have cash inflows from `Assets:Investments:Brokerage` because you sold some investments, you can configure these transactions to be summed up and shown as `Sale of investments`:

```json
"InflowCategories" : {
            "Sale of investments" : ["Assets:Investments"]
}
```
The account path is matched with `StartsWith()` so all cash inflow transactions involving any accounts that are under `Assets:Investments` will be totaled up and included in the `Sale of investments` category and listed as one row on the report instead of separate rows for each account.

# Available Reports
List of all reports is below. Note that some of these will only work if you provide the necessary configuration for them.

| Report name | Identifier/endpoint | Description | Requires additional configuration? |
| - | - | - | - |
| Profit & Loss / Income Statement | `profitloss` | Standard P&L accounting report. Each period is one calendar year. Available periods are current year and previous years up to `NumYearsAvailable`  | No |
| Savings Rate | `savingsrate` | Shows the ratio of how much you save vs spend on yearly basis. Based on P&L report. | No |
| Available to Spend | `availabletospend` | Shows how much you can spend in the current year to meet your desired savings rate. Based on P&L report. | No |
| Balance Sheet | `balancesheet` | Standard balance sheet report. Available dates are today and December 31 of each previous year up to `NumYearsAvailable` | No |
| Net Worth Chart | `networthchart` | Shows how your net worth has changed over time (once per year as of the end of each year). Based on balance sheet. Number of years shown is configured in `NetWorthMaxYears` | No |
| Database Stats | `dbstats` | Not a financial report, but a set of statistics about your database, such as when it was last updated, latest price date, number of transactions, etc. | No |
| Cash Flow | `cashflow` | Shows your cash movement on annual basis. Similar to GnuCash's "Cash Flow" report, but simplified to override account names with user-defined category for each cash movement. To configure, see `CashFlowSettings` configuration section | Yes |
| FI Report | `fireport` | Financial Independence readiness report. Opinionated metric showing how close you are to being "financially independent". Defined as a percentage of your current liquid assets (including cash, stocks and retirement accounts) to the total amount required to be finanically independent. The total amount required is defined as how much you need in order for your average annual expenses to be at or below your desired annual [safe withdrawal rate](https://www.bogleheads.org/wiki/Safe_withdrawal_rates). To configure, see `FISettings` configuration section | Yes |
| Investments | `investments` | Your overall investment portfolio report. Shows the total portfolio amount and how much it changed over the past day/month/6 months/YTD or a year. Note that the change is not your rate of return but a simple balance change, including your contributions/withdrawals as well as investment gains/losses. The report also breaks down your portoflio into how much you have in US Stock, International Stock and Bonds and tells you whether you need to rebalance per your desired asset allocation. Note that only these 3 asset classes are supported so if you would like to track more than these 3 asset classes, this report will not work for you. To configure, see `InvestmentsSettings` configuration section | Yes |