namespace GnuCashReports.Models
{
    /// <summary>
    /// Represents a three-column report row consisting of a name and two decimal numbers. 
    /// Two instances of ThreeColumnReportItem are considered equal if they have the same Name (case insensitive)
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
        /// Combines two lists of items into one list of ThreeColumnReportItem, merging them based on the name. 
        /// For items that only exist on one side, the other side's amount is set to zero.
        /// </summary>
        /// <param name="itemsRight"></param>
        /// <param name="itemsLeft"></param>
        /// <returns></returns>
        public static List<ThreeColumnReportItem> CombineItems(Dictionary<string, decimal> itemsLeft, Dictionary<string, decimal> itemsRight)
        {
            List<ThreeColumnReportItem> combinedItems = new List<ThreeColumnReportItem>();
            foreach(var rightItem in itemsRight)
            {
                bool foundMatch = false;
                foreach(var leftItem in itemsLeft)
                {
                    if (rightItem.Key == leftItem.Key)
                    {
                        foundMatch = true;
                        combinedItems.Add(new ThreeColumnReportItem{ Name = rightItem.Key, AmountRight = rightItem.Value, AmountLeft = leftItem.Value});
                        break;
                    }
                }
                if (!foundMatch)
                    combinedItems.Add(new ThreeColumnReportItem{ Name = rightItem.Key, AmountRight = rightItem.Value, AmountLeft = 0});
            }
            // add remaining items from itemsLeft that didn't have a match
            foreach(var leftItem in itemsLeft)
            {
                var combinedItem = new ThreeColumnReportItem { Name = leftItem.Key, AmountRight = 0, AmountLeft = leftItem.Value };
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