using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Models
{
    public class PriceMismatchModel
    {
        public string Sku { get; set; }
        public string WooComercePrice { get; set; }
        public string SapPrice { get; set; }
    }
}
