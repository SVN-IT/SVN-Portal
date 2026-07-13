using Org.BouncyCastle.Asn1.X509;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL;
using System.Globalization;

namespace Sigma_Dashboard.Services
{
    public class SectionTimeServices
    {
        string connectionString;
        DBConfiguration dBConfiguration;
        public SectionTimeServices(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }

        /// <summary>
        /// Lấy current working time thực tế của từng item
        /// </summary>
        /// <param name="product_id"></param>
        /// <param name="startDatetime"></param>
        /// <param name="finishedTime"></param>
        /// <param name="minStartSection"></param>
        /// <param name="curDateTime"></param>
        /// <param name="sectionTimes"></param>
        /// <param name="gapTime"></param>
        /// <param name="Duration"></param>
        /// <param name="hours"></param>
        /// <returns></returns>
        public double CalculateWorkingTime(List<int> product_id, DateTime startDatetime, DateTime finishedTime,
            DateTime minStartSection, DateTime curDateTime,
            List<SectionTime> sectionTimes, double gapTime, double Duration, int hours)
        {
            double workingTime = 0;
            var dataPortal = new mrp_productionDataPortal(connectionString);
            var productionUI = dataPortal.GetDataByProduct_IDInSection(product_id, startDatetime, curDateTime, hours);
            startDatetime = minStartSection;
            startDatetime = GetStartTime(sectionTimes, startDatetime);
            if (productionUI != null)
            {
                finishedTime = productionUI.date_finished != null ? productionUI.date_finished.Value.AddHours(hours) : curDateTime;
            }
            else
            {
                DateTimeOffset timeAtPlusHour = new DateTimeOffset(curDateTime, TimeSpan.FromHours(7))
                              .ToOffset(TimeSpan.FromHours(hours));
                curDateTime = timeAtPlusHour.DateTime;

                finishedTime = curDateTime;
            }
            gapTime = Math.Round(GetTotalGap(sectionTimes, finishedTime).TotalMinutes / 60.0, 2);
            TimeSpan diff = finishedTime - startDatetime;
            workingTime = Math.Round(diff.TotalMinutes / 60.0, 2) - gapTime; // - Duration
            return workingTime;
        }

        /// <summary>
        /// Từ 2 array Sections và TargetData, tạo ra List<SectionTime> để dễ dàng xử lý sau này
        /// </summary>
        /// <param name="Sections"></param>
        /// <param name="TargetData"></param>
        /// <returns></returns>
        public List<SectionTime> SplitSectionTime(string[] Sections, double[] TargetData)
        {
            List<SectionTime> sectionTimes = new List<SectionTime>();
            for (int i = 0; i < Sections.Length; i++)
            {
                SectionTime sectionTime = new SectionTime();
                sectionTime.Time = Sections[i];
                if(i < TargetData.Length)
                    sectionTime.Target = TargetData[i];

                sectionTimes.Add(sectionTime);
            }
            return sectionTimes;
        }

        /// <summary>
        /// Tính tổng target đã đạt được cho đến thời điểm hiện tại (bao gồm cả ca đang diễn ra nếu có)
        /// </summary>
        /// <param name="sections"></param>
        /// <param name="now"></param>
        /// <returns></returns>
        public double GetTotalTargetUntilNow(List<SectionTime> sections, DateTime now)
        {
            return sections
                .Where(s =>
                    s.EndTime <= now ||                // đã kết thúc
                    (s.StartTime <= now && now <= s.EndTime) // đang diễn ra
                )
                .Sum(s => s.Target);
        }

