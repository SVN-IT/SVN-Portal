using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;
using System.Globalization;

namespace Sigma_Dashboard.Services.Helpers
{
    public class HomeControllerHelper
    {
        string connectionString;
        string checkListConnection;
        DBConfiguration dBConfiguration;
        AppSettingServices appSettingServices;
        SectionTimeServices sectionTimeServices;
        public HomeControllerHelper(DBConfiguration dBConfiguration, 
            AppSettingServices appSettingServices, 
            SectionTimeServices sectionTimeServices)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            checkListConnection = dBConfiguration.CheckListConnectionString;
            this.appSettingServices = appSettingServices;
            this.sectionTimeServices = sectionTimeServices;
        }

        public async Task<DashboardViewModel> SummaryData(string date, string storedProceduce, string tableName, int topDefect, string shift, string companyCode, int hours = 7)
        {
            hours = await GetLocalHourByCompanyCode(companyCode);
            // Model hiển thị dữ liệu trên dashboard
            DashboardViewModel dashboardData = new DashboardViewModel();

            // UI lấy dữ liệu từ database
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            List<SVN_target_v1UI> targetDataUI = new List<SVN_target_v1UI>(); // khai báo lớp dto để hứng dữ liệu
            List<SVN_Defect_record_v1UI> defect_RecordUI = new List<SVN_Defect_record_v1UI>();
            List<SVN_quantity_reason_v1UI> quantity_ReasonUI = new List<SVN_quantity_reason_v1UI>();

            var targetdataportal = new SVN_Target_v1DataPortal(connectionString); // gọi dataportal để sử dụng
            var defectdataportal = new SVN_Defect_record_v1DataPortal(connectionString);
            var quntityreasondataportal = new SVN_quantity_reason_v1DataPortal(connectionString);
            var svnqachecklistreportdataportal = new SVNQACheckListReport_v1DataPortal(checkListConnection);
            var mrp_productionDataPortal = new mrp_productionDataPortal(connectionString);
            var svn_equipment_StatusDataPortal = new SVN_Equipment_Status_UpdateDataPortal(connectionString);
            var productionResultDataPortal = new SVN_production_result_v1DataPortal(connectionString);

            OperInfoConfig operInfoConfig = await appSettingServices.GetOperInfoConfig(); // Lấy cấu hình các operation từ appsettings
            List<OperInfo> opers = operInfoConfig.OperInfo; // Lấy danh sách các operation từ appsettings
            opers = GetOpersByCompanyCode(opers, companyCode);

            List<SectionConfig> sectionConfigs = await appSettingServices.GetSectionConfigs(); // Lấy danh sách section theo ca từ appsettings
            List<string> curSectionList = GetCurrentSectionConfig(shift, sectionConfigs, companyCode); // Lấy danh sách section theo ca hiện tại

            //Gắn section time vào DashboardViewModel
            dashboardData.Sections = curSectionList.ToArray();

            DateTime currentDate = DateTime.Now;
            try
            {
                currentDate = DateTime.ParseExact(date, "yyyyMMdd", null);
            }
            catch
            {
                currentDate = DateTime.Now;
            }

            try
            {
                targetDataUI = await targetdataportal.ReadList(date, shift.ToLower(), storedProceduce);//lấy dữ liệu target từ csdl 
                defect_RecordUI = await defectdataportal.ReadList(date); //lấy dữ liệu defect record từ csdl theo ngày
                quantity_ReasonUI = await quntityreasondataportal.ReadList(); //lấy dữ liệu quantity reason từ csdl (các mã lỗi và tên lỗi)
                dataUI = await productionResultDataPortal.ReadListByOperationsRunning(date, tableName); //lấy dữ liệu sản xuất thực tế theo ngày và theo các operation đang chạy hiện tại

                if (targetDataUI != null && targetDataUI.Count > 0)
                {
                    targetDataUI = targetDataUI.Where(x => x.Shift.Trim().ToLower() == shift.ToLower()).ToList();

                    //Lấy ra các Operation có target để hiển thị trên UI
                    var operHaveTarget = targetDataUI.Select(x => x.Operation).Distinct().ToList();
                    opers = opers.Where(x => operHaveTarget.Contains(x.Operation)).ToList();
                }

                if (dataUI != null && dataUI.Count > 0)
                {
                    dataUI = dataUI.Where(x => x.Shift.Trim().ToLower() == shift.ToLower()).ToList(); //lọc dữ liệu thực tế theo ca hiện tại
                    foreach (var item in opers) 
                    {
                        double workingTime = 0;
                        List<DefectData> defectDatas = new List<DefectData>();
                        BarChartData barChartData = new BarChartData();
                        barChartData.Operation = item.Operation;

                        string charID = item.Operation;
                        charID = charID.Replace("-", "");
                        if (charID.Contains("(SM)"))
                        {
                            charID = charID.Replace("(SM)", "");
                        }
                        if (charID.Contains("(ITA)"))
                        {
                            charID = charID.Replace("(ITA)", "");
                        }
                        barChartData.ChartID = charID;

                        //Lấy dữ liệu Hourly target của từng operation
                        var dataUIbyOperTarget = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Target");
                        if (dataUIbyOperTarget != null)
                        {
                            barChartData.TargetLabel = $"Target total: {dataUIbyOperTarget.Time1 + dataUIbyOperTarget.Time2 + dataUIbyOperTarget.Time3 + dataUIbyOperTarget.Time4 + dataUIbyOperTarget.Time5}";
                            barChartData.TargetData = new double[] { dataUIbyOperTarget.Time1, dataUIbyOperTarget.Time2, dataUIbyOperTarget.Time3, dataUIbyOperTarget.Time4, dataUIbyOperTarget.Time5 };
                        }
                        else
                        {
                            barChartData.TargetLabel = $"Target total: 0";
                            barChartData.TargetData = new double[] { 0, 0, 0, 0, 0 };
                        }
                        var dataUIbyOperLine = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Production Qty");
                        if (dataUIbyOperLine != null)
                        {
                            barChartData.ActualLabel = $"Actual total: {dataUIbyOperLine.Time1 + dataUIbyOperLine.Time2 + dataUIbyOperLine.Time3 + dataUIbyOperLine.Time4 + dataUIbyOperLine.Time5}";
                            barChartData.ActualData = new double[] { dataUIbyOperLine.Time1, dataUIbyOperLine.Time2, dataUIbyOperLine.Time3, dataUIbyOperLine.Time4, dataUIbyOperLine.Time5 };
                        }
                        else
                        {
                            barChartData.ActualLabel = $"Actual total: 0";
                            barChartData.ActualData = new double[] { 0, 0, 0, 0, 0 };
                        }

                        //add defect by category
                        if (quantity_ReasonUI != null && defect_RecordUI != null)
                        {
                            //var quantity_ReasonUI_by_oper = quantity_ReasonUI.Where(x => x.operation == item.Operation).Select(x =>
                            //{
                            //    string category = x.name;
                            //    int value = defect_RecordUI.Where(y => y.Operation == item.Operation && y.Defect_Code == x.code).Sum(y => y.Qty_NG);

                            //    DefectData defectData = new DefectData
                            //    {
                            //        Category = category,
                            //        Value = value
                            //    };
                            //    defectDatas.Add(defectData);

                            //    return x;
                            //}).ToList();
                            var quantity_ReasonUI_by_oper = quantity_ReasonUI.Where(x => x.operation == item.Operation).ToList();
                            //foreach(var itemReason in quantity_ReasonUI_by_oper)
                            //{
                            //    string category = itemReason.name;
                            //    int value = defect_RecordUI.Where(y => y.Operation == item.Operation && y.Defect_Code == itemReason.code).Sum(y => y.Qty_NG);
                            //    DefectData defectData = new DefectData
                            //    {
                            //        Category = category,
                            //        Value = value
                            //    };
                            //    defectDatas.Add(defectData);
                            //}
                            var defectRecordUI_by_oper = defect_RecordUI.Where(x => x.Operation == item.Operation).ToList();
                            foreach (var itemDefect in defectRecordUI_by_oper)
                            {
                                var categoryByCodeAndOper = quantity_ReasonUI_by_oper.FirstOrDefault(x => x.code == itemDefect.Defect_Code);
                                if (categoryByCodeAndOper != null)
                                {
                                    string category = categoryByCodeAndOper.name;
                                    int value = itemDefect.Qty_NG;
                                    DefectData defectData = new DefectData
                                    {
                                        Category = category,
                                        Value = value
                                    };
                                    defectDatas.Add(defectData);
                                }

                            }
                        }

                        //get 5 ng lỡn nhất
                        if (defectDatas != null && defectDatas.Count > 0)
                        {
                            if (topDefect == 0)
                            {
                                defectDatas = defectDatas.OrderByDescending(x => x.Value).ToList();
                            }
                            else
                            {
                                defectDatas = defectDatas.OrderByDescending(x => x.Value).Take(topDefect).ToList();
                            }

                            List<string> defectLabel = new List<string>();
                            List<int> defectData = new List<int>();

                            defectDatas = defectDatas.Select(x =>
                            {
                                defectLabel.Add($"{x.Category}: {x.Value}");
                                defectData.Add(x.Value);
                                return x;
                            }).ToList();

                            barChartData.DefectLabel = defectLabel.ToArray();
                            barChartData.DefectData = defectData.ToArray();
                        }

                        //Lấy Daily target của từng operation
                        SVN_target_v1UI targetDataUIbyOper = new SVN_target_v1UI();
                        if(targetDataUI != null && targetDataUI.Count > 0)
                        {
                            targetDataUIbyOper = targetDataUI.FirstOrDefault(x => x.Operation == item.Operation);
                        }

                        double HPlanTarget = 0;
                        double UPHTarget = 0;
                        double UPPHTarget = 0;
                        double DefectTarget = 0;
                        double HPlanCurrent = 0;
                        double HPlanPercent = 0;
                        double UPHCurrent = 0;
                        double UPHPercent = 0;
                        double UPPHCurrent = 0;
                        double UPPHPercent = 0;
                        double DefectCurrent = 0;
                        double DefectPercent = 0;

                        if (targetDataUIbyOper != null)
                        {
                            HPlanTarget = targetDataUIbyOper.Daily_plan;
                            UPHTarget = targetDataUIbyOper.UPH;
                            UPPHTarget = targetDataUIbyOper.UPPH;
                            DefectTarget = targetDataUIbyOper.Defect;
                            //Lấy ra list section có target
                            DateTime curDateTime = DateTime.Now;
                            DateTime today = currentDate;
                            double gapTime = 0; // Khoảng thời gian trống gữa các ca

                            List<SectionTime> sectionTimes = new List<SectionTime>();
                            sectionTimes = sectionTimeServices.SplitSectionTime(dashboardData.Sections, barChartData.TargetData);

                            var listSection = sectionTimes.Where(x => x.Target != 0).ToList();

                            sectionTimes = sectionTimeServices.GetListSectionTime(listSection, today, currentDate);

                            HPlanTarget = sectionTimeServices.GetTotalTargetUntilNow(sectionTimes, DateTime.Now);

                            var minStartSection = sectionTimes.FirstOrDefault() != null ? sectionTimes.FirstOrDefault().StartTime : DateTime.MinValue;
                            var maxEndSection = sectionTimes.LastOrDefault() != null ? sectionTimes.LastOrDefault().EndTime : DateTime.MinValue;

                            // Tính toán Working time thực tế
                            if (minStartSection != DateTime.MinValue &&
                                maxEndSection != DateTime.MinValue)
                            {
                                double Duration = 0;
                                DateTime datetime = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                                var equipmentStatus = await svn_equipment_StatusDataPortal.GetCalDuration(datetime.ToString("yyyy-MM-dd"), item.Operation);
                                if (equipmentStatus != null)
                                {
                                    //Duration = equipmentStatus.TotalDuration;
                                    Duration = equipmentStatus.Select(x => x.DurationHours).Sum();
                                }
                                DateTime finishedTime = curDateTime;
                                DateTime startDatetime = minStartSection;
                                DateTime endDatetime = maxEndSection;

                                if (curDateTime < startDatetime)
                                {
                                    workingTime = 0;
                                }
                                else if (startDatetime < curDateTime && curDateTime < endDatetime)
                                {
                                    workingTime = sectionTimeServices.CalculateWorkingTime(item.Produce_id, startDatetime, finishedTime, minStartSection,
                                            curDateTime, sectionTimes, gapTime, Duration, hours);
                                }
                                else if (endDatetime < curDateTime)
                                {
                                    workingTime = targetDataUIbyOper.Workingtime;
                                }
                            }


                            //dữ liệu test
                            HPlanCurrent = targetDataUIbyOper.Total_Qty;
                            HPlanPercent = HPlanTarget != 0 ? Math.Round((HPlanCurrent / HPlanTarget) * 100, 2) : 0;
                            UPHCurrent = Math.Round(targetDataUIbyOper.Current_UPH, 2);

                            if (workingTime > 0)
                            {
                                UPHCurrent = Math.Round(targetDataUIbyOper.Total_Qty / workingTime, 2);
                            }

                            UPHPercent = UPHTarget != 0 ? Math.Round((UPHCurrent / UPHTarget) * 100, 2) : 0;
                            UPPHCurrent = Math.Round(targetDataUIbyOper.Current_UPPH, 2);

                            if (workingTime > 0)
                            {
                                UPPHCurrent = Math.Round(UPHCurrent / targetDataUIbyOper.MaxLabor, 2);
                            }

                            UPPHPercent = UPPHTarget != 0 ? Math.Round((UPPHCurrent / UPPHTarget) * 100, 2) : 0;

                            DefectTarget = Math.Round(targetDataUIbyOper.Defect * 100, 2);
                            DefectCurrent = Math.Round(targetDataUIbyOper.Total_Qty != 0 ? (targetDataUIbyOper.Total_NG_Qty / targetDataUIbyOper.Total_Qty) * 100 : 0, 2);
                            DefectPercent = Math.Round(targetDataUIbyOper.Total_Qty != 0 && targetDataUIbyOper.Defect != 0 ? (DefectCurrent / DefectTarget) * 100 : 0, 2);
                        }
                        barChartData.HourlyPlanData = HPlanCurrent + "/" + HPlanTarget;
                        barChartData.HourlyPlanPercent = $"{HPlanPercent}%";
                        barChartData.UPHData = UPHCurrent + "/" + UPHTarget;
                        barChartData.UPHPercent = $"{UPHPercent}%";
                        barChartData.UPPHData = UPPHCurrent + "/" + UPPHTarget;
                        barChartData.UPPHPercent = $"{UPPHPercent}%";
                        barChartData.DefectInfo = $"{DefectCurrent}%" + "/" + $"{DefectTarget}%";
                        barChartData.DefectPercent = $"{DefectPercent}%";

                        string line = "1";

                        var dataUIbyOperManQty = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Man Q'ty");
                        if(dataUIbyOperManQty != null)
                        {
                            line = dataUIbyOperManQty.Product;
                        }

                        barChartData.ChartTitle = $"{item.Operation} - Line {line}";

                        dashboardData.BarChartData.Add(barChartData);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception (you can use a logging framework like Serilog, NLog, etc.)
                Console.WriteLine($"An error occurred while fetching summary data: {ex.Message}");
            }
            return dashboardData;
        }

        #region Private methods
        private List<string> GetCurrentSectionConfig(string section, List<SectionConfig> sectionConfig, string companyCode)
        {
            List<string> curSectionList = new List<string>();
            //Lấy ra list ca làm việc theo ca ngày hoặc đêm, và company code
            if (sectionConfig != null && sectionConfig.Count > 0)
            {
                var sectionByCompany = sectionConfig.FirstOrDefault(x => x.CompanyCode == companyCode);
                if (sectionByCompany != null)
                {
                    if (section == "Day")
                    {
                        curSectionList = sectionByCompany.DaySection.Split(",").ToList();
                    }
                    else if (section == "Night")
                    {
                        curSectionList = sectionByCompany.NightSection.Split(",").ToList();
                    }
                }
            }
            return curSectionList;
        }

        private async Task<int> GetLocalHourByCompanyCode(string companyCode)
        {
            int hours = 7;
            //Lấy ra list ca làm việc theo ca ngày hoặc đêm, và company code
            var sectionConfig = await appSettingServices.GetSectionConfigs();
            if (sectionConfig != null && sectionConfig.Count > 0)
            {
                var sectionByCompany = sectionConfig.FirstOrDefault(x => x.CompanyCode == companyCode);
                if (sectionByCompany != null)
                {
                    hours = sectionByCompany.Hour;
                }
            }
            return hours;
        }

        private List<OperInfo> GetOpersByCompanyCode(List<OperInfo> opers, string companyCode)
        {
            if (!string.IsNullOrWhiteSpace(companyCode))
            {
                switch (companyCode)
                {
                    case "SM":
                        // Lọc các item có đuôi (SM)
                        opers = opers.Where(x => x.Operation.EndsWith("(SM)")).ToList();
                        break;

                    case "ITA":
                        // Lọc các item có đuôi (ITA)
                        opers = opers.Where(x => x.Operation.EndsWith("(ITA)")).ToList();
                        break;

                    case "SVN":
                        // Lọc các item KHÔNG chứa (SM) và KHÔNG chứa (ITA)
                        opers = opers.Where(x => !x.Operation.EndsWith("(SM)") && !x.Operation.EndsWith("(ITA)")).ToList();
                        break;
                }
            }
            return opers;
        }
        #endregion
    }
}
