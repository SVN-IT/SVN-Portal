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
                    for(int i = 0; i < copies; i++)
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

        private string PrepareTemplate(string template, PrintTemViewModel viewModel)
        {
            if(!string.IsNullOrWhiteSpace(viewModel.lot_code))
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
    }
}