        /// <summary>
        /// Hàm này sẽ chuyển đổi chuỗi thời gian trong Sections (ví dụ: "8h-10h") thành các đối tượng SectionTime có StartTime và EndTime là DateTime thực tế.
        /// </summary>
        /// <param name="listSection"></param>
        /// <param name="today"></param>
        /// <param name="currentDate"></param>
        /// <returns></returns>
        public List<SectionTime> GetListSectionTime(List<SectionTime> listSection, DateTime today, DateTime currentDate)
        {
            List<SectionTime> sectionTimes = new List<SectionTime>();
            listSection = listSection.Select(x =>
            {
                var times = x.Time.Split('-');

                // Hàm phụ để chuẩn hóa chuỗi "8h" hoặc "8:30" thành "08:30"
                string NormalizeTime(string t)
                {
                    t = t.Replace("h", ":");
                    if (t.EndsWith(":")) t += "00";
                    if (t.Length <= 4 && !t.Contains(":")) t += ":00"; // Xử lý trường hợp chỉ có số "8"
                    if (t.IndexOf(":") == 1) t = "0" + t;
                    return t;
                }

                string sTime = NormalizeTime(times[0]);
                string eTime = NormalizeTime(times[1]);

                string dateString = today.ToString("yyyy-MM-dd");

                // Parse thời gian bắt đầu
                DateTime startDatetime = DateTime.ParseExact(dateString + " " + sTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                // Parse thời gian kết thúc
                DateTime endDatetime = DateTime.ParseExact(dateString + " " + eTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                // LOGIC QUAN TRỌNG: 
                // Nếu thời gian kết thúc nhỏ hơn thời gian bắt đầu (VD: 22:00 - 02:00)
                // Hoặc nếu cả hai đều nhỏ hơn một mốc buổi sáng trong khi ca bắt đầu từ tối (ca đêm)
                if (endDatetime <= startDatetime)
                {
                    endDatetime = endDatetime.AddDays(1);
                }

                // Trường hợp đặc biệt: Nếu cả Start và End đều thuộc ngày hôm sau (VD: 00:30 - 03:00)
                // Giả sử ca đêm bắt đầu từ 20h, bất kỳ mốc nào < 8h sáng nên được hiểu là ngày hôm sau
                if (startDatetime.Hour < 8 && startDatetime.Hour >= 0)
                {
                    // Kiểm tra nếu thực sự là đang chạy ca đêm (thường dựa vào giờ bắt đầu ca lớn)
                    // Ở đây ta cộng thêm 1 ngày cho cả hai nếu chúng nằm trong khung giờ sáng sớm
                    startDatetime = startDatetime.AddDays(1);
                    endDatetime = endDatetime.AddDays(1);
                }

                // 2025-12-19: Lấy target output theo ca hiện tại
                //if (currentDate >= startDatetime && currentDate <= endDatetime)
                //{
                //    HPlanTarget = x.Target;
                //}

                SectionTime sectionTime = new SectionTime();
                sectionTime.Time = x.Time;
                sectionTime.StartTime = startDatetime;
                sectionTime.EndTime = endDatetime;
                sectionTime.Target = x.Target;
                sectionTimes.Add(sectionTime);

                return x;
            }).ToList();
            return sectionTimes;
        }

        /// <summary>
        /// Hàm này sẽ xác định thời gian bắt đầu thực tế của ca đầu tiên dựa trên danh sách SectionTime đã được chuyển đổi và thời gian đầu vào (thường là thời gian bắt đầu sản xuất của item).
        /// </summary>
        /// <param name="times"></param>
        /// <param name="firstInput"></param>
        /// <returns></returns>
        private static DateTime GetStartTime(List<SectionTime> times, DateTime firstInput)
        {
            DateTime startTime = firstInput;
            if (times != null && times.Count > 0)
            {
                if (firstInput > times[0].StartTime && firstInput < times[0].EndTime)
                {
                    startTime = firstInput;
                }
                else
                {
                    startTime = times[0].StartTime;
                }
            }
            return startTime;
        }

        /// <summary>
        /// Hàm này sẽ tính tổng thời gian gap (thời gian không hoạt động) giữa các ca làm việc đã hoàn thành cho đến thời điểm hiện tại.
        /// </summary>
        /// <param name="times"></param>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        private static TimeSpan GetTotalGap(List<SectionTime> times, DateTime currentTime)
        {
            if (times == null || times.Count < 2)
                return TimeSpan.Zero;

            // Sắp xếp theo StartTime
            var ordered = times.OrderBy(t => t.StartTime).ToList();

            DateTime minStart = ordered.First().StartTime;
            DateTime maxEnd = ordered.Max(t => t.EndTime);

            TimeSpan totalGap = TimeSpan.Zero;

            // Nếu currentTime <= minStart: chưa có gì xảy ra → 0
            if (currentTime <= minStart)
                return TimeSpan.Zero;

            // Nếu currentTime >= maxEnd: tính toàn bộ gap
            if (currentTime >= maxEnd)
            {
                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    var gap = ordered[i + 1].StartTime - ordered[i].EndTime;
                    if (gap > TimeSpan.Zero)
                        totalGap += gap;
                }
                return totalGap;
            }

            // Nếu currentTime nằm trong khoảng [minStart, maxEnd]
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var end = ordered[i].EndTime;
                var nextStart = ordered[i + 1].StartTime;

                if (currentTime <= end)
                {
                    // Vẫn đang trong ca này => chưa có gap nào xảy ra
                    break;
                }
                else if (currentTime > end && currentTime <= nextStart)
                {
                    // currentTime nằm giữa end và nextStart => gap tính tới currentTime
                    var gap = currentTime - end;
                    if (gap > TimeSpan.Zero)
                        totalGap += gap;
                    break;
                }
                else
                {
                    // currentTime đã qua nextStart => cộng toàn bộ gap này
                    var gap = nextStart - end;
                    if (gap > TimeSpan.Zero)
                        totalGap += gap;
                }
            }

            return totalGap;
        }
    }

    public class SectionTime
    {
        public string Time { get; set; } //Chuỗi stringTime 8h-10h chưa dc phân giã
        public DateTime StartTime { get; set; } // chuyển thành 8h
        public DateTime EndTime { get; set; } // chuyển thành 10h
        public double Target { get; set; }
    }
}
