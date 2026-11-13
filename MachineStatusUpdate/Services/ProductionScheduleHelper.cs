using MachineStatusUpdate.Models;

namespace MachineStatusUpdate.Services
{
    /* Tính mốc "hết giờ sản xuất theo kế hoạch" của 1 Operation trong ngày, dựa vào dòng
       Type_value = "Target" của bảng SVN_Production_result_Viindoo (ca day + ca night).
       Dùng làm EstimateTime dự phòng cho các bản ghi Stop mà người dùng quên nhập EstimateTime thủ công. */
    public static class ProductionScheduleHelper
    {
        // Giờ KẾT THÚC của từng Time-slot, và số ngày cộng thêm so với Date_time gốc.
        private static readonly (TimeSpan End, int DayOffset)[] DaySlotEnds =
        {
            (new TimeSpan(10, 10, 0), 0), // Time1: 8h00 - 10h10
            (new TimeSpan(12, 30, 0), 0), // Time2: 10h10 - 12h30
            (new TimeSpan(15, 10, 0), 0), // Time3: 12h30 - 15h10
            (new TimeSpan(18,  0, 0), 0), // Time4: 15h10 - 18h00
            (new TimeSpan(20,  0, 0), 0), // Time5: 18h00 - 20h00
        };

        private static readonly (TimeSpan End, int DayOffset)[] NightSlotEnds =
        {
            (new TimeSpan(22,  0, 0), 0), // Time1: 20h00 - 22h00
            (new TimeSpan(23, 30, 0), 0), // Time2: 22h10 - 23h30
            (new TimeSpan( 3,  0, 0), 1), // Time3: 0h30 - 3h00 (hôm sau)
            (new TimeSpan( 5, 30, 0), 1), // Time4: 3h10 - 5h30 (hôm sau)
            (new TimeSpan( 8,  0, 0), 1), // Time5: 6h00 - 8h00 (hôm sau)
        };

        private static readonly TimeSpan DayShiftStart = new TimeSpan(8, 0, 0);

        /// "Ngày sản xuất" (production day) của 1 thời điểm: 1 chu kỳ SX là [8h00 ngày D, 8h00 ngày D+1)
        /// (ca Day rồi nối tiếp ca Night, ca Night kéo dài qua nửa đêm tới 8h hôm sau). Vì vậy 1 thời điểm
        /// trước 8h sáng vẫn thuộc "ngày sản xuất" hôm trước (đang ở đuôi ca đêm hôm trước), không phải
        /// ngày dương lịch hiện tại — khớp với cách bảng SVN_Production_result_Viindoo gán Date_time cho
        /// Time3/Time4/Time5 của ca night.
        public static DateTime GetProductionDay(DateTime dateTime)
        {
            return dateTime.TimeOfDay < DayShiftStart ? dateTime.Date.AddDays(-1) : dateTime.Date;
        }

        /// Trả về null nếu không có Target > 0 ở cả ca day lẫn ca night (không xác định được).
        public static DateTime? CalculateEndOfScheduledProduction(
            DateTime baseDate,
            IEnumerable<SVN_Production_result_Viindoo> targetRowsForOperation)
        {
            var rows = targetRowsForOperation?.ToList() ?? new List<SVN_Production_result_Viindoo>();

            var dayRow = rows.FirstOrDefault(r => string.Equals(r.Shift?.Trim(), "day", StringComparison.OrdinalIgnoreCase));
            var nightRow = rows.FirstOrDefault(r => string.Equals(r.Shift?.Trim(), "night", StringComparison.OrdinalIgnoreCase));

            var dayCutoff = ComputeShiftCutoff(baseDate.Date, dayRow, DaySlotEnds);
            var nightCutoff = ComputeShiftCutoff(baseDate.Date, nightRow, NightSlotEnds);

            if (dayCutoff == null) return nightCutoff;
            if (nightCutoff == null) return dayCutoff;
            return dayCutoff > nightCutoff ? dayCutoff : nightCutoff;
        }

        private static DateTime? ComputeShiftCutoff(
            DateTime baseDate,
            SVN_Production_result_Viindoo? row,
            (TimeSpan End, int DayOffset)[] slotEnds)
        {
            if (row == null) return null;

            string?[] targets = { row.Time1, row.Time2, row.Time3, row.Time4, row.Time5 };

            int lastActiveIndex = -1;
            for (int i = 0; i < targets.Length; i++)
            {
                if (decimal.TryParse(targets[i], out var v) && v != 0)
                    lastActiveIndex = i;
            }

            if (lastActiveIndex < 0) return null;

            var (end, dayOffset) = slotEnds[lastActiveIndex];
            return baseDate.AddDays(dayOffset).Add(end);
        }
    }
}
