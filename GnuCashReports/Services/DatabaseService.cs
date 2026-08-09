namespace GnuCashReports.Services
{
    using GnuCashReports.Models;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
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
        /// <summary>
        /// Returns profit&loss/income statement data for specified period. Both start and end dates are inclusive
        /// </summary>
        /// <param name="startDate">Include transactions posted on this date or later (inclusive)</param>
        /// <param name="endDate">Include transactions posted on this date or before (inclusive)</param>
        /// <returns></returns>
        public async Task<List<ReportItem>> GetLevel2ProfitLossAsync(DateOnly startDate, DateOnly endDate)
        {
            var results = new List<ReportItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@startDate", startDate));
                command.Parameters.Add(new SqliteParameter("@endDate", endDate)); 
                command.Parameters.Add(new SqliteParameter("@reportCurrency", _appSettings.ReportCurrency));
                //command.Parameters.Add(new SqliteParameter("@rootAccountName", _appSettings.RootAccountName));
                //command.Parameters.Add(new SqliteParameter("@ignorePattern", 
                //!String.IsNullOrWhiteSpace(_appSettings.ClosingEntriesPattern) ? _appSettings.ClosingEntriesPattern : DBNull.Value));
                command.CommandText = @"
            WITH RECURSIVE account_tree AS (
    SELECT a.guid, a.name, a.parent_guid, a.account_type, a.commodity_guid,
           a.guid AS level2_guid, a.name AS level2_name
    FROM accounts a
    WHERE a.parent_guid IN (
		(select guid from accounts where account_type='INCOME' and parent_guid=(select root_account_guid from books limit 1)),
		(select guid from accounts where account_type='EXPENSE' and parent_guid=(select root_account_guid from books limit 1))
	)
    UNION ALL

    SELECT a.guid, a.name, a.parent_guid, a.account_type, a.commodity_guid,
           at.level2_guid, at.level2_name
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
closing_txguids as (select obj_guid from slots where name='book_closing'),
latest_prices AS (
    SELECT p.commodity_guid, p.currency_guid, MAX(p.date) AS latest_date
    FROM prices p 
    WHERE DATE(p.date) <= DATE(@endDate)  
    GROUP BY p.commodity_guid, p.currency_guid
),
primary_prices as (
SELECT p.commodity_guid, p.currency_guid, c.mnemonic, c.namespace, p.value_num * 1.0 / p.value_denom AS price
    FROM prices p 
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.currency_guid=c.guid),
inverse_prices as (
 SELECT p.currency_guid as commodity_guid, p.commodity_guid as currency_guid, c.mnemonic, c.namespace, p.value_denom * 1.0 / p.value_num AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.commodity_guid=c.guid),
all_prices as (
SELECT * from primary_prices
UNION
SELECT * from inverse_prices
UNION -- second order prices	
SELECT p.commodity_guid, i.currency_guid, i.mnemonic, i.namespace, p.price * i.price as price 
	FROM primary_prices p 
	JOIN inverse_prices i on p.currency_guid=i.commodity_guid),
pl_level2 AS (
    SELECT at.level2_guid, at.level2_name AS account_name,
           at.account_type,
           SUM(
            CASE 
                WHEN c.mnemonic = @reportCurrency and c.namespace='CURRENCY' THEN
					s.quantity_num * 1.0 / s.quantity_denom
                ELSE
					s.quantity_num * 1.0 / s.quantity_denom * IFNULL(pl.price, 0)  
            END
        ) AS amount
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
	LEFT JOIN commodities c ON at.commodity_guid = c.guid
	LEFT JOIN all_prices pl ON at.commodity_guid = pl.commodity_guid and pl.mnemonic=@reportCurrency and pl.namespace='CURRENCY'
    WHERE DATE(t.post_date) >= DATE(@startDate) AND DATE(t.post_date) <= DATE(@endDate) AND 
	t.guid not in (select obj_guid from closing_txguids)
    GROUP BY at.level2_guid, at.level2_name, at.account_type
)
SELECT account_type, account_name, amount
FROM pl_level2";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            decimal amount = reader[2] != DBNull.Value ? reader.GetDecimal(2) : 0;
                            string accountType = reader.GetString(0);
                            if (accountType == AccountType.INCOME)
                                amount *= -1; // income amounts are credit/negative in db, so reverse the sign before returning
                            results.Add(new ReportItem
                            {
                                AccountType = accountType,
                                AccountName = reader.GetString(1),
                                Amount = amount
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

        public async Task<decimal> GetAverageAnnualExpenses()
        {
            if (_appSettings.FISettings == null)
            {
                throw new ArgumentException("Before calling this method, FISettings must be configured in appsettings.json");
            }
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                int numYears = _appSettings.FISettings.AverageExpensesYearsLookback;
                command.Parameters.Add(new SqliteParameter("@numYears", numYears));
                DateTime startDate = new DateTime(DateTime.Now.Year - numYears, 1, 1);
                DateTime endDate = new DateTime(DateTime.Now.Year - 1, 12, 31);
                command.Parameters.Add(new SqliteParameter("@startDate", startDate));
                command.Parameters.Add(new SqliteParameter("@endDate", endDate));
                command.Parameters.Add(new SqliteParameter("@reportCurrency", _appSettings.ReportCurrency));
                //command.Parameters.Add(new SqliteParameter("@ignorePattern", 
                //    !String.IsNullOrWhiteSpace(_appSettings.ClosingEntriesPattern) ? _appSettings.ClosingEntriesPattern : DBNull.Value));
                command.CommandText = @"
            WITH RECURSIVE
root_guid as (select root_account_guid as guid from books limit 1),
root_expense_guid as (select guid from accounts where account_type='EXPENSE' and parent_guid=(select guid from root_guid)),
account_tree AS (
    SELECT a.guid, a.name, a.parent_guid, a.account_type, a.commodity_guid,
           a.guid AS level2_guid, a.name AS level2_name
    FROM accounts a
    WHERE a.parent_guid=(select guid from root_expense_guid)

    UNION ALL

    SELECT a.guid, a.name, a.parent_guid, a.account_type, a.commodity_guid,
           at.level2_guid, at.level2_name
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
closing_txguids as (select obj_guid from slots where name='book_closing'),
latest_prices AS (
    SELECT p.commodity_guid, p.currency_guid, MAX(p.date) AS latest_date
    FROM prices p 
    WHERE DATE(p.date) <= DATE(@endDate)  
    GROUP BY p.commodity_guid, p.currency_guid
),
primary_prices as (
SELECT p.commodity_guid, p.currency_guid, c.mnemonic, c.namespace, p.value_num * 1.0 / p.value_denom AS price
    FROM prices p 
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.currency_guid=c.guid),
inverse_prices as (
 SELECT p.currency_guid as commodity_guid, p.commodity_guid as currency_guid, c.mnemonic, c.namespace, p.value_denom * 1.0 / p.value_num AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.commodity_guid=c.guid),
all_prices as (
SELECT * from primary_prices
UNION
SELECT * from inverse_prices
UNION -- second order prices	
SELECT p.commodity_guid, i.currency_guid, i.mnemonic, i.namespace, p.price * i.price as price 
	FROM primary_prices p 
	JOIN inverse_prices i on p.currency_guid=i.commodity_guid),
pl_level2_ytd AS (
    SELECT at.level2_guid, at.level2_name AS account_name,
           at.account_type,
           SUM(
            CASE 
                WHEN c.mnemonic = @reportCurrency and c.namespace='CURRENCY' THEN
					s.quantity_num * 1.0 / s.quantity_denom
                ELSE
					s.quantity_num * 1.0 / s.quantity_denom * IFNULL(pl.price, 0)  
            END
        ) AS total_amount
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
	LEFT JOIN commodities c ON at.commodity_guid = c.guid
	LEFT JOIN all_prices pl ON at.commodity_guid = pl.commodity_guid and pl.mnemonic=@reportCurrency and pl.namespace='CURRENCY'
    WHERE DATE(t.post_date) >= DATE(@startDate) AND DATE(t.post_date) <= DATE(@endDate) AND 
		t.guid not in (select obj_guid from closing_txguids)
    GROUP BY at.level2_guid, at.level2_name, at.account_type
)
select sum(total_amount)/@numYears as AverageAnnualExpenses from pl_level2_ytd";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                        return await reader.ReadAsync() && reader[0] != DBNull.Value ? reader.GetDecimal(0) : 0;
                    else
                        return 0;
                }
            }
        }

        /// <summary>
        /// Returns the balance sheet for root balance sheet accounts as of current date
        /// </summary>
        public async Task<List<ReportItem>> GetBalanceSheetAsync()
        {
            return await GetBalanceSheetAsync(await GetRootBalanceSheetAccountGuids(), DateOnly.FromDateTime(DateTime.Now));
        }

        /// <summary>
        /// Returns guids of root ASSET and LIABILITY accounts (direct descendents of the root account)
        /// </summary>
        public async Task<List<string>> GetRootBalanceSheetAccountGuids()
        {
            List<string> rootBalanceSheetAccountGuids = new List<string>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                //command.Parameters.Add(new SqliteParameter("@rootAccountName", _appSettings.RootAccountName));
                command.CommandText = @"select guid from accounts where account_type in ('ASSET','LIABILITY') 
                and parent_guid=(select root_account_guid from books limit 1)";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            rootBalanceSheetAccountGuids.Add(reader.GetString(0));
                        }
                        return rootBalanceSheetAccountGuids;
                    }
                    else
                        throw new Exception("No root accounts found for ASSET and LIABILITY. Your database may be corrupted."); 
                }
            }
        }

        
        /// <summary>
        /// Returns a balance sheet for specified list of account Guids as of specified date. Uses the last prices up through the report date. 
        /// Sub-account balances are summed up and only direct descendents of the specified parentAccountGuids are listed, including all sub-account balances
        /// </summary>
        /// <param name="parentAccountGuids">Guids of accounts to list balances for</param>
        /// <param name="date">Date (inclusive) as of which to return the balance sheet for</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<List<ReportItem>> GetBalanceSheetAsync(List<string> parentAccountGuids, DateOnly date)
        {
            if (parentAccountGuids == null || parentAccountGuids.Count == 0)
            {
                throw new ArgumentException("At least one parent account guid must be provided");
            }
            var results = new List<ReportItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                // Pass the list of parent GUIDs as a single JSON array parameter and expand it with json_each()
                string parentGuidsJson = JsonSerializer.Serialize(parentAccountGuids);
                command.Parameters.Add(new SqliteParameter("@parentGuidsJson", parentGuidsJson));
                command.Parameters.Add(new SqliteParameter("@date", date));
                command.Parameters.Add(new SqliteParameter("@reportCurrency", _appSettings.ReportCurrency));
                command.CommandText = @"WITH RECURSIVE account_tree AS (
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
    WHERE a.parent_guid IN (SELECT value FROM json_each(@parentGuidsJson))

    UNION ALL

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
    SELECT p.commodity_guid, p.currency_guid, MAX(p.date) AS latest_date
    FROM prices p 
    WHERE DATE(p.date) <= DATE(@date)  
    GROUP BY p.commodity_guid, p.currency_guid
),
primary_prices as (
SELECT p.commodity_guid, p.currency_guid, c.mnemonic, c.namespace, p.value_num * 1.0 / p.value_denom AS price
    FROM prices p 
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.currency_guid=c.guid),
inverse_prices as (
 SELECT p.currency_guid as commodity_guid, p.commodity_guid as currency_guid, c.mnemonic, c.namespace, p.value_denom * 1.0 / p.value_num AS price
    FROM prices p
    JOIN latest_prices lp ON p.commodity_guid = lp.commodity_guid AND p.currency_guid=lp.currency_guid AND p.date = lp.latest_date
	JOIN commodities c on p.commodity_guid=c.guid),
