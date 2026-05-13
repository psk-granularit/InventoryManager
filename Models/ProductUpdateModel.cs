using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Models
{
    public  class ProductUpdateModel
    {
        public int id { get; set; }

        public decimal stock_quantity { get; set; }

        public string status { get; set; }

    }
    
}
