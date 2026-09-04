using System.Runtime.Serialization;

namespace InventoryManager.Models
{
    public  class ProductUpdateModel
    {
        public int id { get; set; }

        public decimal stock_quantity { get; set; }

        public string status { get; set; }

    }
    
}
