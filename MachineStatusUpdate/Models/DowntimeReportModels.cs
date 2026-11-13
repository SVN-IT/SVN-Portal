namespace MachineStatusUpdate.Models
{
    public class DowntimeReportByOperationError
    {
        public string Operation { get; set; }
        public string ISS_Code { get; set; }
        public string ErrorName { get; set; }
        public int DowntimeCount { get; set; }
        public double TotalDowntimeMinutes { get; set; }
        public string TotalDowntimeFormatted { get; set; }
    }

    // Model tổng hợp theo Operation
    public class DowntimeReportByOperation
    {
        public string Operation { get; set; }
        public int TotalDowntimeCount { get; set; }
        public double TotalDowntimeMinutes { get; set; }
        public string TotalDowntimeFormatted { get; set; }
        public List<DowntimeReportByOperationError> ErrorDetails { get; set; }
    }

    // Model cho Chart data
    public class DowntimeChartData
    {
        public List<string> Operations { get; set; }
        public List<double> DowntimeMinutes { get; set; }
        public List<ChartErrorBreakdown> ErrorBreakdowns { get; set; }
    }

    public class ChartErrorBreakdown
    {
        public string Operation { get; set; }
        public List<string> ErrorNames { get; set; }
        public List<double> Minutes { get; set; }
    }


    public class IssCodeChartData
    {
        public List<string> IssCodeLabels { get; set; }
        public List<double> DowntimeMinutes { get; set; }
        public List<int> DowntimeCounts { get; set; }
    }

    public class DailyDowntimeChartData
    {
        public List<string> Dates { get; set; }
        public List<double> DowntimeMinutes { get; set; }
        public List<int> DowntimeCounts { get; set; }
    }

    // Model cho từng dự án trong Chart "Thời gian chạy của từng dự án"
    public class ProjectRunningTimeItem
    {
        public string Project { get; set; }
        public double TotalWorkingTime { get; set; } // giờ
        public double Downtime { get; set; } // giờ
        public double RunningTime { get; set; } // giờ
        public double DowntimePercent { get; set; }
        public double RunningPercent { get; set; }
    }

    public class RunningTimeByProjectChartData
    {
        public string ReportDateFrom { get; set; } // yyyyMMdd, null = không giới hạn mốc bắt đầu (lấy từ trước tới giờ)
        public string ReportDateTo { get; set; } // yyyyMMdd - luôn có giá trị, không bao giờ vượt quá "hôm qua" (N-1)
        public List<ProjectRunningTimeItem> Items { get; set; } = new List<ProjectRunningTimeItem>();
    }

    // Model cho Dashboard "Bản ghi chưa Run" (đang Stop, chưa quay lại Run)
    public class PendingStopItem
    {
        public int Id { get; set; }
        public string? SVNCode { get; set; }
        public string? Operation { get; set; }
        public string? ISS_Code { get; set; }
        public string? ErrorName { get; set; }
        public string? Description { get; set; }
        public DateTime? StopDatetime { get; set; }
        public string? EstimateTime { get; set; }
        public DateTime? EstimateAt { get; set; }
        public bool AutoRunEnabled { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsAutoEstimated { get; set; }
        public TimeSpan Duration { get; set; }
    }

    // Model cho Heatmap Downtime theo Thứ và Khung giờ
    public class HeatmapDowntimeData
    {
        public List<string> Days { get; set; }        // Mon..Sun
        public List<string> HourLabels { get; set; }   // 00h, 02h, ... 22h
        public List<List<int>> Counts { get; set; }    // [day][hourBucket] = số lần downtime
        public int LowThreshold { get; set; }          // <= LowThreshold => Low
        public int MediumThreshold { get; set; }       // <= MediumThreshold => Medium, hơn nữa => High
        public int MaxCount { get; set; }
    }
}