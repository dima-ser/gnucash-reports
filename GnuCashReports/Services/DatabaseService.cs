namespace GnuCashReports.Services
{
    using GnuCashReports.Models;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly AppSettings _appSettings;

        public DatabaseService(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _connectionString = _appSettings.GnuCashDbConnectionString;
        }

        public async Task<List<ProfitLossItem>> GetLevel2ProfitLossAsync()
        {
            var results = new List<ProfitLossItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@ytdStart", new DateTime(DateTime.Now.Year, 1, 1)));
                command.Parameters.Add(new SqliteParameter("@ytdEnd", new DateTime(DateTime.Now.Year + 1, 1, 1))); // include future-dated transaction until the end of current year
                command.Parameters.Add(new SqliteParameter("@prevStart", new DateTime(DateTime.Now.Year - 1, 1, 1)));
                command.Parameters.Add(new SqliteParameter("@prevEnd", new DateTime(DateTime.Now.Year, 1, 1)));
                command.Parameters.Add(new SqliteParameter("@incomeGuid", _appSettings.IncomeRootAccountGuid));
                command.Parameters.Add(new SqliteParameter("@expenseGuid", _appSettings.ExpenseRootAccountGuid));
                string closingEntriesSql = "";
                if (_appSettings.ClosingEntriesPattern != null)
                {
                    command.Parameters.Add(new SqliteParameter("@ignorePattern", _appSettings.ClosingEntriesPattern));
                    closingEntriesSql = " and description not like @ignorePattern ";
                }
                command.CommandText = @"
            WITH RECURSIVE account_tree AS (
    SELECT a.guid, a.name, a.parent_guid, a.account_type,
           a.guid AS level2_guid, a.name AS level2_name
    FROM accounts a
    WHERE a.parent_guid IN (@incomeGuid,@expenseGuid)

    UNION ALL

    SELECT a.guid, a.name, a.parent_guid, a.account_type,
           at.level2_guid, at.level2_name
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
pl_level2_ytd AS (
    SELECT at.level2_guid, at.level2_name AS account_name,
           at.account_type,
           SUM(s.value_num * 1.0 / s.value_denom) AS total_amount
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
    WHERE t.post_date BETWEEN @ytdStart AND @ytdEnd " + closingEntriesSql +
    @"GROUP BY at.level2_guid, at.level2_name, at.account_type
),
pl_level2_prev_year AS (
    SELECT at.level2_guid, at.level2_name AS account_name,
           at.account_type,
           SUM(s.value_num * 1.0 / s.value_denom) AS total_amount
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
    WHERE t.post_date BETWEEN @prevStart AND @prevEnd " + closingEntriesSql + 
    @"GROUP BY at.level2_guid, at.level2_name, at.account_type
)
SELECT ytd.account_type, ytd.account_name, ytd.total_amount as ytd_amount, prev.total_amount as prev_year_amount
FROM pl_level2_ytd as ytd left JOIN pl_level2_prev_year as prev on ytd.level2_guid=prev.level2_guid
union
SELECT prev.account_type, prev.account_name, ytd.total_amount as ytd_amount, prev.total_amount as prev_year_amount
FROM pl_level2_prev_year as prev left JOIN pl_level2_ytd as ytd on ytd.level2_guid=prev.level2_guid";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new ProfitLossItem
                            {
                                AccountType = reader.GetString(0),
                                AccountName = reader.GetString(1),
                                TotalAmountYTD = reader[2] != DBNull.Value ? reader.GetDecimal(2) : 0,
                                TotalAmountPrevYear = reader[3] != DBNull.Value ? reader.GetDecimal(3) : 0
                            });
                            //Console.WriteLine(reader.GetString(1) + " " + reader.GetString(2));
                        }
                    } //else
                    //{
                    //    Console.WriteLine("no data returned");
                    //}
                    return results;
                }
            }
        }

        public async Task<List<BalanceSheetItem>> GetBalanceSheetAsync()
        {
            var results = new List<BalanceSheetItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@assetGuid", _appSettings.AssetRootAccountGuid));
                command.Parameters.Add(new SqliteParameter("@liabilityGuid", _appSettings.LiabilityRootAccountGuid));
                command.CommandText = @"WITH RECURSIVE account_tree AS (
    -- Level 2 accounts (direct children of top-level)
    SELECT 
        a.guid,
        a.name,
        a.account_type,
        a.parent_guid,
        a.commodity_guid,
        a.commodity_scu,
        a.guid AS level2_guid,
        a.name AS level2_name,
		a.code AS level2_code
    FROM accounts a
    WHERE a.parent_guid IN (@assetGuid, @liabilityGuid)

    UNION ALL

    -- Descendants of level 2 accounts
    SELECT 
        a.guid,
        a.name,
        a.account_type,
        a.parent_guid,
        a.commodity_guid,
        a.commodity_scu,
        at.level2_guid,
        at.level2_name,
		at.level2_code
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
latest_prices AS (
    SELECT p.commodity_guid, MAX(p.date) AS latest_date
    FROM prices p
    GROUP BY p.commodity_guid
),
price_lookup AS (
    SELECT p.commodity_guid,
           p.value_num * 1.0 / p.value_denom AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.date = lp.latest_date
),
balances AS (
    SELECT 
        at.level2_name AS account_name,
        at.account_type,
		at.level2_code AS account_code,
        SUM(
            CASE 
                WHEN at.account_type in ('MUTUAL', 'STOCK') and c.namespace != 'CURRENCY' THEN
                    s.quantity_num * 1.0 / s.quantity_denom * IFNULL(pl.price, 0)
                ELSE
                    s.value_num * 1.0 / s.value_denom
            END
        ) AS balance
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
    LEFT JOIN price_lookup pl ON at.commodity_guid = pl.commodity_guid
	where DATE(t.post_date) <= DATETIME('now', 'localtime')  
    GROUP BY at.level2_guid, at.level2_name, at.account_type
    HAVING ABS(balance) > 0.0001
)
SELECT case when account_type in ('ASSET','BANK','CASH','MUTUAL','STOCK') then 'ASSET' else account_type end as general_account_type,  account_name, sum(balance) as balance
FROM balances
group by general_account_type, account_name
ORDER BY general_account_type, account_code;";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new BalanceSheetItem
                            {
                                AccountType = reader.GetString(0),
                                AccountName = reader.GetString(1),
                                Balance = reader.GetDecimal(2),
                            });
                        }
                    } 
                    return results;
                }
            }
        }

        /// <summary>
        /// Returns net worth as of specified date (inclusive)
        /// </summary>
        /// <param name="date">Date to return net worth for. Date is inclusive, meaning transactions and prices as of this date are included in teh net worth</param>
        /// <returns></returns>
        public async Task<decimal> GetNetWorthAsync(DateOnly date)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@assetGuid", _appSettings.AssetRootAccountGuid));
                command.Parameters.Add(new SqliteParameter("@liabilityGuid", _appSettings.LiabilityRootAccountGuid));
                DateOnly priceDate = date.AddDays(1); // to make the prices reflect end of day price
                command.Parameters.Add(new SqliteParameter("@transDate", date));
                command.Parameters.Add(new SqliteParameter("@priceDate", priceDate));
                command.CommandText = @"WITH RECURSIVE account_tree AS (
    -- Root accounts
    SELECT 
        a.guid,
        a.name,
        a.account_type,
        a.parent_guid,
        a.commodity_guid,
        a.commodity_scu,
        a.guid AS level2_guid,
        a.name AS level2_name,
		a.code AS level2_code
    FROM accounts a
    WHERE a.guid IN (@assetGuid, @liabilityGuid)

    UNION ALL

    -- Descendants of root accounts
    SELECT 
        a.guid,
        a.name,
        a.account_type,
        a.parent_guid,
        a.commodity_guid,
        a.commodity_scu,
        at.level2_guid,
        at.level2_name,
		at.level2_code
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
latest_prices AS (
    SELECT p.commodity_guid, MAX(p.date) AS latest_date
    FROM prices p
	WHERE p.date < DATETIME(@priceDate)  
    GROUP BY p.commodity_guid
),
price_lookup AS (
    SELECT p.commodity_guid,
           p.value_num * 1.0 / p.value_denom AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.date = lp.latest_date
),
balances AS (
    SELECT 
        at.level2_name AS account_name,
        at.account_type,
		at.level2_code AS account_code,
        SUM(
            CASE 
                WHEN at.account_type in ('MUTUAL', 'STOCK') and c.namespace != 'CURRENCY' THEN
                    s.quantity_num * 1.0 / s.quantity_denom * IFNULL(pl.price, 0)
                ELSE
                    s.value_num * 1.0 / s.value_denom
            END
        ) AS balance
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
    LEFT JOIN price_lookup pl ON at.commodity_guid = pl.commodity_guid
	where DATE(t.post_date) < DATETIME(@transDate)
    GROUP BY at.level2_guid, at.level2_name, at.account_type
    HAVING ABS(balance) > 0.0001
)
SELECT sum(balance) as networth
FROM balances";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    return reader.GetDecimal(0);
                }
            }
        }

        public async Task<List<BalanceSheetItem>> GetInvestmentsAsync(List<string> investmentRootAccountGuids, string netChangeInterval, string netChangeInterval2, TimeSpan cutoffTime)
        {
            var results = new List<BalanceSheetItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                //List<string> investmentRootAccountGuids = _appSettings.InvestmentRootAccountGuids;
                StringBuilder sqlInList = new StringBuilder("");
                for (int i = 0; i < investmentRootAccountGuids.Count; i++)
                {
                    command.Parameters.Add(new SqliteParameter("@guid"+i, investmentRootAccountGuids[i]));
                    sqlInList.Append("@guid" + i + ",");
                }
                sqlInList.Remove(sqlInList.Length - 1, 1); // remove the last comma
                command.Parameters.Add(new SqliteParameter("@netChangeInterval", netChangeInterval));
                command.Parameters.Add(new SqliteParameter("@netChangeInterval2", netChangeInterval2));
                command.Parameters.Add(new SqliteParameter("@cutoffModifier", DateTime.Now.TimeOfDay < cutoffTime ? "start of day" : "1 second"));
                command.CommandText = @"WITH RECURSIVE account_tree AS (
    SELECT guid, name, account_type, commodity_guid, commodity_scu, parent_guid
    FROM accounts
    WHERE guid IN (" + sqlInList.ToString() + @")
    UNION ALL

    SELECT a.guid, a.name, a.account_type, a.commodity_guid, a.commodity_scu, a.parent_guid
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
latest_prices AS (
    SELECT p.commodity_guid,
           MAX(p.date) AS latest_date
    FROM prices p
    GROUP BY p.commodity_guid
),
prev_prices AS (
	 SELECT p.commodity_guid,
           MAX(p.date) AS latest_date
    FROM prices p
	WHERE p.date < datetime('now', 'localtime', @netChangeInterval, @cutoffModifier)
    GROUP BY p.commodity_guid
),
prev_prices2 AS (
	 SELECT p.commodity_guid,
           MAX(p.date) AS latest_date
    FROM prices p
	WHERE p.date < datetime('now', 'localtime', @netChangeInterval2, @cutoffModifier)
    GROUP BY p.commodity_guid
),
price_lookup AS (
    SELECT p.commodity_guid,
           p.value_num * 1.0 / p.value_denom AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.date = lp.latest_date
),
prev_price_lookup AS (
    SELECT p.commodity_guid,
           p.value_num * 1.0 / p.value_denom AS price
    FROM prices p
    JOIN prev_prices lp ON p.commodity_guid = lp.commodity_guid AND p.date = lp.latest_date
),
prev_price_lookup2 AS (
    SELECT p.commodity_guid,
           p.value_num * 1.0 / p.value_denom AS price
    FROM prices p
    JOIN prev_prices2 lp ON p.commodity_guid = lp.commodity_guid AND p.date = lp.latest_date
),
balances AS (
    SELECT 
		at.guid,
        at.account_type,
        at.name AS account_name,
        CASE 
            WHEN at.account_type in ('MUTUAL', 'STOCK') AND c.namespace != 'CURRENCY' THEN
                SUM(s.quantity_num * 1.0 / s.quantity_denom * pl.price)
            ELSE
                SUM(s.value_num * 1.0 / s.value_denom)
        END AS balance
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid	
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
    LEFT JOIN price_lookup pl ON at.commodity_guid = pl.commodity_guid
    GROUP BY at.account_type, at.name
    --HAVING ABS(balance) > 0.0001
),
prev_balances AS (
    SELECT 
		at.guid,
        at.account_type,
        at.name AS account_name,
		CASE 
            WHEN at.account_type in ('MUTUAL', 'STOCK') AND c.namespace != 'CURRENCY' THEN
                SUM(s.quantity_num * 1.0 / s.quantity_denom * ppl.price)
            ELSE
                SUM(s.value_num * 1.0 / s.value_denom)
        END AS prev_balance
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid and t.post_date < datetime('now', 'localtime', @netChangeInterval)
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
	LEFT JOIN prev_price_lookup ppl ON at.commodity_guid = ppl.commodity_guid
    GROUP BY at.account_type, at.name
    --HAVING ABS(prev_balance) > 0.0001 
),
prev_balances2 AS (
    SELECT 
		at.guid,
        at.account_type,
        at.name AS account_name,
		CASE 
            WHEN at.account_type in ('MUTUAL', 'STOCK') AND c.namespace != 'CURRENCY' THEN
                SUM(s.quantity_num * 1.0 / s.quantity_denom * ppl.price)
            ELSE
                SUM(s.value_num * 1.0 / s.value_denom)
        END AS prev_balance2
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid and t.post_date < datetime('now', 'localtime', @netChangeInterval2)
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
	LEFT JOIN prev_price_lookup2 ppl ON at.commodity_guid = ppl.commodity_guid
    GROUP BY at.account_type, at.name
    --HAVING ABS(prev_balance) > 0.0001 
)
SELECT b.account_type, b.account_name, b.balance, p.prev_balance, p2.prev_balance2
FROM balances b LEFT JOIN prev_balances p on b.guid=p.guid
LEFT JOIN prev_balances2 p2 on b.guid=p2.guid";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new BalanceSheetItem
                            {
                                AccountType = reader.GetString(0),
                                AccountName = reader.GetString(1),
                                Balance = reader.GetDecimal(2),
                                PreviousBalance = reader[3] != DBNull.Value ? reader.GetDecimal(3) : 0,
                                PreviousBalance2 = reader[4] != DBNull.Value ? reader.GetDecimal(4) : 0
                            });
                        }
                    }
                    return results;
                }
            }
        }

        public async Task<LastUpdated> GetLastUpdatedAsync()
        {
            var result = new LastUpdated();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"select MAX(post_date) as date, 'transaction' as date_type from transactions
where post_date <= DATE('now')  
union all
select max(date) as date, 'price' as date_type from prices";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            DateTime temp = DateTime.MinValue;
                            if (reader[1].ToString() == "transaction")
                            {
                                DateTime.TryParse(reader[0].ToString(), out temp);
                                result.LastTransactionDate = temp;
                            }
                            else if (reader[1].ToString() == "price")
                            {
                                DateTime.TryParse(reader[0].ToString(), out temp);
                                result.LastPriceDate = temp;
                            }
                        }
                    }
                    return result;
                }
            }
        }
    }

}
