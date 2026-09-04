using System.Runtime.Serialization;

namespace InventoryManager.Models
{

    public class ProductBatchUpdateModel
    {
        public List<ProductUpdateModel> update { get; set; }
    }
}
