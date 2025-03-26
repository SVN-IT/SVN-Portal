using SVNShareLib;
using System.Text;
using Zebra.Sdk.Comm;

namespace PrinterServices
{
    public class TCP_Printter
    {
        public BODataProcessResult SendToPrinterViaTCP(string printerIp, int port, string data)
        {
            try
            {
                Connection thePrinterConn = new TcpConnection(printerIp, port);
                thePrinterConn.Open();
                thePrinterConn.Write(Encoding.UTF8.GetBytes(data));
                thePrinterConn.Close();

                return new BODataProcessResult { OK = true, Message = "Print successfully over TCP/IP." };
            }
            catch (ConnectionException ex)
            {
                return new BODataProcessResult { OK = false, Message = "Error connecting via TCP/IP: " + ex.Message };
            }
            catch (Exception ex)
            {

                return new BODataProcessResult { OK = false, Message = "Unknown error over TCP/IP: " + ex.Message };
            }
        }
    }
}
