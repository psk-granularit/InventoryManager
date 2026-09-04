using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace InventoryManager.Models
{
    [DataContract]
    public class Product
    {
        [DataMember(Name = "id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [DataMember(Name = "name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [DataMember(Name = "sku")]
        [JsonPropertyName("sku")]
        public string Sku { get; set; }
        [DataMember(Name = "price")]
        [JsonPropertyName("price")]
        public string Price { get; set;}
        [DataMember(Name = "regular_price")]
        [JsonPropertyName("regular_price")]
        public string RegularPrice { get; set;}
        [DataMember(Name = "stock_quantity")]
        [JsonPropertyName("stock_quantity")]
        public int? StockQuantity { get;set; }
        [DataMember(Name = "status")]
        [JsonPropertyName("status")]
        public string Status { get; set;}
    }
}
