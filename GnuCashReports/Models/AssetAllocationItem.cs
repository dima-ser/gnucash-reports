namespace GnuCashReports.Models
{
    public class AssetAllocationItem
    {
        public InvestmentItem InvestmentItem  { get; set; }
        private AssetAllocation _assetAllocation { get; set; }

        public AssetAllocationItem(InvestmentItem investmentItem, AssetAllocation assetAllocation)
        {
            InvestmentItem = investmentItem;
            _assetAllocation = assetAllocation;
        }

        public decimal USAmount
        {
            get
            {
                return InvestmentItem.Balance * (_assetAllocation.US / 100);
            }
        }
        public decimal IntnlAmount
        {
            get
            {
                return InvestmentItem.Balance * (_assetAllocation.INTNL / 100);
            }
        }
        public decimal BondAmount
        {
            get
            {
                return InvestmentItem.Balance * (_assetAllocation.BND / 100);
            }
        }
    }

}
