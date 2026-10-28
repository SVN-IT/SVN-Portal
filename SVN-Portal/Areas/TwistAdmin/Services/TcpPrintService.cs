using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace SVN_Portal.Areas.TwistAdmin.Services
{
    // Gửi ZPL tới máy in qua TCP (cổng raw 9100) có timeout và trả lỗi thật.
    // Lưu ý: gửi thành công = máy in đã nhận dữ liệu, chưa chắc tem đã in ra (hết giấy, mở nắp...).
    public static class TcpPrintService
    {
        const int ConnectTimeoutMs = 5000;
        const int WriteTimeoutMs = 10000;

        public class SendResult
        {
            public bool Ok { get; set; }
            public string? Error { get; set; }
            public int Bytes { get; set; }
            public long ElapsedMs { get; set; }
        }

        // copies bản: gửi lặp ZPL trên cùng 1 kết nối
        public static async Task<SendResult> SendAsync(string ip, int port, string zpl, int copies)
        {
            var sw = Stopwatch.StartNew();
            byte[] data = Encoding.UTF8.GetBytes(zpl);
            try
            {
                using var client = new TcpClient();
                string? connectError = await ConnectAsync(client, ip, port);
                if (connectError != null) return new SendResult { Error = connectError, ElapsedMs = sw.ElapsedMilliseconds };

                using var stream = client.GetStream();
                stream.WriteTimeout = WriteTimeoutMs;
                for (int i = 0; i < copies; i++)
                {
                    await stream.WriteAsync(data);
                }
                await stream.FlushAsync();
                return new SendResult { Ok = true, Bytes = data.Length * copies, ElapsedMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                return new SendResult { Error = $"Lỗi khi gửi dữ liệu tới {ip}:{port}: {ex.Message}", ElapsedMs = sw.ElapsedMilliseconds };
            }
        }

        // Chỉ mở rồi đóng kết nối để kiểm tra máy in có phản hồi không (không in gì)
        public static async Task<SendResult> TestAsync(string ip, int port)
        {
            var sw = Stopwatch.StartNew();
            using var client = new TcpClient();
            string? error = await ConnectAsync(client, ip, port);
            return new SendResult { Ok = error == null, Error = error, ElapsedMs = sw.ElapsedMilliseconds };
        }

        static async Task<string?> ConnectAsync(TcpClient client, string ip, int port)
        {
            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            try
            {
                await client.ConnectAsync(ip, port, cts.Token);
                return null;
            }
            catch (OperationCanceledException)
            {
                return $"Không kết nối được máy in: {ip}:{port} không phản hồi sau {ConnectTimeoutMs / 1000} giây. Kiểm tra máy in đã bật, cắm mạng và đúng IP/Port.";
            }
            catch (SocketException ex)
            {
                return $"Không kết nối được máy in {ip}:{port}: {ex.Message}";
            }
        }
    }
}
