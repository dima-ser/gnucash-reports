# GnuCashReports
Provides various financial reports based on exising GnuCash database. Each report is meant to be used within Home Assistant as a "webpage"/(iframe) card, so they're designed to fit well on a small screen. However, you can also use this as a standalone web application that you can view in any browser/screen. 

# Installation (Docker)
The app can be run as a standalone container in Docker or as a Docker Compose stack (recommended). An example Docker compose file is below. 
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
The application is configured via `appsettings.json` file. Use the sample config file provided below to create your file and adjust as needed.

`Logging` and `AllowedHosts` sections are standard .NET configs, can be left at default. All app-specifc configuration is under `AppSettings` section.

## GnuCashDbConnectionString [required]
Connection string to your GnuCash database (must be a Sqlite db). If running in Docker, leave this as is and instead, map the path in your docker-compose file.

## IncomeRootAccountGuid, ExpenseRootAccountGuid, AssetRootAccountGuid, LiabilityRootAccountGuid [all required]
These are GUIDs of your root income, expense, asset and liability accounts in GnuCash database. You have to use a tool such as **Db Browser for SQLite** to find these for your database. These would all be directly under the ROOT account. This Sqlite query can be used to find these:
```sql
select * from accounts 
where parent_guid=(select guid from accounts where account_type='ROOT' and name='Root Account')
and account_type in ('ASSET','LIABILITY','INCOME','EXPENSE')
```

## ClosingEntriesPattern [optional]
If you are using GnuCash's "Close Book" feature, you'll need to specify your closing entries description pattern to ignore those transactions, otherwise the reports will be inaccurate. Used as argument for `not like` SQL condition, so if all your closing entries start with `Closing`, you'd specify `Closing%`. Remove this paramater if your database does not have closing entries

## TargetSavingsPercentage [optional]
This is used in the "Available to Spend" report to show amount available to spend this year based on your desired savings percentage rate. For example, set this to 50 if you're targeting to save 50% of you income. If not provided, it will default to 0.

## ExpenseAccountEmojis [optional]
These are used in your expense reports to make them easier to see at a glance and to shorten account names to better fit on the expense chart. Used in the expense report to prepend account names and in the expense chart instead of account names to save space. If omitted for a particular expense account, account name will be used instead. This section is optional.

## InvestmentSettings [optional]
These are required for "Asset Allocation" report. If you don't track investments in GnuCash, you can omit this whole section. However, if any of these are configured, the rest are required as well.

### InvestmentRootAccountGuids
These are GUIDs of all your root investement accounts in GnuCash/accounts you want to treat as "Investments" and be included in the "Asset Allocation" report. All child accounts are included automatically, so only parent GUIDs are required. At least one is required, but you can have as many as needed.

### InvestmentAssetAllocations
This section is used to let the application know what portion of each investment account is US Stock, International Stock and Bonds. All your investment accounts (including children of InvestmentRootAccountGuids) need to be configured here. Use account name for the "Name" property (can be the same as stock/fund ticker, but not necesserily). For example, if you hold VTSAX (which is 100% US Stock), you'd configure it like so (assuming account name in GnuCash is also "VTSAX"):
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

### RebalanceRelativePercentage
This is the **relative** percentage each asset class can deviate from your target allocation before the report will tell you to rebalance (see https://www.bogleheads.org/wiki/Rebalancing). For example, set this to 20 if you want to rebalance any time an asset class deviates from target by 20% relative to its target. Only relative percentage is supported at this time. The report will also tell you how far off you are in absolute percentage as well as dollar amounts, so you can still use the report to see if you need to rebalance even if you use a different rebalancing strategy.

## NetChangeInterval and NetChangeInterval2
This is a time interval to look back to determine the change in overall investment balance since the beginning of the interval. Only two intervals are supported at this time. This must be one of the following: "-[n] day(s)|month(s)|year(s)" or "start of day|month|year". For example: "-1 day", "-6 months", "start of year", etc. Note that this won't show your investments' rate of return, but the actual balance change, including contributions and investment growth.

## TimeOfDayCutoff
The investment balance is assumed to only update once a day. This feature also relies on the assumption you update prices in GnuCash at the same time every day (either manually or via a script). In order to accurately calculate daily balance changes, the program needs to know when your prices are updated. Set this to a time of day right after your prices are updated so that the program can tell whether it's looking at yesterday's or today's prices. If you hold mutual funds that update prices daily, you'll want to update your prices after the stock market closes for the day. For example, if you update your prices at 3:50pm (15:50) daily, set this to "16:00" so that the program can start showing the net daily balance change at 4pm. 

## Sample appsettings.json file
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AppSettings": {
    "GnuCashDbConnectionString": "Data Source=sqlite/gnucash.sqlite",
    "IncomeRootAccountGuid": "6d2bb682fb3649b3b8c84b4232fea511",
    "ExpenseRootAccountGuid": "0e1756401cd14f68be156196928476f4",
    "AssetRootAccountGuid": "0a19379c161a4c18b96bfb41ab9c0679",
    "LiabilityRootAccountGuid": "772af8022dd44907acbf28f4970d9163",
    "ClosingEntriesPattern": "Closing%",
    "TargetSavingsPercentage": 50,
    "ExpenseAccountEmojis": {
      "Auto": "🚗",
      "Food": "🍔",
      "Household": "🧻",
      "Medical Expenses": "🏥",
      "Taxes": "💰",
      "Travel": "🛫"
    },
    "InvestmentSettings": {
      "InvestmentRootAccountGuids": [ "12a13591eee247f4bd47047b64cea878", "848e25e11a9d4453be38544dfa95025b" ],
      "InvestmentAssetAllocations": [
        {
          "Name": "VBTLX",
          "US": 0,
          "INTNL": 0,
          "BND": 100
        },
        {
          "Name": "VXUS",
          "US": 0,
          "INTNL": 100,
          "BND": 0
        },
        {
          "Name": "VTSAX",
          "US": 100,
          "INTNL": 0,
          "BND": 0
        },
        {
          "Name": "VFFVX",
          "US": 54,
          "INTNL": 36,
          "BND": 10
        }
      ],
      "TargetAssetAllocation": {
        "Name": "Target Asset Allocation",
        "US": 70,
        "INTNL": 20,
        "BND": 10
      },
      "RebalanceRelativePercentage": 20,
      "NetChangeInterval": "-1 day",
      "NetChangeInterval2": "-7 days",
      "TimeOfDayCutoff": "16:00"
    }
  }
}
```