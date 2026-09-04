namespace InventoryManager
{
    public class Settings
    {
        public Settings() { }

        // ERPNext connection
        public string ErpNextUrl { get; set; } = string.Empty;
        public string ErpNextApiKey { get; set; } = string.Empty;
        public string ErpNextApiSecret { get; set; } = string.Empty;

        // Warehouse configuration
        public List<string> WhsToIncludeInInventory { get; set; } = new();
        public string ShortExpiryWarehouse { get; set; } = string.Empty;
        public string DamagedGoodsWarehouse { get; set; } = string.Empty;
        public Dictionary<string, string> WarehouseCodeMapping { get; set; } = new();

        // Sync controls
        public int? ProductsToUpdate { get; set; }
        public int? ProductsToSkip { get; set; }

        // WooCommerce endpoints
        public string ProductsListEndpoint => "wp-json/wc/v3/products";
        public string ProductEndpoint => "wp-json/wc/v3/products";

        // Reporting
        public List<string> SyncronizerReportReceivers { get; set; } = new();
        public string FromEmail => "shop@petstore.co.ke";

#if DEBUG
        public string SiteUrl => "https://petstore.four.africa/";
        public string WooCommerceConsumerKey => "ck_8d03df024e9bacecc10468c20bc5c6c55f6f5719";
        public string WooCommerceConsumerSecret => "cs_844598c529655db084e9a1c7834e3eb125d21759";
#else
        public string SiteUrl => "https://petstore.co.ke/";
        public string WooCommerceConsumerKey => "ck_8d03df024e9bacecc10468c20bc5c6c55f6f5719";
        public string WooCommerceConsumerSecret => "cs_844598c529655db084e9a1c7834e3eb125d21759";
        public bool IsTestMode => false;
#endif

        /// <summary>
        /// Resolves warehouse identifiers to ERPNext warehouse names.
        /// If the value looks like an old numeric code (e.g. "01"), it is resolved
        /// using the WarehouseCodeMapping dictionary. Otherwise, it is assumed to
        /// already be an ERPNext warehouse name and passed through as-is.
        /// </summary>
        public List<string> ResolveWarehouses(List<string> warehouseIdentifiers)
        {
            var resolved = new List<string>();

            foreach (var identifier in warehouseIdentifiers)
            {
                if (WarehouseCodeMapping.TryGetValue(identifier, out var erpNextName))
                {
                    // Numeric code fallback — resolve to ERPNext name
                    resolved.Add(erpNextName);
                }
                else
                {
                    // Already an ERPNext warehouse name — use as-is
                    resolved.Add(identifier);
                }
            }

            return resolved;
        }

        /// <summary>
        /// Resolves a single warehouse identifier (name or code) to an ERPNext warehouse name.
        /// </summary>
        public string ResolveWarehouse(string warehouseIdentifier)
        {
            if (string.IsNullOrWhiteSpace(warehouseIdentifier))
                return warehouseIdentifier;

            if (WarehouseCodeMapping.TryGetValue(warehouseIdentifier, out var erpNextName))
                return erpNextName;

            return warehouseIdentifier;
        }

        public string GetAllProductsUrlWithOffset(int offset = 0)
        {
            return $"{SiteUrl}{ProductsListEndpoint}?_fields=id,name,price,regular_price,stock_quantity,sku,status&offset={offset}&per_page=100&consumer_key={WooCommerceConsumerKey}&consumer_secret={WooCommerceConsumerSecret}";
        }

        public string GetProductUrl(string sku)
        {
            return $"{SiteUrl}{ProductsListEndpoint}?sku={sku}&consumer_key={WooCommerceConsumerKey}&consumer_secret={WooCommerceConsumerSecret}";
        }

        public string GetProductsBatchUpdateUrl()
        {
            return $"{SiteUrl}{ProductsListEndpoint}/batch?consumer_key={WooCommerceConsumerKey}&consumer_secret={WooCommerceConsumerSecret}";
        }
    }
}
