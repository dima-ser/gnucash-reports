namespace GnuCashReports.Models
{
    /// <summary>
    /// Represents a three-column report row consisting of a name and two decimal numbers
    /// </summary>
    public class ThreeColumnReportItem
    {
        public string Name { get; set; } = String.Empty;
        public decimal AmountRight { get; set; }
        public decimal AmountLeft { get; set; }

        public bool Equals(ThreeColumnReportItem? other)
        {
            if (other is null) return false;
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as ThreeColumnReportItem);

        public override int GetHashCode() =>
            Name?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;

        /// <summary>
        /// Combines two lists of items into one list of ThreeColumnReportItem, merging them based on the name
        /// </summary>
        /// <param name="items1"></param>
        /// <param name="items2"></param>
        /// <returns></returns>
        public static List<ThreeColumnReportItem> CombineItems(Dictionary<string, decimal> items1, Dictionary<string, decimal> items2)
        {
            List<ThreeColumnReportItem> combinedItems = new List<ThreeColumnReportItem>();
            foreach(var item1 in items1)
            {
                bool foundMatch = false;
                foreach(var item2 in items2)
                {
                    if (item1.Key == item2.Key)
                    {
                        foundMatch = true;
                        combinedItems.Add(new ThreeColumnReportItem{ Name = item1.Key, AmountRight = item1.Value, AmountLeft = item2.Value});
                        break;
                    }
                }
                if (!foundMatch)
                    combinedItems.Add(new ThreeColumnReportItem{ Name = item1.Key, AmountRight = item1.Value, AmountLeft = 0});
            }
            // add remaining items from items2 that didn't have a match
            foreach(var item2 in items2)
            {
                var combinedItem = new ThreeColumnReportItem { Name = item2.Key, AmountRight = 0, AmountLeft = item2.Value };
                if (!combinedItems.Contains(combinedItem))
                {
                    combinedItems.Add(combinedItem);
                }
            }

            return combinedItems
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}