all_prices as (
SELECT * from primary_prices
UNION
SELECT * from inverse_prices
UNION -- second order prices	
SELECT p.commodity_guid, i.currency_guid, i.mnemonic, i.namespace, p.price * i.price as price 
	FROM primary_prices p 
	JOIN inverse_prices i on p.currency_guid=i.commodity_guid),
balances AS (
    SELECT 
        at.level2_name AS account_name,
        at.account_type,
		at.level2_code AS account_code, c.guid as commodity_guid, pl.price, c.mnemonic, c.namespace, at.guid,
        SUM(
            CASE 
                WHEN c.mnemonic = @reportCurrency and c.namespace='CURRENCY' THEN
					s.quantity_num * 1.0 / s.quantity_denom
                ELSE
					s.quantity_num * 1.0 / s.quantity_denom * IFNULL(pl.price, 0)  
            END
        ) AS balance
    FROM splits s
    JOIN transactions t ON s.tx_guid = t.guid
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
    LEFT JOIN all_prices pl ON at.commodity_guid = pl.commodity_guid and pl.mnemonic=@reportCurrency and pl.namespace='CURRENCY'
	where DATE(t.post_date) <= DATE(@date)
    GROUP BY at.level2_guid, at.level2_name, at.account_type, c.guid
    HAVING ABS(balance) > 0.0001
)
--select * from balances
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
                            results.Add(new ReportItem
                            {
                                AccountType = reader.GetString(0),
                                AccountName = reader.GetString(1),
                                Amount = reader.GetDecimal(2),
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
        /// <param name="date">Date to return net worth for. Date is inclusive, meaning transactions and prices as of this date are included in the net worth</param>
        /// <returns></returns>
        public async Task<decimal> GetNetWorthAsync(DateOnly date)
        {
            List<ReportItem> balanceSheet = await GetBalanceSheetAsync(await GetRootBalanceSheetAccountGuids(), date);
            return balanceSheet.Sum(b=>b.Amount);
        }

        public async Task<List<ReportItem>> GetInvestmentsAsync(List<string> investmentParentAccountGuids, DateOnly date)
        {

            var results = new List<ReportItem>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();

                string parentGuidsJson = JsonSerializer.Serialize(investmentParentAccountGuids);
                command.Parameters.Add(new SqliteParameter("@parentGuidsJson", parentGuidsJson));
                command.Parameters.Add(new SqliteParameter("@date", date));
                command.CommandText = @"WITH RECURSIVE account_tree AS (
    SELECT guid, name, account_type, commodity_guid, commodity_scu, parent_guid
    FROM accounts
    WHERE guid IN (SELECT value FROM json_each(@parentGuidsJson))
    UNION ALL

    SELECT a.guid, a.name, a.account_type, a.commodity_guid, a.commodity_scu, a.parent_guid
    FROM accounts a
    JOIN account_tree at ON a.parent_guid = at.guid
),
latest_prices AS (
    SELECT p.commodity_guid,
           MAX(p.date) AS latest_date
    FROM prices p
    WHERE DATE(p.date) <= DATE(@date) 
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
    JOIN transactions t ON s.tx_guid = t.guid and DATE(t.post_date) <= DATE(@date) 	
    JOIN account_tree at ON s.account_guid = at.guid
    LEFT JOIN commodities c ON at.commodity_guid = c.guid
    LEFT JOIN price_lookup pl ON at.commodity_guid = pl.commodity_guid
    GROUP BY at.guid, at.account_type, at.name
    HAVING ABS(balance) > 0.0001
)
SELECT b.account_type, b.account_name, b.balance
FROM balances b";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new ReportItem
                            {
                                AccountType = reader.GetString(0),
                                AccountName = reader.GetString(1),
                                Amount = reader.GetDecimal(2)
                            });
                        }
                    }
                    return results;
                }
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            var result = new DatabaseStats();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"SELECT
                    t.last_enter_date,
                    p.last_price_date,
                    t.oldest_transaction_date,
                    t.transaction_count,
                    a.account_count_all,
                    a.account_count_active
                FROM (
                    SELECT
                        MAX(enter_date) AS last_enter_date,
                        MIN(post_date) AS oldest_transaction_date,
                        COUNT(guid) AS transaction_count
                    FROM transactions
                ) t
                CROSS JOIN (
                    SELECT MAX(date) AS last_price_date
                    FROM prices
                ) p
                CROSS JOIN (
                    SELECT
                        COUNT(CASE WHEN placeholder = 0 THEN 1 END) AS account_count_all,
                        COUNT(CASE WHEN placeholder = 0 AND hidden = 0 THEN 1 END) AS account_count_active
                    FROM accounts
                ) a;";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            DateTime temp = DateTime.MinValue;
                            DateTime.TryParse(reader["last_enter_date"].ToString(), out temp);
                            result.LastUpdatedDate = temp.ToLocalTime();
                            DateTime.TryParse(reader["last_price_date"].ToString(), out temp);
                            result.LastPriceDate = DateOnly.FromDateTime(temp);;
                            DateTime.TryParse(reader["oldest_transaction_date"].ToString(), out temp);

                            result.OldestTransactionDate = DateOnly.FromDateTime(temp);;
                            result.TransactionCount = Convert.ToInt32(reader["transaction_count"]);
                            result.AllAccountCount = Convert.ToInt32(reader["account_count_all"]);
                            result.ActiveAccountCount = Convert.ToInt32(reader["account_count_active"]);

                            // DateTime temp = DateTime.MinValue;
                            // if (reader[1].ToString() == "transaction")
                            // {
                            //     DateTime.TryParse(reader[0].ToString(), out temp);
                            //     result.LastTransactionDate = temp;
                            // }
                            // else if (reader[1].ToString() == "price")
                            // {
                            //     DateTime.TryParse(reader[0].ToString(), out temp);
                            //     result.LastPriceDate = temp;
                            // }
                        }
                    }
                    return result;
                }
            }
        }

        /// <summary>
        /// Returns account guid based on full account path
        /// </summary>
        /// <param name="fullAccountPath">Full account path, delimited with colons. For example, Assets:Cash:Checking</param>
        /// <returns></returns>
        public async Task<string> GetAccountGuid(string fullAccountPath)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@fullAccountPath", fullAccountPath));
                command.CommandText = @"WITH RECURSIVE account_paths AS (
    SELECT
        guid,
        parent_guid,
        name,
        CASE
            WHEN account_type = 'ROOT' THEN ''
            ELSE name
        END AS full_path
    FROM accounts
    WHERE parent_guid IS NULL

    UNION ALL

    SELECT
        a.guid,
        a.parent_guid,
        a.name,
        CASE
            WHEN ap.full_path = ''
                THEN a.name
            ELSE ap.full_path || ':' || a.name
        END
    FROM accounts a
    JOIN account_paths ap
        ON a.parent_guid = ap.guid
)
SELECT guid
FROM account_paths
WHERE full_path = @fullAccountPath";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        return reader.GetString(0);
                    }
                    else
                        throw new Exception("No account found with path \"" + fullAccountPath);
                }
            }
        }

        public async Task<List<CashFlowItem>> GetCashFlowStatement(string parentCashAccountPath, DateOnly startDate, DateOnly endDate)
        {
            string parentCashAccountGuid = await GetAccountGuid(parentCashAccountPath);
            List<CashFlowItem> cashFlows = new List<CashFlowItem>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.Parameters.Add(new SqliteParameter("@parentCashAccountGuid", parentCashAccountGuid));
                command.Parameters.Add(new SqliteParameter("@startDate", startDate));
                command.Parameters.Add(new SqliteParameter("@endDate", endDate));
                //command.Parameters.Add(new SqliteParameter("@rootAccountName", _appSettings.RootAccountName));
                command.CommandText = @"WITH RECURSIVE 
  -- 1. Identify all GUIDs inside the Cash box (the parent and all its children)
  cash_accounts AS (
    SELECT guid FROM accounts WHERE guid = @parentCashAccountGuid
    UNION ALL
    SELECT a.guid FROM accounts a
    JOIN cash_accounts ca ON a.parent_guid = ca.guid
  ),

  -- 2. Find the Root Account GUID, then seed its immediate children
  all_account_paths AS (
    SELECT 
      guid, 
      parent_guid, 
      name, 
      name AS full_path
    FROM accounts 
    WHERE parent_guid = (select root_account_guid from books limit 1)
    
    UNION ALL
    
    -- Recursive step: append child names using a colon
    SELECT 
      child.guid, 
      child.parent_guid, 
      child.name, 
      parent.full_path || ':' || child.name
    FROM accounts child
    JOIN all_account_paths parent ON child.parent_guid = parent.guid
  ),

  -- 3. Find transactions that touch our cash accounts
  transactions_touching_cash AS (
    SELECT DISTINCT tx_guid 
    FROM splits 
    WHERE account_guid IN (SELECT guid FROM cash_accounts)
  ),

  -- 4. Get the counterparties and determine if individual splits are inflows or outflows
  counter_splits AS (
    SELECT 
      s.account_guid,
      CASE WHEN (CAST(s.value_num AS REAL) / s.value_denom) < 0 
           THEN ABS(CAST(s.value_num AS REAL) / s.value_denom) ELSE 0 END AS inflow_amount,
      CASE WHEN (CAST(s.value_num AS REAL) / s.value_denom) > 0 
           THEN CAST(s.value_num AS REAL) / s.value_denom ELSE 0 END AS outflow_amount
    FROM splits s
    JOIN transactions tx ON s.tx_guid = tx.guid
    WHERE s.tx_guid IN (SELECT tx_guid FROM transactions_touching_cash)
      AND s.account_guid NOT IN (SELECT guid FROM cash_accounts)
      AND DATE(tx.post_date) >= DATE(@startDate) AND DATE(tx.post_date) <= DATE(@endDate)
  )

-- 5. Aggregate inflows and outflows by the pre-cleaned counterparty account path
SELECT 
  p.full_path AS AccountPath,
  ROUND(SUM(cs.inflow_amount), 2) AS Inflow,
  ROUND(SUM(cs.outflow_amount), 2) AS Outflow
FROM counter_splits cs
JOIN all_account_paths p ON cs.account_guid = p.guid
GROUP BY p.full_path
HAVING SUM(cs.inflow_amount) > 0 OR SUM(cs.outflow_amount) > 0
ORDER BY p.full_path";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            cashFlows.Add(new CashFlowItem{ 
                                AccountPath = reader.GetString(0), 
                                Inflow = reader.GetDecimal(1), 
                                Outflow = reader.GetDecimal(2) });
                        }
                    }
                    return cashFlows;
                }
            }
        }
    }

}
