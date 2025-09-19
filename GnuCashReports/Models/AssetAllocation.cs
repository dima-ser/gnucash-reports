using System.Runtime;
using System.Text.Json.Serialization;

namespace GnuCashReports.Models
{
    public class AssetAllocation
    {
        public string Name { get; set; }

        public decimal US { get; }

        public decimal INTNL { get; }
        public decimal BND { get;  }
        public AssetAllocation(string Name, decimal US, decimal INTNL, decimal BND) 
        {
            //if (US + INTNL + BND != 100)
            //    throw new ArgumentException("Error initializing AssetAllocation \"" + Name + "\": Percentages must add up to 100");
            this.Name = Name;
            this.US = US;
            this.INTNL = INTNL;
            this.BND = BND;
        }

        public bool PercentagesAddUpTo100()
        {
            return US + INTNL + BND == 100;
        }
    }

}
