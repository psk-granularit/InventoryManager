using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManager
{
    public class Settings
    {
        public Settings() { }
        public List<string> WhsToIncludeInInventory { get; set; }
        public int? ProductsToUpdate { get; set; }
        public int? ProductsToSkip { get; set; }

        public string ProductsListEndpoint => "wp-json/wc/v3/products";
        public string ProductEndpoint => "wp-json/wc/v3/products";

        public List<string> SyncronizerReportReceivers { get; set; }

        public string FromEmail => "shop@petstore.co.ke";

#if DEBUG
        public string SiteUrl => "https://petstore.four.africa/";
        public string WooCommerceConsumerKey => "ck_8d03df024e9bacecc10468c20bc5c6c55f6f5719";
        public string WooCommerceConsumerSecret => "cs_844598c529655db084e9a1c7834e3eb125d21759";
        public string ConnectionString => "Server=MARTIN;Database=Loki_Live_DB21-2;User Id=sa;Password=Pa55word123;";


#else


        public string SiteUrl => "https://petstore.co.ke/";
        public string WooCommerceConsumerKey => "ck_8d03df024e9bacecc10468c20bc5c6c55f6f5719";
        public string WooCommerceConsumerSecret => "cs_844598c529655db084e9a1c7834e3eb125d21759";
        public bool IsTestMode => false;

        public string ConnectionString => "Server=192.168.0.121;Database=Loki_Live_DB21-2;User Id=sa;Password=2020YanTra;";

#endif
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
