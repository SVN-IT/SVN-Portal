using System.Drawing;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ViidooDBServiceAPI.Services
{
    public class YeelightService
    {
        private string ip;
        private int port = 55443;
        public YeelightService(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }
        private void SendCommand(string command)
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(ip, port);
                NetworkStream stream = client.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(command + "\r\n");
                stream.Write(data, 0, data.Length);
                stream.Close();
                client.Close();
            }
        }

        private void SendCommand(string method, object[] parameters)
        {
            var cmd = new
            {
                id = 1,
                method = method,
                @params = parameters
            };

            string json = System.Text.Json.JsonSerializer.Serialize(cmd);
            SendCommand(json);
        }

        private string SendCommandV1(string method, object[] parameters)
        {
            var cmd = new
            {
                id = 1,
                method = method,
                @params = parameters
            };

            string json = System.Text.Json.JsonSerializer.Serialize(cmd);

            using (TcpClient client = new TcpClient())
            {
                client.Connect(ip, port);
                using (NetworkStream stream = client.GetStream())
                {
                    // Gửi lệnh
                    byte[] data = Encoding.UTF8.GetBytes(json + "\r\n");
                    stream.Write(data, 0, data.Length);
                    stream.Flush();

                    // Đọc phản hồi từ đèn
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    return response;
                }
            }
        }

        // Bật / Tắt đèn
        public void SetPower(bool on)
        {
            string state = on ? "on" : "off";
            string cmd = $"{{\"id\":1,\"method\":\"set_power\",\"params\":[\"{state}\",\"smooth\",500]}}";
            SendCommand(cmd);
        }

        // Đổi màu RGB
        public void SetColor(Color color)
        {
            int rgb = (color.R << 16) + (color.G << 8) + color.B; // convert sang decimal
            string cmd = $"{{\"id\":1,\"method\":\"set_rgb\",\"params\":[{rgb},\"smooth\",500]}}";
            SendCommand(cmd);
        }

        // Đổi độ sáng
        public void SetBrightness(int brightness) // brightness: 1 - 100
        {
            string cmd = $"{{\"id\":1,\"method\":\"set_bright\",\"params\":[{brightness},\"smooth\",500]}}";
            SendCommand(cmd);
        }

        /// <summary>
        /// Nhấp nháy đèn với màu chỉ định
        /// </summary>
        /// <param name="color">Màu (Color.Red, Color.Blue, ...)</param>
        /// <param name="times">Số lần nháy (0 = vô hạn)</param>
        /// <param name="speedMs">Tốc độ mỗi nhịp (ms)</param>
        /// <param name="brightness">Độ sáng (1–100)</param>
        public void Blink(Color color, int times, int speedMs, int brightness = 100)
        {
            // Chuyển Color sang giá trị RGB integer
            int rgb = (color.R << 16) | (color.G << 8) | color.B;

            // Flow expression: 
            // speedMs bật màu -> speedMs tắt
            string flow = $"{speedMs},1,{rgb},{brightness}, {speedMs},1,0,1";

            // Gửi lệnh start_cf
            SendCommand("start_cf", new object[] { times, 1, flow });
        }

        /// <summary>
        /// Dừng nhấp nháy
        /// </summary>
        public void Stop()
        {
            SendCommand("stop_cf", null);
        }

    }
}
