using InventoryManager.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace InventoryManager.Services
{
    public class ProductsService
    {
        private readonly EmailService _emailService;
        private readonly Settings _settings;
        private readonly ErpNextService _erpNextService;

        public ProductsService(Config config)
        {
            _emailService = new EmailService(config);
            _settings = new Settings();

            _settings.WhsToIncludeInInventory = config.WarehousesToIncludeInInventory;
            _settings.ProductsToSkip = config.ProductsToSkip;
            _settings.ProductsToUpdate = config.ProductsToUpdate;
            _settings.SyncronizerReportReceivers = config.SyncronizerReportReceivers;

            // ERPNext configuration
            _settings.ErpNextUrl = config.ErpNextUrl;
            _settings.ErpNextApiKey = config.ErpNextApiKey;
            _settings.ErpNextApiSecret = config.ErpNextApiSecret;
            _settings.ShortExpiryWarehouse = config.ShortExpiryWarehouse;
            _settings.DamagedGoodsWarehouse = config.DamagedGoodsWarehouse;
            _settings.WarehouseCodeMapping = config.WarehouseCodeMapping;

            _erpNextService = new ErpNextService(_settings);
        }

        private HttpClient HttpClient => new HttpClient();

        private Stopwatch? Watch;

        public bool StartWatch()
        {
            Watch = Stopwatch.StartNew();
            return true;
        }
        public double StopWatchAndReturnElapsedMilliseconds()
        {
            Watch?.Stop();
            return (double)(Watch?.ElapsedMilliseconds ?? 0);
        }
        public List<Product> GetAllWooCommerceProducts()
        {
            var allProducts = new List<Product>();

            var allListed = false;
            var offset = 0;
            Console.WriteLine("Fetching woocommerce products...");

            do
            {
                var url = _settings.GetAllProductsUrlWithOffset(offset);

                var result = HttpClient.GetAsync(url).Result;
                var jsonResult = result.Content.ReadAsStringAsync().Result;
                var products = GetProductsFromJson(jsonResult);

                if (products.Count < 100)
                {
                    allListed = true;
                }

                allProducts.AddRange(products);
                offset += 100;
                var msg = $"\rFetched a total of {allProducts.Count} products from WooCommerce";
                Console.Write(msg);

            } while (!allListed);
            Console.WriteLine("Done Fetching products");
            return allProducts;
        }

        /// <summary>
        /// Fetches products with stock from ERPNext for the specified warehouses.
        /// Replaces the old QuerySapProducts() which used direct SQL against SAP.
        /// </summary>
        /// <param name="warehouses">
        /// List of warehouse names or codes. If null, uses the default warehouses from config.
        /// Supports both ERPNext warehouse names and old numeric codes via the fallback mapping.
        /// </param>
        public List<ErpNextProduct> QueryErpNextProducts(List<string>? warehouses = null)
        {
            return _erpNextService.GetProducts(warehouses);
        }

        public bool BatchUpdateProducts(ProductBatchUpdateModel model)
        {
            var url = _settings.GetProductsBatchUpdateUrl();

            var serializedContent = JsonSerializer.Serialize(model);

            var stringContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");

            var result = HttpClient.PostAsync(url, stringContent).Result;
            if (!result.IsSuccessStatusCode)
            {
                var response = result.Content.ReadAsStringAsync().Result;
                var msg = "Batch update failed with result:: " + response + " REQUEST::: " + stringContent;
                Console.WriteLine(msg);
                LogMessage(msg);

                return false;
            }

            return true;
        }
        public bool UpdateStock()
        {
            var wooCommerceProducts = GetAllWooCommerceProducts();

            var erpNextProducts = new List<ErpNextProduct> { };
            try
            {
                erpNextProducts = QueryErpNextProducts();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR]: Unable to pull ERPNext products: " + ex.Message);
                LogMessage("[ERROR]: Unable to pull ERPNext products. ErrorMessage: " + ex.Message);
            }


            var updateList = new ProductBatchUpdateModel
            {
                update = new List<ProductUpdateModel> { }
            };
            var productCodes = new List<string> { };

            var counter = 0;
            var updatesCounter = 1;
            var totalProductsUpdated = 0;

            var missingEcommerceProducts = new List<string> { };
            var missingEcommerceBulkProducts = new List<string> { };

            var missingEcomerceCount = 0;
            var missingEcommerceBulkCount = 0;

            var msg = "";


            foreach (ErpNextProduct erpProduct in erpNextProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == erpProduct.ItemCode);

                Product? wooCommerceBulkProduct = null;

                if (erpProduct.IsBlPackItem == "Y")
                {
                    wooCommerceBulkProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku != null && (prod.Sku.Contains(erpProduct.ItemCode)) && (prod.Sku.Contains("$")) && int.TryParse(prod.Sku.Substring(prod.Sku.IndexOf("$") + 1), out int bultQty));
                    if (wooCommerceBulkProduct == null)
                    {
                        msg = $"The product {erpProduct.ItemName} ({erpProduct.ItemCode}) is missing a matching bulk quantity product on ecommerce.";
                        missingEcommerceBulkProducts.Add($"{erpProduct.ItemCode} - {erpProduct.ItemName}");
                        missingEcommerceBulkCount++;
                        Console.WriteLine(msg);
                        LogMessage(msg);
                    }
                }

                if (wooCommerceProduct != null || wooCommerceBulkProduct != null)
                {
                    if (wooCommerceProduct != null && erpProduct.StockQty != wooCommerceProduct.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = erpProduct.StockQty, status = wooCommerceProduct.Status});
                        productCodes.Add(erpProduct.ItemCode);
                     
                    }
                    if (wooCommerceBulkProduct != null)
                    {
                        var stockQty = GetBulkItemStockQuantity(erpProduct);

                        if (stockQty != wooCommerceBulkProduct.StockQuantity)
                        {
                            updateList.update.Add(new ProductUpdateModel { id = wooCommerceBulkProduct.Id, stock_quantity = stockQty , status = wooCommerceBulkProduct.Status});
                            productCodes.Add(wooCommerceBulkProduct.Sku);
                        }

                    }
                }
                else
                {
                    msg = $"The product {erpProduct.ItemName} ({erpProduct.ItemCode}) is missing matching product(s) on ecommerce.";
                    missingEcommerceProducts.Add($"{erpProduct.ItemCode} - {erpProduct.ItemName}");
                    missingEcomerceCount++;
                    Console.WriteLine(msg);
                    LogMessage(msg);
                }

                if (updateList.update.Count >= 9 || counter == (erpNextProducts.Count() - 1))
                {
                    if (!updateList.update.Any())
                    {
                        continue;
                    }

                    msg = "Update Number: [" + (updatesCounter) + "] Products To Update: " + string.Join(",", productCodes);
                    Console.WriteLine(msg);
                    LogMessage(msg);

                    totalProductsUpdated += updateList.update.Count();
                    BatchUpdateProducts(updateList);

                    updateList.update.Clear();
                    productCodes.Clear();

                    updatesCounter++;
                }

                counter++;
            }


            try
            {
                LogMessage($"Missing items: {missingEcomerceCount}");
                LogMessage($"Missing bulk items: {missingEcommerceBulkCount}");

                _emailService.SendSyncronizerReportEmail(missingEcommerceProducts, missingEcommerceBulkProducts, missingEcomerceCount, missingEcommerceBulkCount);

            }
            catch (Exception ex)
            {
                LogMessage("[ERROR SENDING EMAIL] " + ex.Message + Environment.NewLine + ex.StackTrace);

            }

            msg = "Total Number Of products updated:: " + totalProductsUpdated;
            Console.WriteLine(msg);
            LogMessage(msg);
            return true;
        }
        private List<string> UpdateShortExpiryInventory(List<Product> wooCommerceProducts)
        {
            var missingShortExpiryProducts = new List<string> { };

            var erpNextProducts = new List<ErpNextProduct> { };
            try
            {
                var shortExpiryWarehouse = _settings.ResolveWarehouse(_settings.ShortExpiryWarehouse);
                erpNextProducts = QueryErpNextProducts(new List<string> { shortExpiryWarehouse });
            }
            catch (Exception ex)
            {
                LogMessage("[ERROR]: Unable to pull ERPNext products for short expiry. ErrorMessage: " + ex.Message);
            }


            var updateList = new ProductBatchUpdateModel
            {
                update = new List<ProductUpdateModel> { }
            };

            var productCodes = new List<string> { };

            var counter = 0;
            var updatesCounter = 1;
            var totalProductsUpdated = 0;
            var missingSeProductsText = new List<string> { };
            var missingSeBulkProducts = new List<string> { };
            var updatedSeProducts = new List<string> { };
            var msg = "";

            foreach (ErpNextProduct erpProduct in erpNextProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == erpProduct.ItemCode + "@SE");

                if (wooCommerceProduct != null)
                {
                    if (erpProduct.StockQty <= 0 && wooCommerceProduct.Status != "draft")
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = erpProduct.StockQty , status = "draft"});
                        productCodes.Add(erpProduct.ItemCode);
                    }
                    else if (wooCommerceProduct != null && erpProduct.StockQty != wooCommerceProduct.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = erpProduct.StockQty , status = "publish" });
                        productCodes.Add(erpProduct.ItemCode);
                    }
                    updatedSeProducts.Add(erpProduct.ItemCode);
                }
               

                if (updateList.update.Count >= 10 || counter == (erpNextProducts.Count() - 1))
                {
                    if (!updateList.update.Any())
                    {
                        continue;
                    }

                    msg = "Short Expiry Update Number: [" + (updatesCounter) + "] Products To Update: " + string.Join(",", productCodes);

                    Console.WriteLine(msg);
                    LogMessage(msg);

                    totalProductsUpdated += updateList.update.Count();
                    BatchUpdateProducts(updateList);

                    updateList.update.Clear();
                    productCodes.Clear();

                    updatesCounter++;
                }

                counter++;
            }

            msg = "Total Number Of SE products updated:: " + totalProductsUpdated;
            Console.WriteLine(msg);
            LogMessage(msg);

            var erpSeProductsWithInventory = erpNextProducts.Where(prod => prod.StockQty > 0);
            var erpSeProductsNotUpdated = erpSeProductsWithInventory.Where(x => !updatedSeProducts.Contains(  x.ItemCode));
            foreach (var seProd in erpSeProductsNotUpdated)
            {
                missingShortExpiryProducts.Add($"{seProd.ItemCode} - {seProd.ItemName}");
            }
            return missingShortExpiryProducts;
        }
        private List<string> UpdateDamagedGoodsInventory(List<Product> wooCommerceProducts)
        {
            var missingDamagedProducts = new List<string> { };

            var erpNextProducts = new List<ErpNextProduct> { };
            try
            {
                var damagedWarehouse = _settings.ResolveWarehouse(_settings.DamagedGoodsWarehouse);
                erpNextProducts = QueryErpNextProducts(new List<string> { damagedWarehouse });
            }
            catch (Exception ex)
            {
                LogMessage("[ERROR]: Unable to pull ERPNext products for damaged goods. ErrorMessage: " + ex.Message);
            }


            var updateList = new ProductBatchUpdateModel
            {
                update = new List<ProductUpdateModel> { }
            };

            var productCodes = new List<string> { };

            var counter = 0;
            var updatesCounter = 1;
            var totalProductsUpdated = 0;
            var missingDamagedProductsText = new List<string> { };
            var missingDamagedBulkProducts = new List<string> { };
            var updatedDamagedProducts = new List<string> { };
            var msg = "";

            foreach (ErpNextProduct erpProduct in erpNextProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == erpProduct.ItemCode + "@DE");

                if (wooCommerceProduct != null)
                {
                    if (erpProduct.StockQty <= 0 && wooCommerceProduct.Status != "draft")
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = erpProduct.StockQty, status = "draft" });
                        productCodes.Add(erpProduct.ItemCode);
                    }
                    else if (wooCommerceProduct != null && erpProduct.StockQty != wooCommerceProduct.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = erpProduct.StockQty, status = "publish" });
                        productCodes.Add(erpProduct.ItemCode);
                    }
                    updatedDamagedProducts.Add(erpProduct.ItemCode);
                }


                if (updateList.update.Count >= 10 || counter == (erpNextProducts.Count() - 1))
                {
                    if (!updateList.update.Any())
                    {
                        continue;
                    }

                    msg = "Damaged goods Update Number: [" + (updatesCounter) + "] Products To Update: " + string.Join(",", productCodes);

                    Console.WriteLine(msg);
                    LogMessage(msg);

                    totalProductsUpdated += updateList.update.Count();
                    BatchUpdateProducts(updateList);

                    updateList.update.Clear();
                    productCodes.Clear();

                    updatesCounter++;
                }

                counter++;
            }

            msg = "Total Number Of Damaged products updated:: " + totalProductsUpdated;
            Console.WriteLine(msg);
            LogMessage(msg);

            var erpDamagedProductsWithInventory = erpNextProducts.Where(prod => prod.StockQty > 0);
            var erpDamagedProductsNotUpdated = erpDamagedProductsWithInventory.Where(x => !updatedDamagedProducts.Contains(x.ItemCode));
            foreach (var seProd in erpDamagedProductsNotUpdated)
            {
                missingDamagedProducts.Add($"{seProd.ItemCode} - {seProd.ItemName}");
            }
            return missingDamagedProducts;

        }
        private decimal GetBulkItemStockQuantity(ErpNextProduct erpProduct)
        {
            var quantity = erpProduct.StockQty;

            if (erpProduct.IsBlPackItem == "Y" && erpProduct.BlPackQuantity > 1)
            {
                quantity = ((int)erpProduct.StockQty) / erpProduct.BlPackQuantity;
            }

            return quantity;
        }
        public List<Product> GetProductsFromJson(string content)
        {

            return JsonSerializer.Deserialize<List<Product>>(content) ?? new List<Product>();
        }
        public Product? GetProductFromJson(string content)
        {

            return JsonSerializer.Deserialize<List<Product>>(content)?.FirstOrDefault();
        }
        public void LogMessage(string messsage)
        {
            var dateToday = DateTime.Now;
            var filename = $"{dateToday.Year}-{dateToday.Month}-{dateToday.Day}-log.txt";

            string filePath = System.IO.Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, filename);

            using (StreamWriter sw = File.AppendText(filePath))
            {
                sw.WriteLine(messsage);
                sw.Close();
            }

        }
    }
}
