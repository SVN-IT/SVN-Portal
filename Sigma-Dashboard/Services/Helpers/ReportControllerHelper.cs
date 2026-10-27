using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Spreadsheet;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;
using System.Reflection.Emit;

namespace Sigma_Dashboard.Services.Helpers
{
    public class ReportControllerHelper
    {
        string connectionString;
        DBConfiguration dBConfiguration;
        AppSettingServices appSettingServices;
        public ReportControllerHelper(DBConfiguration dBConfiguration, AppSettingServices appSettingServices)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.appSettingServices = appSettingServices;
        }

        public async Task<List<PDResultDailyViewModel>> GetDataForReport(
            DateTime fromdate, DateTime todate, string itemType)
        {
            var result = new List<PDResultDailyViewModel>();
            List<SVN_target_v1UI> targetDataUI = new List<SVN_target_v1UI>();
            List<SVN_Defect_record_v1UI> defect_RecordUI = new List<SVN_Defect_record_v1UI>();
            List<SVN_quantity_reason_v1UI> quantity_ReasonUI = new List<SVN_quantity_reason_v1UI>();
            var targetdataportal = new SVN_Target_v1DataPortal(connectionString);
            var defectdataportal = new SVN_Defect_record_v1DataPortal(connectionString);
            var quntityreasondataportal = new SVN_quantity_reason_v1DataPortal(connectionString);

            var operationConfig = await appSettingServices.GetOperInfoConfig();
            List<OperInfo> opers = operationConfig.OperInfo ?? new List<OperInfo>();
            try
            {
                targetDataUI = await targetdataportal.ReadListTargetFromToDate(fromdate, todate);
                defect_RecordUI = await defectdataportal.ReadListFromToDate(fromdate, todate);
                quantity_ReasonUI = await quntityreasondataportal.ReadList();

                if (targetDataUI != null && targetDataUI.Count > 0)
                {
                    // Lọc theo itemType nếu có
                    if (!string.IsNullOrWhiteSpace(itemType) && (itemType == "FG" || itemType == "WIP"))
                    {
                        var operNames = opers.Where(x => x.WCType == itemType).Select(x => x.Operation).ToList();
                        targetDataUI = targetDataUI.Where(x => operNames.Contains(x.Operation)).ToList();
                    }

                    // Gom nhóm theo ngày và operation
                    var grouped = targetDataUI
                        .GroupBy(x => x.Operation)
                        .OrderBy(g => g.Key);

                    foreach (var group in grouped)
                    {
                        var item = group.First();
                        var oper = opers.FirstOrDefault(x => x.Operation == item.Operation);

                        var vm = new PDResultDailyViewModel();
                        vm.OperationActive = item.Operation;
                        vm.MasterOperation = oper?.MasterOperation ?? "";
                        vm.Datetime = item.Date_time;

                        // Daily Plan
                        vm.DailyPlanTarget = Math.Round(group.Sum(x => x.Daily_plan), 2).ToString();
                        vm.DailyPlanCurrent = Math.Round(group.Sum(x => x.Total_Qty), 2).ToString();
                        double planTarget = group.Sum(x => x.Daily_plan);
                        double planCurrent = group.Sum(x => x.Total_Qty);
                        vm.DailyPlanAchieve = planTarget == 0 ? "0%" : Math.Round(planCurrent / planTarget * 100, 2).ToString() + "%";

                        // UPH, UPPH, Labor
                        vm.UPHTarget = Math.Round(group.Average(x => x.UPH), 2).ToString();
                        vm.UPHCurrent = Math.Round(group.Average(x => x.Current_UPH), 2).ToString();

                        vm.UPH = Math.Round(group.Average(x => x.UPH != 0 ? x.Current_UPH / x.UPH * 100 : 0), 2).ToString() + "%";
                        vm.UPPH = Math.Round(group.Average(x => x.UPPH != 0 ? x.Current_UPPH / x.UPPH * 100 : 0), 2).ToString() + "%";

                        vm.LaborTarget = Math.Round(group.Average(x => x.MaxLabor), 2).ToString();
                        vm.LaborCurrent = Math.Round(group.Average(x => x.Labor), 2).ToString();
                        vm.Labor = Math.Round(group.Average(x => x.Labor != 0 ? x.MaxLabor / x.Labor * 100 : 0), 2).ToString() + "%";

                        // Defect
                        double defectTarget = group.Average(x => x.Defect);
                        double defectCurrent = group.Sum(x => x.Total_NG_Qty);
                        double qtyCurrent = group.Sum(x => x.Total_Qty);

                        vm.DefectTarget = defectTarget;
                        vm.DefectCurrent = defectCurrent;
                        vm.QuantityResultCurrent = qtyCurrent;

                        vm.DefectTargetRate = Math.Round(defectTarget * 100, 2).ToString() + "%";
                        vm.DefectCurrentRate = qtyCurrent != 0 ? Math.Round((defectCurrent / qtyCurrent) * 100, 2).ToString() + "%" : "0%";
                        vm.DefectRate = (defectTarget != 0 && qtyCurrent != 0)
                            ? Math.Round((defectCurrent / qtyCurrent / defectTarget) * 100, 2).ToString() + "%"
                            : "0%";

                        // Checklist
                        vm.CheckListOnSystem = "OK";

                        // Remark (defect by category)
                        var defectByCat = (quantity_ReasonUI ?? new List<SVN_quantity_reason_v1UI>())
                            .Where(q => q.operation == item.Operation)
                            .Select(q => new
                            {
                                q.name,
                                value = (defect_RecordUI ?? new List<SVN_Defect_record_v1UI>())
                                    .Where(d => d.Operation == item.Operation && d.Defect_Code == q.code)
                                    .Sum(d => d.Qty_NG)
                            })
                            .Where(x => x.value != 0)
                            .ToList();

                        vm.Remark = defectByCat.Any()
                            ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, defectByCat.Select(x => $"{x.name}: {x.value}"))
                            : string.Empty;

                        result.Add(vm);
                    }
                }
            }
            catch
            {
                // ignore
            }
            return result;
        }

        public async Task<List<DailyTargetDetailsViewModel>> GetDailyResultDetails(
            DateTime fromdate, DateTime todate)
        {
            List<DailyTargetDetailsViewModel> viewModels = new List<DailyTargetDetailsViewModel>();
            var targetdataportal = new SVN_Target_v1DataPortal(connectionString);
            try
            {
                var targetDataUI = await targetdataportal.ReadListTargetFromToDate(fromdate, todate);
                if (targetDataUI != null && targetDataUI.Count > 0)
                {
                    targetDataUI = targetDataUI.Select(x =>
                    {
                        var viewModel = new DailyTargetDetailsViewModel();
                        var (project, partNumber) = SplitOperationActive(x.Operation);
                        viewModel.Project = project;
                        viewModel.PartNumber = partNumber;
                        viewModel.Date_time = x.Date_time;
                        viewModel.Daily_plan = x.Daily_plan;
                        viewModel.UPH = x.UPH;
                        viewModel.UPPH = x.UPPH;
                        viewModel.Labor = x.Labor;
                        viewModel.Total_Qty = x.Total_Qty;
                        viewModel.MaxLabor = x.MaxLabor;
                        viewModel.Current_UPH = x.Current_UPH;
                        viewModel.Current_UPPH = x.Current_UPPH;
                        viewModel.Defect = x.Defect;
                        viewModel.Total_NG_Qty = x.Total_NG_Qty;
                        viewModel.WC = x.WC;
                        viewModel.Workingtime = x.Workingtime;
                        viewModel.Shift = x.Shift;

                        viewModels.Add(viewModel);
                        return x;
                    }).ToList();
                }
            }
            catch
            {
                // ignore
            }
            return viewModels;
        }


        public static (string Project, string PartNumber) SplitOperationActive(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return (string.Empty, string.Empty);
            }

            // Tách chuỗi tại dấu '-' đầu tiên (tối đa thành 2 phần tử)
            string[] parts = rawInput.Split(new[] { '-' }, 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 2)
            {
                // Có dấu '-' -> phần đầu là Project, phần còn lại là PartNumber
                return (parts[0], parts[1]);
            }
            else
            {
                // Không có dấu '-' (như "POP") -> gán vào Project, PartNumber để rỗng
                return (parts[0], string.Empty);
            }
        }
    }
}
