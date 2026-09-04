using System.Text.Json.Serialization;

namespace InventoryManager.Models
{
    /// <summary>
    /// Represents a product from ERPNext (replaces SapProduct).
    /// Maps to the ERPNext Item doctype fields + computed stock from Bin.
    /// </summary>
    public class ErpNextProduct
    {
        [JsonPropertyName("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [JsonPropertyName("item_name")]
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Computed field: sum of actual_qty from Bin records across configured warehouses.
        /// Not directly deserialized from ERPNext — populated by ErpNextService.
        /// </summary>
        public decimal StockQty { get; set; }

        /// <summary>
        /// Raw value from ERPNext custom field (1 = yes, 0 = no).
        /// </summary>
        [JsonPropertyName("custom_is_bulk_pack")]
        public int IsBlPackItemRaw { get; set; }

        /// <summary>
        /// Backward-compatible property: returns "Y" or "N" to maintain
        /// existing business logic in ProductsService without changes.
        /// </summary>
        public string IsBlPackItem => IsBlPackItemRaw == 1 ? "Y" : "N";

        [JsonPropertyName("custom_bulk_pack_qty")]
        public int BlPackQuantity { get; set; }
    }

    /// <summary>
    /// Represents a stock entry from ERPNext's Bin doctype.
    /// Used internally by ErpNextService to aggregate stock per item.
    /// </summary>
    public class ErpNextBinEntry
    {
        [JsonPropertyName("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [JsonPropertyName("actual_qty")]
        public decimal ActualQty { get; set; }

        [JsonPropertyName("warehouse")]
        public string Warehouse { get; set; } = string.Empty;
    }
}
