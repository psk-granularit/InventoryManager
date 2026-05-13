using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Models
{

    public class ProductBatchUpdateModel
    {
        public List<ProductUpdateModel> update { get; set; }
    }
}
