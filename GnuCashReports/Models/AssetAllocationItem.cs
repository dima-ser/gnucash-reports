namespace GnuCashReports.Models
{
    public class AssetAllocationItem
    {
        public BalanceSheetItem BalanceSheetItem  { get; set; }
        private AssetAllocation _assetAllocation { get; set; }

        public AssetAllocationItem(BalanceSheetItem balanceSheetItem, AssetAllocation assetAllocation)
        {
            BalanceSheetItem = balanceSheetItem;
            _assetAllocation = assetAllocation;
        }

        public decimal USAmount
        {
            get
            {
                return BalanceSheetItem.Balance * (_assetAllocation.US / 100);
            }
        }
        public decimal IntnlAmount
        {
            get
            {
                return BalanceSheetItem.Balance * (_assetAllocation.INTNL / 100);
            }
        }
        public decimal BondAmount
        {
            get
            {
                return BalanceSheetItem.Balance * (_assetAllocation.BND / 100);
            }
        }
    }

}
