using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;

namespace Sigma_Dashboard.Services.Helpers
{
    public class HomeControllerHelper
    {
        string connectionString;
        string checkListConnection;
        DBConfiguration dBConfiguration;
        AppSettingServices appSettingServices;
        public HomeControllerHelper(DBConfiguration dBConfiguration, AppSettingServices appSettingServices)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            checkListConnection = dBConfiguration.CheckListConnectionString;
            this.appSettingServices = appSettingServices;
        }

        public async Task<DashboardViewModel> SummaryData(string date, string storedProceduce, string tableName, int topDefect, string shift, int hours, string companyCode)
        {
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
                        BarChartData barChartData = new BarChartData();
                        barChartData.Operation = item.Operation;

                        //Lấy dữ liệu Hourly target của từng operation
                        var dataUIbyOperTarget = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Target");
                        if (dataUIbyOperTarget != null)
                        {
                            barChartData.TargetLabel = $"Target total: {dataUIbyOperTarget.Time1 + dataUIbyOperTarget.Time2 + dataUIbyOperTarget.Time3 + dataUIbyOperTarget.Time4 + dataUIbyOperTarget.Time5}";
                            barChartData.TargetData = new double[] { dataUIbyOperTarget.Time1, dataUIbyOperTarget.Time2, dataUIbyOperTarget.Time3, dataUIbyOperTarget.Time4, dataUIbyOperTarget.Time5 };
                        }
                        var dataUIbyOperLine = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Production Qty");
                        if (dataUIbyOperLine != null)
                        {
                            barChartData.ActualLabel = $"Actual total: {dataUIbyOperLine.Time1 + dataUIbyOperLine.Time2 + dataUIbyOperLine.Time3 + dataUIbyOperLine.Time4 + dataUIbyOperLine.Time5}";
                            barChartData.ActualData = new double[] { dataUIbyOperLine.Time1, dataUIbyOperLine.Time2, dataUIbyOperLine.Time3, dataUIbyOperLine.Time4, dataUIbyOperLine.Time5 };
                        }

                        //Lấy Daily target của từng operation
                        SVN_target_v1UI targetDataUIbyOper = new SVN_target_v1UI();
                        if(targetDataUI != null && targetDataUI.Count > 0)
                        {
                            targetDataUIbyOper = targetDataUI.FirstOrDefault(x => x.Operation == item.Operation);
                        }

                        if(targetDataUIbyOper != null)
                        {
                            double HPlanTarget = targetDataUIbyOper.Daily_plan;
                            double UPHCurrent = targetDataUIbyOper.Current_UPH;
                            double UPPHCurrent = targetDataUIbyOper.Current_UPPH;
                            double workingTime = 0;
                            //Lấy ra list section có target
                            DateTime curDateTime = DateTime.Now;
                            DateTime today = currentDate;
                            double gapTime = 0; // Khoảng thời gian trống gữa các ca
                        }


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
        #endregion
    }
}
