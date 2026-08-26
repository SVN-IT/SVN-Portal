using PrinterServices;
using PrinterServices.Objects;
using SVN_Portal.Controllers;
using SVN_Portal.Models;
using SVNShareLib;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SVN_Portal.Services.Helpers
{
    public class ToolsHelper
    {
        public ToolsHelper()
        {

        }

        /// <summary>
        /// Hàm in label đơn
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="printerConfigData"></param>
        /// <param name="copies"></param>
        /// <returns></returns>
        public BODataProcessResult PrintByTCP(List<PrintTemViewModel> viewModels,
            PrinterConfigData printerConfigData,
            int copies)
        {
            // Lấy thông tin máy in
            string printerIp = printerConfigData.IP_Printer;
            int port = Convert.ToInt32(printerConfigData.Port_Printer);
            string zplData = printerConfigData.ZPL_Temp;
            string dplData = printerConfigData.DPL_Temp;
            try
            {
                foreach (var viewModel in viewModels)
                {
                    for (int i = 0; i < copies; i++)
                    {
                        string zpl = PrepareTemplate(zplData, viewModel);
                        TCP_Printter tcp_Printter = new TCP_Printter();
                        tcp_Printter.SendToPrinterViaTCP(printerIp, port, zpl);
                    }
                    //string zpl = PrepareTemplate(zplData, viewModel);
                    //TCP_Printter tcp_Printter = new TCP_Printter();
                    //tcp_Printter.SendToPrinterViaTCP(printerIp, port, zpl);
                }
                return new BODataProcessResult { OK = true, Message = "Print successfully over TCP/IP." };
            }
            catch (Exception ex)
            {
                return new BODataProcessResult { OK = false, Message = "Error connecting via TCP/IP: " + ex.Message };
            }
        }

        public BODataProcessResult PrintTwistLabelByTCP(string zplData, PrinterConfigData printerConfigData, int copies)
        {
            string printerIp = printerConfigData.IP_Printer;
            int port = Convert.ToInt32(printerConfigData.Port_Printer);
            try
            {
                for (int i = 0; i < copies; i++)
                {
                    TCP_Printter tcp_Printter = new TCP_Printter();
                    tcp_Printter.SendToPrinterViaTCP(printerIp, port, zplData);
                }
                return new BODataProcessResult { OK = true, Message = "Print successfully over TCP/IP." };
            }
            catch (Exception ex)
            {
                return new BODataProcessResult { OK = false, Message = "Error connecting via TCP/IP: " + ex.Message };
            }
        }

        /// <summary>
        /// Hàm in shipping label
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="printerConfigData"></param>
        /// <param name="copies"></param>
        /// <returns></returns>
        public BODataProcessResult PrintShippingByTCP(List<PrintShippingViewModel> viewModels,
            PrinterConfigData printerConfigData,
            int copies, string dateCode)
        {
            // Lấy thông tin máy in
            string printerIp = printerConfigData.IP_Printer;
            int port = Convert.ToInt32(printerConfigData.Port_Printer);
            string zplData = printerConfigData.ZPL_Temp;
            string dplData = printerConfigData.DPL_Temp;
            try
            {
                for (int i = 0; i < copies; i++)
                {
                    string zpl = PrepareShippingTemplate(zplData, viewModels, viewModels[0].package_code, dateCode);
                    TCP_Printter tcp_Printter = new TCP_Printter();
                    tcp_Printter.SendToPrinterViaTCP(printerIp, port, zpl);
                }
                return new BODataProcessResult { OK = true, Message = "Print successfully over TCP/IP." };
            }
            catch (Exception ex)
            {
                return new BODataProcessResult { OK = false, Message = "Error connecting via TCP/IP: " + ex.Message };
            }
        }

        public BODataProcessResult PrintToastLabelByTCP(PrintToastLabelRequest viewModel,
            PrinterConfigData printerConfigData)
        {
            // Lấy thông tin máy in
            string printerIp = printerConfigData.IP_Printer;
            int port = Convert.ToInt32(printerConfigData.Port_Printer);
            string zplData = printerConfigData.ZPL_Temp;
            string dplData = printerConfigData.DPL_Temp;
            try
            {
                for (int i = 0; i < viewModel.Copies; i++)
                {
                    string zpl = PrepareToastLabel5ItemsTemplate(zplData, viewModel);
                    TCP_Printter tcp_Printter = new TCP_Printter();
                    tcp_Printter.SendToPrinterViaTCP(printerIp, port, zpl);
                }
                return new BODataProcessResult { OK = true, Message = "Print successfully over TCP/IP." };
            }
            catch (Exception ex)
            {
                return new BODataProcessResult { OK = false, Message = "Error connecting via TCP/IP: " + ex.Message };
            }
        }

        public async Task<BODataProcessResult> GetImageFromImage(PrintTemViewModel viewModel, string size, string zplTemp)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                string zpl = PrepareTemplate(zplTemp, viewModel);
                var content = new StringContent(zpl);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                string apiGetImage = "http://api.labelary.com/v1/printers/8dpmm/labels/#size/0/";
                apiGetImage = apiGetImage.Replace("#size", size);

                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(apiGetImage, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var image = await response.Content.ReadAsByteArrayAsync();
                        processResult.OK = true;
                        processResult.Content = image;
                        processResult.Message = "Get image successfully.";
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Error getting image: " + response.ReasonPhrase;
                    }
                }

            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = "Error connecting via TCP/IP: " + ex.Message;
            }
            return processResult;
        }

        private string PrepareTemplate(string template, PrintTemViewModel viewModel)
        {
            if (!string.IsNullOrWhiteSpace(viewModel.lot_code))
            {
                char lasstChar = viewModel.lot_code[viewModel.lot_code.Length - 1];
                if (lasstChar == '0')
                {
                    viewModel.lot_code = viewModel.lot_code.Remove(viewModel.lot_code.Length - 1) + ">60";
                }
            }
            template = template.Replace("{product_name}", viewModel.item_name).
                Replace("{lot_code}", viewModel.lot_code).
                Replace("{production_qty}", viewModel.product_qty.ToString());
            return template;
        }

        private string PrepareShippingTemplate(string template, List<PrintShippingViewModel> viewModels, string packageCode, string dateCode)
        {
            template = template.Replace("{package_code}", packageCode).Replace("{date}", dateCode);
            for (int i = 0; i < viewModels.Count; i++)
            {
                int index = i + 1;
                template = template.Replace("{lot_code" + index + "}", viewModels[i].lot_code);
            }
            return template;
        }

        private string PrepareToastLabel5ItemsTemplate(string template, PrintToastLabelRequest viewModel)
        {
            template = template.Replace("{toast_part_number}", viewModel.PartNumber)
                .Replace("{model_number}", viewModel.ModelNumber)
                .Replace("{part_desc}", viewModel.PartDesc)
                .Replace("{quantity}", viewModel.Quantity)
                .Replace("{lotID}", viewModel.LotID)
                .Replace("{toast_PO_number}", viewModel.ToastPONumber);
            if (viewModel.Print150Seri)
            {
                template = template.Replace("{all_serial_number1}", viewModel.AllSeri1);
                template = template.Replace("{all_serial_number2}", viewModel.AllSeri2);
                template = template.Replace("{all_serial_number3}", viewModel.AllSeri3);
            }
            else
            {
                var allSerialNumbers = viewModel.AllSeri1.Split(',').ToList();
                for (int i = 0; i < allSerialNumbers.Count; i++)
                {
                    int index = i + 1;
                    template = template.Replace("{serial_number" + index + "}", allSerialNumbers[i]);
                }
            }
            return template;
        }
    }
}
