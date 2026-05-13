using InventoryManager.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Models
{
    public class SapProduct
    {
        [DataField("ItemCode")]
        public string ItemCode { get; set; }
        [DataField("ItemName")]
        public string ItemName { get; set; }

        [DataField("OnHand")]
        public decimal StockQty { get; set; }

        [DataField("IsBlPackItem")]
        public string IsBlPackItem { get; set; }

        [DataField("BlPackQuantity")]
        public int BlPackQuantity { get; set; }


    }
}
