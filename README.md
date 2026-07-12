# GnuCashReports
Provides various financial reports based on exising GnuCash database. Each report is meant to be used within Home Assistant as a "webpage"/(iframe) card, so they're designed to fit well on a small screen. However, you can also use this as a standalone web application that you can view in any browser/screen. 

# Installation (Docker)
The app can be run as a standalone container in [Docker](https://hub.docker.com/r/dimaser/gnucashreports) or as a Docker Compose stack (recommended). An example Docker compose file is below. 
`[/path/to/your/gnucash-sqlite-db]` should be the host path to your GnuCash database. Your database must be in Sqlite format.
`[/path/to/your/appsettings.json]` host path to your appsettings.json configuration file. The documentation and example config file is provided below.
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
Build and start the project with `docker compose up -d`. The web interface should be available on http://[your-docker-host-name]:8085. If you get any errors, check container logs and refer to configuration documentation below to resolve. For HTTPS (required if you want to embed reports in HomeAssistant), use a reverse proxy.

# Configuration
The application is configured via `appsettings.json` file. Rename the provided `appsettings.Sample.json` to `appsettings.json` and modify as needed.

`Logging` and `AllowedHosts` sections are standard .NET configs, can be left at default. All app-specifc configuration is under `AppSettings` section.

## GnuCashDbConnectionString [required]
Connection string to your GnuCash database (must be a Sqlite db). If running in Docker, leave this as is and instead, map the path in your docker-compose file.

## RootAccountName [required]
This is the name of your root account in the database. This should normally be set to `Root Account` unless you changed it in your database.

## ClosingEntriesPattern [optional]
If you are using GnuCash's "Close Book" feature, you'll need to specify your closing entries description pattern to ignore those transactions, otherwise the reports will be inaccurate. Used as argument for `not like` SQL condition, so if all your closing entries start with `Closing`, you'd specify `Closing%`. Remove this paramater or set it to empty string if your database does not have closing entries

## TargetSavingsPercentage [optional]
This is used in the "Available to Spend" report to show amount available to spend this year based on your desired savings percentage rate. For example, set this to 50 if you're targeting to save 50% of you income. If not provided, it will default to 0.

## NumYearsAvailable [required]
Must be between 2 and 100. Number of years (starting from the current year and going backwards) that are available in the dropdowns used in certain reports for comparison.

## ExpenseAccountEmojis [optional]
These are used in your expense reports to make them easier to see at a glance and to shorten account names to better fit on the expense chart. Used in the expense report to prepend account names and in the expense chart instead of account names to save space. If omitted for a particular expense account, account name will be used instead. This section is optional.

## NetWorthYearsToDisplay [required]
Number of years to display on net worth chart (including current year to date).

## InvestmentSettings [optional]
These are required for the "Investments" report. If you don't track investments in GnuCash, you can omit this whole section. However, if any of these are configured, the rest are required as well.

### InvestmentParentAccounts
Full colon-delimited paths to all your parent investement accounts in GnuCash/accounts you want to treat as "Investments" and be included in the "Asset Allocation" report. For example: `Assets:Investments:Brokerage`. All child accounts are included automatically, so only parents are required. At least one is required, but you can have as many as needed.

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

## NetChangeInterval, NetChangeInterval2
This is a time interval to look back to determine the change in overall investment balance since the beginning of the interval. Only two intervals are supported at this time. This must be one of the following: "-[n] day(s)|month(s)|year(s)" or "start of day|month|year". For example: "-1 day", "-6 months", "start of year", etc. Note that this won't show your investments' rate of return, but the overall balance change, including contributions/withdrawals and investment growth/losses.

## TimeOfDayCutoff
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