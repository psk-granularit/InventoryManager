using InventoryManager.Common;
using InventoryManager.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace InventoryManager.Services
{
    public class ProductsService
    {
        private EmailService _emailService;
        private Settings _settings;
        public ProductsService(Config config)
        {
            _emailService = new EmailService(config);
            _settings = new Settings();

            _settings.WhsToIncludeInInventory = config.WarehousesToIncludeInInventory;
            _settings.ProductsToSkip = config.ProductsToSkip;
            _settings.ProductsToUpdate = config.ProductsToUpdate;
            _settings.SyncronizerReportReceivers = config.SyncronizerReportReceivers;


        }

        private HttpClient HttpClient => new HttpClient();

        private Stopwatch Watch;

        public bool StartWatch()
        {
            Watch = Stopwatch.StartNew();
            return true;
        }
        public double StopWatchAndReturnElapsedMilliseconds()
        {
            Watch.Stop();
            return (double)Watch.ElapsedMilliseconds;
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

        public List<SapProduct> QuerySapProducts(string warehousesNumbers = null)
        {
            List<SapProduct> products = new List<SapProduct>();

            using (SqlConnection connection = new SqlConnection(_settings.ConnectionString))
            {

                String sql = GetAllProductsSqlString(warehousesNumbers);

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SapProduct item = ReflectPropertyInfo.ReflectType<SapProduct>(reader);
                            products.Add(item);
                        }
                    }
                    connection.Close();
                }
            }
            if (_settings.ProductsToUpdate != null)
            {
                return products.Take((int)_settings.ProductsToUpdate).ToList();

            }
            return products;
        }

        private string GetAllProductsSqlString(string wareHousesString = null)
        {

            if (string.IsNullOrEmpty(wareHousesString))
            {
                foreach (var whs in _settings.WhsToIncludeInInventory)
                {
                    wareHousesString += "'" + whs + "',";
                }

                wareHousesString = wareHousesString.Trim(',');
            }

            var sql = $"SELECT Q.*,ISNULL( QB.U_BlPack, 'N' ) as IsBlPackItem,ISNULL(QB.U_BlQty, 0 ) as BlPackQuantity, QB.U_BlWebId as BlPackWebId FROM (Select a.ItemCode,ItemName,sum (b.OnHand ) OnHand  from OITM a  inner  join oitw b on a.itemcode = b.itemcode left join OWHS whs on whs.WhsCode = b.WhsCode  Where whs.WhsCode in ({wareHousesString}) and a.validFor='Y'  and a.U_WebItem='Y' group by a.ItemCode, ItemName ) Q left join OITM QB on QB.ItemCode = Q.ItemCode order by Q.ItemCode";

            if (_settings.ProductsToSkip != null)
            {
                sql += $"  offset {_settings.ProductsToSkip} rows";
            }
            return sql;
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

            var sapProducts = new List<SapProduct> { };
            try
            {
                sapProducts = QuerySapProducts();

            }
            catch (Exception ex)
            {
                LogMessage("[ERROR]: Unable to pull sap products. ErrorMessage: " + ex.Message);
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


            foreach (SapProduct sapProduct in sapProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == sapProduct.ItemCode);

                Product wooCommerceBulkProduct = null;

                if (sapProduct.IsBlPackItem == "Y")
                {
                    wooCommerceBulkProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku != null && (prod.Sku.Contains(sapProduct.ItemCode)) && (prod.Sku.Contains("$")) && int.TryParse(prod.Sku.Substring(prod.Sku.IndexOf("$") + 1), out int bultQty));
                    if (wooCommerceBulkProduct == null)
                    {
                        msg = $"The product {sapProduct.ItemName} ({sapProduct.ItemCode}) is missing a matching bulk quantity product on ecommerce.";
                        missingEcommerceBulkProducts.Add($"{sapProduct.ItemCode} - {sapProduct.ItemName}");
                        missingEcommerceBulkCount++;
                        Console.WriteLine(msg);
                        LogMessage(msg);
                    }
                }

                if (wooCommerceProduct != null || wooCommerceBulkProduct != null)
                {
                    if (wooCommerceProduct != null && sapProduct?.StockQty != wooCommerceProduct?.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = sapProduct.StockQty, status = wooCommerceProduct.Status});
                        productCodes.Add(sapProduct.ItemCode);
                     
                    }
                    if (wooCommerceBulkProduct != null)
                    {
                        var stockQty = GetBulkItemStockQuantity(sapProduct);

                        if (stockQty != wooCommerceBulkProduct.StockQuantity)
                        {
                            updateList.update.Add(new ProductUpdateModel { id = wooCommerceBulkProduct.Id, stock_quantity = stockQty , status = wooCommerceBulkProduct.Status});
                            productCodes.Add(wooCommerceBulkProduct.Sku);
                        }

                    }
                }
                else
                {
                    msg = $"The product {sapProduct.ItemName} ({sapProduct.ItemCode}) is missing matching product(s) on ecommerce.";
                    missingEcommerceProducts.Add($"{sapProduct.ItemCode} - {sapProduct.ItemName}");
                    missingEcomerceCount++;
                    Console.WriteLine(msg);
                    LogMessage(msg);
                }

                if (updateList.update.Count >= 9 || counter == (sapProducts.Count() - 1))
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

            var sapProducts = new List<SapProduct> { };
            try
            {
                sapProducts = QuerySapProducts("'04'");

            }
            catch (Exception ex)
            {
                LogMessage("[ERROR]: Unable to pull sap products. ErrorMessage: " + ex.Message);
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

            foreach (SapProduct sapProduct in sapProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == sapProduct.ItemCode + "@SE");

                if (wooCommerceProduct != null)
                {
                    if (sapProduct.StockQty <= 0 && wooCommerceProduct.Status != "draft")
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = sapProduct.StockQty , status = "draft"});
                        productCodes.Add(sapProduct.ItemCode);
                    }
                    else if (wooCommerceProduct != null && sapProduct.StockQty != wooCommerceProduct.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = sapProduct.StockQty , status = "publish" });
                        productCodes.Add(sapProduct.ItemCode);
                    }
                    updatedSeProducts.Add(sapProduct.ItemCode);
                }
               

                if (updateList.update.Count >= 10 || counter == (sapProducts.Count() - 1))
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

            var sapSeProductsWithInventory = sapProducts.Where(prod => prod.StockQty > 0);
            var sapSeProductsNotUpdated = sapSeProductsWithInventory.Where(x => !updatedSeProducts.Contains(  x.ItemCode));
            foreach (var seProd in sapSeProductsNotUpdated)
            {
                missingShortExpiryProducts.Add($"{seProd.ItemCode} - {seProd.ItemName}");
            }
            return missingShortExpiryProducts;
        }
        private List<string> UpdateDamagedGoodsInventory(List<Product> wooCommerceProducts)
        {
            var missingDamagedProducts = new List<string> { };

            var sapProducts = new List<SapProduct> { };
            try
            {
                sapProducts = QuerySapProducts("'02'");

            }
            catch (Exception ex)
            {
                LogMessage("[ERROR]: Unable to pull sap products. ErrorMessage: " + ex.Message);
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

            foreach (SapProduct sapProduct in sapProducts)
            {
                var wooCommerceProduct = wooCommerceProducts.FirstOrDefault(prod => prod.Sku == sapProduct.ItemCode + "@DE");

                if (wooCommerceProduct != null)
                {
                    if (sapProduct.StockQty <= 0 && wooCommerceProduct.Status != "draft")
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = sapProduct.StockQty, status = "draft" });
                        productCodes.Add(sapProduct.ItemCode);
                    }
                    else if (wooCommerceProduct != null && sapProduct.StockQty != wooCommerceProduct.StockQuantity)
                    {
                        updateList.update.Add(new ProductUpdateModel { id = wooCommerceProduct.Id, stock_quantity = sapProduct.StockQty, status = "publish" });
                        productCodes.Add(sapProduct.ItemCode);
                    }
                    updatedDamagedProducts.Add(sapProduct.ItemCode);
                }


                if (updateList.update.Count >= 10 || counter == (sapProducts.Count() - 1))
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

            var sapDamagedProductsWithInventory = sapProducts.Where(prod => prod.StockQty > 0);
            var sapDamagedProductsNotUpdated = sapDamagedProductsWithInventory.Where(x => !updatedDamagedProducts.Contains(x.ItemCode));
            foreach (var seProd in sapDamagedProductsNotUpdated)
            {
                missingDamagedProducts.Add($"{seProd.ItemCode} - {seProd.ItemName}");
            }
            return missingDamagedProducts;

        }
        private decimal GetBulkItemStockQuantity(SapProduct sapProduct)
        {
            var quantity = sapProduct?.StockQty ?? 0;

            if (sapProduct.IsBlPackItem == "Y" && (sapProduct?.BlPackQuantity ?? 0) > 1)
            {
                quantity = ((int)(sapProduct?.StockQty ?? 0)) / (sapProduct?.BlPackQuantity ?? 0);
            }

            return quantity;
        }
        public List<Product> GetProductsFromJson(string content)
        {

            return JsonSerializer.Deserialize<List<Product>>(content);
        }
        public Product GetProductFromJson(string content)
        {

            return JsonSerializer.Deserialize<List<Product>>(content).FirstOrDefault();
        }
        public void LogMessage(string messsage)
        {
            var dateToday = DateTime.Now;
            var filename = $"{dateToday.Year}-{dateToday.Month}-{dateToday.Day}-log.txt";

            string filePath = System.IO.Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), filename);

            using (StreamWriter sw = File.AppendText(filePath))
            {
                sw.WriteLine(messsage);
                sw.Close();
            }

        }
    }
}
