using InventoryManager.Models;
using InventoryManager.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InventoryManager
{
    internal class Program
    {


        static void Main(string[] args)
        {
            var defaultConfigs = GetConfigs(args);

            var productsService = new ProductsService(defaultConfigs);

            Console.WriteLine("Inititating inventory sync");
            LogMessage($"[{DateTime.Now.ToString("yyyy-mm-dd-HH-mm")}]" + "Inititating inventory sync" );
            productsService.StartWatch();
            var status = false;
            try
            {
                status = productsService.UpdateStock();
            }
            catch(Exception ex)
            {
                LogMessage("[ERROR] " + ex.Message + Environment.NewLine + ex.StackTrace);
            }

            var elapsedMilliseconds = productsService.StopWatchAndReturnElapsedMilliseconds();

            TimeSpan t = TimeSpan.FromMilliseconds(elapsedMilliseconds);
            string timeSpanText = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
            string msg = $"[{DateTime.Now.ToString("yyyy-mm-dd-HH-mm")}]"  +  "Time taken to update products: " + timeSpanText;
            Console.WriteLine(msg);
            LogMessage(msg);

            if (status)
            {
                Console.WriteLine("All Products Updated Successfully");
            }
        }

        private static Config GetConfigs(string[] args)
        {
            var configs = new Config();
            try
            {
                using (StreamReader r = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "appsettings.json")))
                {
                    string text = r.ReadToEnd();
                    configs = JsonSerializer.Deserialize<Config>(text);
                }
            }
            catch
            {
                LogMessage("Could not read configuration settings for the application to function correctly. Kindly check your appsettings.json");
                Environment.Exit(1);
            }


            var validArgs = new List<string> { "ProductsToSkip", "ProductsToUpdate", "SyncronizerReportReceivers", "WarehousesToIncludeInInventory" };
            var counter = 0;
            foreach (var arg in args)
            {
                if (validArgs.Any(x => x.ToLower().Equals(arg.ToLower())))
                {
                    switch (arg.ToLower())
                    {
                        case "productstoskip":
                            var stringValue = args[counter + 1];
                            var isvalid = int.TryParse(stringValue, out int value);
                            configs.ProductsToSkip = isvalid ? value : configs.ProductsToSkip;
                            break;
                        case "productstoupdate":
                            string strValue = args[counter + 1];
                            bool isvalidNum = int.TryParse(strValue, out int productsToUpdate);
                            configs.ProductsToUpdate = isvalidNum ? productsToUpdate : configs.ProductsToUpdate;
                            break;
                        case "syncronizerreportreceivers":

                            var emails = args[counter + 1];
                            if (!string.IsNullOrWhiteSpace(emails))
                            {
                                var emailsList = emails.Split(',');

                                var validEmails = emailsList.Where(x => IsValidEmail(x)).ToList();

                                if (validEmails.Any())
                                {
                                    configs.SyncronizerReportReceivers = validEmails;
                                }
                                else
                                {
                                    Console.WriteLine("Kindly check your list of report receivers and ensure it is correct");
                                    Environment.Exit(1);

                                }

                            }
                            break;
                        case "warehousestoincludeininventory":
                            var whss = args[counter + 1];
                            var validWhsNumbers = new List<string> { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" };
                            if (!string.IsNullOrWhiteSpace(whss))
                            {
                                var emailsList = whss.Split(',');

                                var valisWhss = emailsList.Where(x => validWhsNumbers.Contains(x)).ToList();

                                if (valisWhss.Any())
                                {
                                    configs.WarehousesToIncludeInInventory = valisWhss;
                                }
                                else
                                {
                                    Console.WriteLine("Kindly check your list of warehouses");
                                    Environment.Exit(1);
                                }

                            }
                            break;
                    }
                }
                counter++;
            }
            

            return configs;
        }
        private static bool IsValidEmail(string email)
        {
            string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, pattern);
        }
        private static void LogMessage(string messsage)
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
