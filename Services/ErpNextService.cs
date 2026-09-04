using InventoryManager.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace InventoryManager.Services
{
    /// <summary>
    /// Service that fetches product and stock data from ERPNext via REST API.
    /// Replaces the direct SQL Server queries to SAP Business One.
    /// </summary>
    public class ErpNextService
    {
        private readonly HttpClient _httpClient;
        private readonly Settings _settings;

        public ErpNextService(Settings settings)
        {
            _settings = settings;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.ErpNextUrl.TrimEnd('/') + "/")
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", $"{settings.ErpNextApiKey}:{settings.ErpNextApiSecret}");
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Fetches all web-enabled, active items from ERPNext and merges stock data
        /// from the specified warehouses. Returns a list comparable to the old QuerySapProducts().
        /// </summary>
        /// <param name="warehouses">
        /// List of ERPNext warehouse names to include in stock aggregation.
        /// If null, uses the default warehouses from settings.
        /// </param>
        public List<ErpNextProduct> GetProducts(List<string>? warehouses = null)
        {
            warehouses ??= _settings.ResolveWarehouses(_settings.WhsToIncludeInInventory);

            // Step 1: Fetch all active, web-enabled items
            var items = FetchItems();
            Console.WriteLine($"Fetched {items.Count} web-enabled items from ERPNext");

            // Step 2: Fetch stock levels (Bin records) for the specified warehouses
            var binEntries = FetchBinEntries(warehouses);
            Console.WriteLine($"Fetched {binEntries.Count} stock entries from ERPNext for warehouses: {string.Join(", ", warehouses)}");

            // Step 3: Aggregate stock by item_code (sum across warehouses)
            var stockByItem = binEntries
                .GroupBy(b => b.ItemCode)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(b => b.ActualQty)
                );

            // Step 4: Merge stock into items
            foreach (var item in items)
            {
                if (stockByItem.TryGetValue(item.ItemCode, out var qty))
                {
                    item.StockQty = qty;
                }
                else
                {
                    item.StockQty = 0;
                }
            }

            // Step 5: Apply pagination settings (skip/take)
            IEnumerable<ErpNextProduct> result = items;

            if (_settings.ProductsToSkip.HasValue)
            {
                result = result.Skip(_settings.ProductsToSkip.Value);
            }

            if (_settings.ProductsToUpdate.HasValue)
            {
                result = result.Take(_settings.ProductsToUpdate.Value);
            }

            return result.ToList();
        }

        /// <summary>
        /// Fetches all active items marked as web items from ERPNext Item doctype.
        /// Equivalent to the old SAP query: WHERE validFor='Y' AND U_WebItem='Y'
        /// </summary>
        private List<ErpNextProduct> FetchItems()
        {
            var allItems = new List<ErpNextProduct>();
            int pageSize = 500;
            int offset = 0;

            while (true)
            {
                var filters = "[[\"disabled\",\"=\",0],[\"custom_web_item\",\"=\",1]]";
                var fields = "[\"item_code\",\"item_name\",\"custom_is_bulk_pack\",\"custom_bulk_pack_qty\"]";

                var url = $"api/resource/Item?filters={Uri.EscapeDataString(filters)}" +
                          $"&fields={Uri.EscapeDataString(fields)}" +
                          $"&limit_page_length={pageSize}" +
                          $"&limit_start={offset}" +
                          $"&order_by=item_code asc";

                var response = _httpClient.GetAsync(url).Result;

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"ERPNext API error fetching items (HTTP {(int)response.StatusCode}): {errorContent}");
                }

                var jsonResult = response.Content.ReadAsStringAsync().Result;
                var apiResponse = JsonSerializer.Deserialize<ErpNextApiResponse<ErpNextProduct>>(jsonResult);

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                    break;

                allItems.AddRange(apiResponse.Data);

                if (apiResponse.Data.Count < pageSize)
                    break;

                offset += pageSize;
                Console.Write($"\rFetched {allItems.Count} items from ERPNext...");
            }

            return allItems;
        }

        /// <summary>
        /// Fetches stock levels from ERPNext Bin doctype for the specified warehouses.
        /// Equivalent to: SELECT ItemCode, sum(OnHand) FROM OITW WHERE WhsCode IN (...)
        /// </summary>
        private List<ErpNextBinEntry> FetchBinEntries(List<string> warehouses)
        {
            var allBins = new List<ErpNextBinEntry>();
            int pageSize = 500;
            int offset = 0;

            // Build the warehouse filter: ["warehouse","in",["Warehouse A","Warehouse B"]]
            var warehouseList = string.Join(",", warehouses.Select(w => $"\"{w}\""));
            var filters = $"[[\"warehouse\",\"in\",[{warehouseList}]],[\"actual_qty\",\">\",0]]";
            var fields = "[\"item_code\",\"actual_qty\",\"warehouse\"]";

            while (true)
            {
                var url = $"api/resource/Bin?filters={Uri.EscapeDataString(filters)}" +
                          $"&fields={Uri.EscapeDataString(fields)}" +
                          $"&limit_page_length={pageSize}" +
                          $"&limit_start={offset}";

                var response = _httpClient.GetAsync(url).Result;

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"ERPNext API error fetching bins (HTTP {(int)response.StatusCode}): {errorContent}");
                }

                var jsonResult = response.Content.ReadAsStringAsync().Result;
                var apiResponse = JsonSerializer.Deserialize<ErpNextApiResponse<ErpNextBinEntry>>(jsonResult);

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                    break;

                allBins.AddRange(apiResponse.Data);

                if (apiResponse.Data.Count < pageSize)
                    break;

                offset += pageSize;
            }

            return allBins;
        }
    }
}
