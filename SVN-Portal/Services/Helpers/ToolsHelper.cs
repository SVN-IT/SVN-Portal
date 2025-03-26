using PrinterServices;
using PrinterServices.Objects;
using SVN_Portal.Models;
using SVNShareLib;

namespace SVN_Portal.Services.Helpers
{
    public class ToolsHelper
    {
        public ToolsHelper()
        {
            
        }

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
                    string zpl = PrepareTemplate(zplData, viewModel);
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

        private string PrepareTemplate(string template, PrintTemViewModel viewModel)
        {
            string bar_code = $"{viewModel.item_name},{viewModel.lot_code},{viewModel.product_qty}";
            template = template.Replace("{item_name}", viewModel.item_name).
                Replace("{lot_code}", viewModel.lot_code).
                Replace("{product_qty}", viewModel.product_qty.ToString()).
                Replace("{bar_code}", bar_code);
            return template;
        }
    }
}
