using InventoryManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManager.Services
{
    public class EmailService

    {
        public EmailService(Config config)
        {
            _settings = new Settings();

            _settings.WhsToIncludeInInventory = config.WarehousesToIncludeInInventory;
            _settings.ProductsToSkip = config.ProductsToSkip;
            _settings.ProductsToUpdate = config.ProductsToUpdate;
            _settings.SyncronizerReportReceivers = config.SyncronizerReportReceivers;

        }
        private Settings _settings;

        public void SendMail(List<string> to, string from, string subject, string body, string filePath)
        {
            MailMessage message = new MailMessage();
            message.From = new MailAddress(from);
            foreach (var receiver in to)
            {
                message.To.Add(receiver);
            }
            message.Subject = subject;
            message.IsBodyHtml = true;
            message.Body = body;

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    message.Attachments.Add(new Attachment(filePath));

                }
                catch (Exception ex)
                {

                }
            }

            SmtpClient smtpClient = new SmtpClient();
            smtpClient.Host = "mail.smtp2go.com";
            smtpClient.Port = 2525;
            smtpClient.Credentials = new NetworkCredential
            {
                UserName = "loki-ventures.com",
                Password = "yMcWXFJMkEWm1UUs"
            };

            smtpClient.Send(message);


        }
        public void SendSyncronizerReportEmail(List<string> missingEcommerceProducts, List<string> missingEcommerceBulkProducts,int missingEcomerceCount,int missingEcommerceBulkCount)
        {
            var body = GetReportEmailBody(missingEcommerceProducts, missingEcommerceBulkProducts, missingEcomerceCount, missingEcommerceBulkCount);

            var dateToday = DateTime.Now;
            //var filename = $"{dateToday.Year}-{dateToday.Month}-{dateToday.Day}-log.txt";

            //string filePath = System.IO.Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), filename);
            //var fileExists = File.Exists(filePath);

            SendMail(_settings.SyncronizerReportReceivers, _settings.FromEmail, "Stock Syncroniser Report", body, null);
        }
        public string GetReportEmailBody(List<string> missingEcommerceProducts, List<string> missingEcommerceBulkProducts, int missingEcomerceCount, int missingEcommerceBulkCount)
        {
            var missingEcommerceProductsBody = "";
            if (missingEcommerceProducts.Any())
            {
                foreach (var prod in missingEcommerceProducts)
                {
                    missingEcommerceProductsBody += $"<tr> <td> {prod} </td> </tr>";
                }

                missingEcommerceProductsBody = $"<br><p>The following products ({missingEcomerceCount} products) are missing on WooComerce:- </p><table role='presentation' border='0' cellpadding='0' cellspacing='0'                    <tbody>\r\n                      <tr>\r\n                        <td align='left'>\r\n                          <table role='presentation' border='0' cellpadding='0' cellspacing='0'>\r\n                            <tbody>        {missingEcommerceProductsBody}                     </tbody>\r\n                          </table>\r\n                        </td>\r\n                      </tr>\r\n                    </tbody>\r\n                  </table>";
            }
            else
            {
                missingEcommerceProductsBody = "<br><p>No missing products on WooComerce</> <br>";
            }
            var missingEcommerceBulkProductsBody = "";
            if (missingEcommerceBulkProducts.Any())
            {
                foreach (var prod in missingEcommerceBulkProducts)
                {
                    missingEcommerceBulkProductsBody += $"<tr> <td> {prod} </td> </tr>";
                }

                missingEcommerceBulkProductsBody = $"<br><p>The following products ({missingEcommerceBulkCount} products) are missing matching bulk products on WooComerce:- </p><table role='presentation' border='0' cellpadding='0' cellspacing='0'>                   <tbody>\r\n                      <tr>\r\n                        <td align='left'>\r\n                          <table role='presentation' border='0' cellpadding='0' cellspacing='0'>\r\n                            <tbody>        {missingEcommerceBulkProductsBody}                     </tbody>\r\n                          </table>\r\n                        </td>\r\n                      </tr>\r\n                    </tbody>\r\n                  </table>";
            }
            else
            {
                missingEcommerceBulkProductsBody = "<br> <p>No missing bulk products on WooComerce</> <br>";

            }
           
           
            var emailBody = missingEcommerceProductsBody + missingEcommerceBulkProductsBody;

            var body = $"\r\n<!doctype html>\r\n<html>\r\n  <head>\r\n    <meta name='viewport' content='width=device-width, initial-scale=1.0'>\r\n    <meta http-equiv='Content-Type' content='text/html; charset=UTF-8'>\r\n    <title>Simple Transactional Email</title>\r\n    <style media='all' type='text/css'>\r\n    /* -------------------------------------\r\n    GLOBAL RESETS\r\n------------------------------------- */\r\n    \r\n    body {{\r\n      font-family: Helvetica, sans-serif;\r\n      -webkit-font-smoothing: antialiased;\r\n      font-size: 16px;\r\n      line-height: 1.3;\r\n      -ms-text-size-adjust: 100%;\r\n      -webkit-text-size-adjust: 100%;\r\n    }}\r\n    \r\n    table {{\r\n      border-collapse: separate;\r\n      mso-table-lspace: 0pt;\r\n      mso-table-rspace: 0pt;\r\n      width: 100%;\r\n    }}\r\n    \r\n    table td {{\r\n      font-family: Helvetica, sans-serif;\r\n      font-size: 16px;\r\n      vertical-align: top;\r\n    }}\r\n    /* -------------------------------------\r\n    BODY & CONTAINER\r\n------------------------------------- */\r\n    \r\n    body {{\r\n      background-color: #f4f5f6;\r\n      margin: 0;\r\n      padding: 0;\r\n    }}\r\n    \r\n    .body {{\r\n      background-color: #f4f5f6;\r\n      width: 100%;\r\n    }}\r\n    \r\n    .container {{\r\n      margin: 0 auto !important;\r\n      max-width: 600px;\r\n      padding: 0;\r\n      padding-top: 24px;\r\n      width: 600px;\r\n    }}\r\n    \r\n    .content {{\r\n      box-sizing: border-box;\r\n      display: block;\r\n      margin: 0 auto;\r\n      max-width: 600px;\r\n      padding: 0;\r\n    }}\r\n    /* -------------------------------------\r\n    HEADER, FOOTER, MAIN\r\n------------------------------------- */\r\n    \r\n    .main {{\r\n      background: #ffffff;\r\n      border: 1px solid #eaebed;\r\n      border-radius: 16px;\r\n      width: 100%;\r\n    }}\r\n    \r\n    .wrapper {{\r\n      box-sizing: border-box;\r\n      padding: 24px;\r\n    }}\r\n    \r\n    .footer {{\r\n      clear: both;\r\n      padding-top: 24px;\r\n      text-align: center;\r\n      width: 100%;\r\n    }}\r\n    \r\n    .footer td,\r\n    .footer p,\r\n    .footer span,\r\n    .footer a {{\r\n      color: #9a9ea6;\r\n      font-size: 16px;\r\n      text-align: center;\r\n    }}\r\n    /* -------------------------------------\r\n    TYPOGRAPHY\r\n------------------------------------- */\r\n    \r\n    p {{\r\n      font-family: Helvetica, sans-serif;\r\n      font-size: 16px;\r\n      font-weight: normal;\r\n      margin: 0;\r\n      margin-bottom: 16px;\r\n    }}\r\n    \r\n    a {{\r\n      color: #0867ec;\r\n      text-decoration: underline;\r\n    }}\r\n    /* -------------------------------------\r\n    BUTTONS\r\n------------------------------------- */\r\n    \r\n    .btn {{\r\n      box-sizing: border-box;\r\n      min-width: 100% !important;\r\n      width: 100%;\r\n    }}\r\n    \r\n    .btn > tbody > tr > td {{\r\n      padding-bottom: 16px;\r\n    }}\r\n    \r\n    .btn table {{\r\n      width: auto;\r\n    }}\r\n    \r\n    .btn table td {{\r\n      background-color: #ffffff;\r\n      border-radius: 4px;\r\n      text-align: center;\r\n    }}\r\n    \r\n    .btn a {{\r\n      background-color: #ffffff;\r\n      border: solid 2px #0867ec;\r\n      border-radius: 4px;\r\n      box-sizing: border-box;\r\n      color: #0867ec;\r\n      cursor: pointer;\r\n      display: inline-block;\r\n      font-size: 16px;\r\n      font-weight: bold;\r\n      margin: 0;\r\n      padding: 12px 24px;\r\n      text-decoration: none;\r\n      text-transform: capitalize;\r\n    }}\r\n    \r\n    .btn-primary table td {{\r\n      background-color: #0867ec;\r\n    }}\r\n    \r\n    .btn-primary a {{\r\n      background-color: #0867ec;\r\n      border-color: #0867ec;\r\n      color: #ffffff;\r\n    }}\r\n    \r\n    @media all {{\r\n      .btn-primary table td:hover {{\r\n        background-color: #ec0867 !important;\r\n      }}\r\n      .btn-primary a:hover {{\r\n        background-color: #ec0867 !important;\r\n        border-color: #ec0867 !important;\r\n      }}\r\n    }}\r\n    \r\n    /* -------------------------------------\r\n    OTHER STYLES THAT MIGHT BE USEFUL\r\n------------------------------------- */\r\n    \r\n    .last {{\r\n      margin-bottom: 0;\r\n    }}\r\n    \r\n    .first {{\r\n      margin-top: 0;\r\n    }}\r\n    \r\n    .align-center {{\r\n      text-align: center;\r\n    }}\r\n    \r\n    .align-right {{\r\n      text-align: right;\r\n    }}\r\n    \r\n    .align-left {{\r\n      text-align: left;\r\n    }}\r\n    \r\n    .text-link {{\r\n      color: #0867ec !important;\r\n      text-decoration: underline !important;\r\n    }}\r\n    \r\n    .clear {{\r\n      clear: both;\r\n    }}\r\n    \r\n    .mt0 {{\r\n      margin-top: 0;\r\n    }}\r\n    \r\n    .mb0 {{\r\n      margin-bottom: 0;\r\n    }}\r\n    \r\n    .preheader {{\r\n      color: transparent;\r\n      display: none;\r\n      height: 0;\r\n      max-height: 0;\r\n      max-width: 0;\r\n      opacity: 0;\r\n      overflow: hidden;\r\n      mso-hide: all;\r\n      visibility: hidden;\r\n      width: 0;\r\n    }}\r\n    \r\n    .powered-by a {{\r\n      text-decoration: none;\r\n    }}\r\n    \r\n    /* -------------------------------------\r\n    RESPONSIVE AND MOBILE FRIENDLY STYLES\r\n------------------------------------- */\r\n    \r\n    @media only screen and (max-width: 640px) {{\r\n      .main p,\r\n      .main td,\r\n      .main span {{\r\n        font-size: 16px !important;\r\n      }}\r\n      .wrapper {{\r\n        padding: 8px !important;\r\n      }}\r\n      .content {{\r\n        padding: 0 !important;\r\n      }}\r\n      .container {{\r\n        padding: 0 !important;\r\n        padding-top: 8px !important;\r\n        width: 100% !important;\r\n      }}\r\n      .main {{\r\n        border-left-width: 0 !important;\r\n        border-radius: 0 !important;\r\n        border-right-width: 0 !important;\r\n      }}\r\n      .btn table {{\r\n        max-width: 100% !important;\r\n        width: 100% !important;\r\n      }}\r\n      .btn a {{\r\n        font-size: 16px !important;\r\n        max-width: 100% !important;\r\n        width: 100% !important;\r\n      }}\r\n    }}\r\n    /* -------------------------------------\r\n    PRESERVE THESE STYLES IN THE HEAD\r\n------------------------------------- */\r\n    \r\n    @media all {{\r\n      .ExternalClass {{\r\n        width: 100%;\r\n      }}\r\n      .ExternalClass,\r\n      .ExternalClass p,\r\n      .ExternalClass span,\r\n      .ExternalClass font,\r\n      .ExternalClass td,\r\n      .ExternalClass div {{\r\n        line-height: 100%;\r\n      }}\r\n      .apple-link a {{\r\n        color: inherit !important;\r\n        font-family: inherit !important;\r\n        font-size: inherit !important;\r\n        font-weight: inherit !important;\r\n        line-height: inherit !important;\r\n        text-decoration: none !important;\r\n      }}\r\n      #MessageViewBody a {{\r\n        color: inherit;\r\n        text-decoration: none;\r\n        font-size: inherit;\r\n        font-family: inherit;\r\n        font-weight: inherit;\r\n        line-height: inherit;\r\n      }}\r\n    }}\r\n    </style>\r\n  </head>\r\n  <body>\r\n    <table role='presentation' border='0' cellpadding='0' cellspacing='0' class='body'>\r\n      <tr>\r\n        <td>&nbsp;</td>\r\n        <td class='container'>\r\n          <div class='content'>\r\n\r\n            <!-- START CENTERED WHITE CONTAINER -->\r\n            <span class='preheader'>Stock synchronizer report.</span>\r\n            <table role='presentation' border='0' cellpadding='0' cellspacing='0' class='main'>\r\n\r\n              <!-- START MAIN CONTENT AREA -->\r\n              <tr>\r\n                <td class='wrapper'>\r\n                  <p>Hi there</p>\r\n                  <p>The stock syncronizer was run and the following is the report</p>\r\n\t\t\t\t  #EMAILBODY#\r\n                  <table role='presentation' border='0' cellpadding='0' cellspacing='0' class='btn btn-primary'>\r\n                    <tbody>\r\n                      <tr>\r\n                        <td align='left'>\r\n                          <table role='presentation' border='0' cellpadding='0' cellspacing='0'>\r\n                            <tbody>\r\n                              <tr>\r\n                                <td> <a href='#SITELINK#wp-admin/edit.php?post_type=product' target='_blank'>View Products On WooCommerce</a> </td>\r\n                              </tr>\r\n                            </tbody>\r\n                          </table>\r\n                        </td>\r\n                      </tr>\r\n                    </tbody>\r\n                  </table>\r\n                  <p>Contact <a href=\"mailto:developers@granularit.com\">The developer</a> if you encounter any errors</p>\r\n                  <p>Good luck!</p>\r\n                </td>\r\n              </tr>\r\n\r\n              <!-- END MAIN CONTENT AREA -->\r\n              </table>\r\n\r\n            <!-- START FOOTER -->\r\n            <div class='footer'>\r\n              <table role='presentation' border='0' cellpadding='0' cellspacing='0'>\r\n                <tr>\r\n                  <td class='content-block'>\r\n                    <span class='apple-link'>Petstore Kenya (Petstore.co.ke)</span>\r\n                    <br> visit <a href='#SITELINK#'>Petstore Kenya</a> today!.\r\n                  </td>\r\n                </tr>\r\n                <tr>\r\n                  <td class='content-block powered-by'>\r\n                    All Rights Resevered\r\n                  </td>\r\n                </tr>\r\n              </table>\r\n            </div>\r\n\r\n            <!-- END FOOTER -->\r\n            \r\n<!-- END CENTERED WHITE CONTAINER --></div>\r\n        </td>\r\n        <td>&nbsp;</td>\r\n      </tr>\r\n    </table>\r\n  </body>\r\n</html>\r\n";

            body = body.Replace("#SITELINK#", _settings.SiteUrl);
            body = body.Replace("#EMAILBODY#", emailBody);

            return body;
        }
    }
}
