using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Models
{
    public class Config
    {
        public List<string> WarehousesToIncludeInInventory { get; set; }   
        public int?  ProductsToUpdate { get; set; }    
        public int? ProductsToSkip { get; set; }

        public List<string> SyncronizerReportReceivers {  get; set; }   
    }
}
