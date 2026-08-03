namespace GnuCashReports.Models
{
    public class AssetAllocationItem
    {
        public ReportItem InvestmentItem  { get; set; }
        private AssetAllocation _assetAllocation { get; set; }

        public AssetAllocationItem(ReportItem investmentItem, AssetAllocation assetAllocation)
        {
            InvestmentItem = investmentItem;
            _assetAllocation = assetAllocation;
        }

        public decimal USAmount
        {
            get
            {
                return InvestmentItem.Amount * (_assetAllocation.US / 100);
            }
        }
        public decimal IntnlAmount
        {
            get
            {
                return InvestmentItem.Amount * (_assetAllocation.INTNL / 100);
            }
        }
        public decimal BondAmount
        {
            get
            {
                return InvestmentItem.Amount * (_assetAllocation.BND / 100);
            }
        }
    }

}
