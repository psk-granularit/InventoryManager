namespace InventoryManager.Models
{
    public class Config
    {
        // ERPNext connection
        public string ErpNextUrl { get; set; } = string.Empty;
        public string ErpNextApiKey { get; set; } = string.Empty;
        public string ErpNextApiSecret { get; set; } = string.Empty;

        // Primary: ERPNext warehouse names for main inventory
        public List<string> WarehousesToIncludeInInventory { get; set; } = new();

        // Named warehouses for special categories
        public string ShortExpiryWarehouse { get; set; } = string.Empty;
        public string DamagedGoodsWarehouse { get; set; } = string.Empty;

        // Fallback: numeric code → ERPNext warehouse name mapping
        public Dictionary<string, string> WarehouseCodeMapping { get; set; } = new();

        // Sync controls
        public int? ProductsToUpdate { get; set; }
        public int? ProductsToSkip { get; set; }

        // Reporting
        public List<string> SyncronizerReportReceivers { get; set; } = new();
    }
}
