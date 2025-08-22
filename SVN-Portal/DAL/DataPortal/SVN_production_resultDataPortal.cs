using Dapper;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SVNShareLib.DAL;
using System.Globalization;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_production_resultDataPortal
    {
        string connectionString;
        public SVN_production_resultDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_production_resultUI>> ReadList(string date, string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_resultUI> dataUI = new List<SVN_production_resultUI>();
            int timeOut = 1000;
            try
            {
                using(IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date";
                    param = new { date = date };
                    var data = await conn.QueryAsync<SVN_production_resultUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_production_resultUI>> ReadListByOperAndWC(string date, string oper, string WC = "", string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_resultUI> dataUI = new List<SVN_production_resultUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Operation = @oper";
                    if(!string.IsNullOrWhiteSpace(WC))
                    {
                        sql += " AND WC = @WC";
                        param = new { date = date, oper = oper, WC = WC };
                    }
                    else
                    {
                        param = new { date = date, oper = oper };
                    }
                    var data = await conn.QueryAsync<SVN_production_resultUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<QtyProdResultByOperViewModel>> SummaryData(string date, List<OperInfo> opers, string storedProceduce, string tableName, string checkListConnection, int topDefect)
        {
            List<QtyProdResultByOperViewModel> viewModels = new List<QtyProdResultByOperViewModel>();
            List<SVN_production_resultUI> dataUI = new List<SVN_production_resultUI>();
            List<SVN_target> targetDataUI = new List<SVN_target>(); // khai báo lớp dto để hứng dữ liệu
            List<SVN_Defect_recordUI> defect_RecordUI = new List<SVN_Defect_recordUI>();
            List<SVN_quantity_reasonUI> quantity_ReasonUI = new List<SVN_quantity_reasonUI>();
            var targetdataportal = new SVN_TargetDataPortal(connectionString); // gọi dataportal để sử dụng
            var defectdataportal = new SVN_Defect_recordDataPortal(connectionString);
            var quntityreasondataportal = new SVN_quantity_reasonDataPortal(connectionString);
            var svnqachecklistreportdataportal = new SVNQACheckListReportDataPortal(checkListConnection);
            try
            {
                targetDataUI = await targetdataportal.ReadList(date, storedProceduce);//lấy dữ liệu target từ csdl 
                defect_RecordUI = await defectdataportal.ReadList(date);
                quantity_ReasonUI = await quntityreasondataportal.ReadList();
                dataUI = await ReadList(date, tableName);


                if (dataUI.Count > 0) 
                {
                    //Lấy Data có WC = null hoặc WC contain FG
                    dataUI = dataUI.Where(x => string.IsNullOrWhiteSpace(x.WC) || x.WC.Contains("FG")).ToList();

                    foreach (var item in opers)
                    {
                        QtyProdResultByOperViewModel viewModel = new QtyProdResultByOperViewModel();
                        QtyProdResultViewModel val1 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val2 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val3 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val4 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val5 = new QtyProdResultViewModel();

                        val1.Time = "8h-10h";
                        val2.Time = "10h10-12h";
                        val3.Time = "13h-15h";
                        val4.Time = "15h10-17h30";
                        val5.Time = "18h-20h";

                        viewModel.Operation = item.Operation;
                        viewModel.Name = item.Name;

                        //add defect by category
                        if (quantity_ReasonUI != null && defect_RecordUI != null)
                        {
                            var quantity_ReasonUI_by_oper = quantity_ReasonUI.Where(x => x.operation == item.Operation).Select(x =>
                            {
                                DefectByCategoryViewModel model = new DefectByCategoryViewModel();
                                model.category = x.name;
                                model.value = defect_RecordUI.Where(y => y.Operation == item.Operation && y.Defect_Code == x.code).Sum(y => y.Qty_NG).ToString();
                                viewModel.DefectByCategoryViewModels.Add(model);
                                return x;
                            }).ToList();
                        }

                        //get 5 ng lỡn nhất
                        if(viewModel.DefectByCategoryViewModels != null && viewModel.DefectByCategoryViewModels.Count > 0)
                        {
                            if (topDefect == 0)
                            {
                                viewModel.DefectByCategoryViewModels = viewModel.DefectByCategoryViewModels.OrderByDescending(x => x.value).ToList();
                            }
                            else
                            {
                                viewModel.DefectByCategoryViewModels = viewModel.DefectByCategoryViewModels.OrderByDescending(x => x.value).Take(topDefect).ToList();
                            }
                        }


                        //sai ở đây
                        //dùng linq mà list đang bị null
                        var dataUIByOper = targetDataUI.FirstOrDefault(x => x.Operation == item.Operation);//Lấy ra 1 dòng target theo opearation

                        var dataUIbyOperTarget = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Target");
                        if (dataUIbyOperTarget != null) 
                        {
                            val1.Target = dataUIbyOperTarget.Time1;
                            val2.Target = dataUIbyOperTarget.Time2;
                            val3.Target = dataUIbyOperTarget.Time3;
                            val4.Target = dataUIbyOperTarget.Time4;
                            val5.Target = dataUIbyOperTarget.Time5;
                            if(dataUIbyOperTarget.Time1 == 0 && dataUIbyOperTarget.Time2 == 0 && 
                                dataUIbyOperTarget.Time3 == 0 && dataUIbyOperTarget.Time4 == 0 && dataUIbyOperTarget.Time5 == 0)
                            {
                                viewModel.IsProduction = false;
                            }
                            else
                            {
                                viewModel.IsProduction = true;
                            }
                        }
                        var dataUIbyOperLine = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Production Qty");
                        if(dataUIbyOperLine != null)
                        {
                            val1.Line = dataUIbyOperLine.Time1;
                            val2.Line = dataUIbyOperLine.Time2;
                            val3.Line = dataUIbyOperLine.Time3;
                            val4.Line = dataUIbyOperLine.Time4;
                            val5.Line = dataUIbyOperLine.Time5;

                            //Lấy dữ liệu Achieve và Forecast
                            viewModel.Achieve = dataUIbyOperLine.Achieve;
                            viewModel.Forecast = dataUIbyOperLine.Forecast;
                            viewModel.WORunning = dataUIbyOperLine.WORunning;
                            viewModel.Product = dataUIbyOperLine.Product;
                            viewModel.Customer = dataUIbyOperLine.Customer;
                            viewModel.WC = dataUIbyOperLine.WC;
                        }
                        var dataUIbyOperManQty = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "Man Q'ty");
                        if (dataUIbyOperManQty != null)
                        {
                            val1.ManQuantity = dataUIbyOperManQty.Time1;
                            val2.ManQuantity = dataUIbyOperManQty.Time2;
                            val3.ManQuantity = dataUIbyOperManQty.Time3;
                            val4.ManQuantity = dataUIbyOperManQty.Time4;
                            val5.ManQuantity = dataUIbyOperManQty.Time5;
                        }
                        var dataUIbyOperNGQty = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.Type_value == "NG_Qty");
                        if (dataUIbyOperNGQty != null)
                        {
                            val1.NG = dataUIbyOperNGQty.Time1;
                            val2.NG = dataUIbyOperNGQty.Time2;
                            val3.NG = dataUIbyOperNGQty.Time3;
                            val4.NG = dataUIbyOperNGQty.Time4;
                            val5.NG = dataUIbyOperNGQty.Time5;
                        }
                        viewModel.ViewModels.Add(val1);
                        viewModel.ViewModels.Add(val2);
                        viewModel.ViewModels.Add(val3);
                        viewModel.ViewModels.Add(val4);
                        viewModel.ViewModels.Add(val5);

                        DateTime result = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                        string strDate = result.ToString("yyyy-MM-dd") + "  00:00:00.000";
                        var checkListData = await svnqachecklistreportdataportal.GetDataByDateAndOperation(strDate, item.StoreID);
                        if (checkListData != null && checkListData.Count > 0)
                        {
                            viewModel.CanProduction = true;
                        }
                        else 
                        {
                            viewModel.CanProduction = false;
                        }


                        if (dataUIByOper != null)
                        {
                            //tạo dong Daiily plan của 1 operation
                            SVN_targetViewModel dailyPlanVM = new SVN_targetViewModel()
                            {
                                Item = "H.Plan",
                                Target = dataUIByOper.Daily_plan,
                                Current = dataUIByOper.Total_Qty,
                                Percent = dataUIByOper.Daily_plan != 0 ? (dataUIByOper.Total_Qty / dataUIByOper.Daily_plan) * 100 : 0
                            };
                            SVN_targetViewModel UPHVM = new SVN_targetViewModel()
                            {
                                Item = "UPH",
                                Target = dataUIByOper.UPH,
                                Current = dataUIByOper.Current_UPH,
                                Percent = dataUIByOper.UPH != 0 ? (dataUIByOper.Current_UPH / dataUIByOper.UPH) * 100 : 0
                            };
                            SVN_targetViewModel UPPHVM = new SVN_targetViewModel()
                            {
                                Item = "UPPH",
                                Target = dataUIByOper.UPPH,
                                Current = dataUIByOper.Current_UPPH,
                                Percent = dataUIByOper.UPPH != 0 ? (dataUIByOper.Current_UPPH / dataUIByOper.UPPH) * 100 : 0
                            };
                            SVN_targetViewModel LaborVM = new SVN_targetViewModel()
                            {
                                Item = "Labor",
                                Target = dataUIByOper.Labor,
                                Current = dataUIByOper.MaxLabor,
                                Percent = dataUIByOper.Labor != 0 ? (dataUIByOper.MaxLabor / dataUIByOper.Labor) * 100 : 0
                            };
                            SVN_targetViewModel NGVM = new SVN_targetViewModel()
                            {
                                Item = "Defect",
                                Target = Math.Round(dataUIByOper.Defect * 100, 2),
                                Current = Math.Round(dataUIByOper.Total_Qty != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty) * 100 : 0, 2),
                                Percent = Math.Round(dataUIByOper.Total_Qty != 0 && dataUIByOper.Defect != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty / dataUIByOper.Defect) * 100 : 0, 2)
                            };
                            viewModel.TargetViewModels.Add(dailyPlanVM);
                            viewModel.TargetViewModels.Add(UPHVM);
                            viewModel.TargetViewModels.Add(UPPHVM);
                            viewModel.TargetViewModels.Add(LaborVM);
                            viewModel.TargetViewModels.Add(NGVM);
                            viewModel.Est = ((dataUIByOper.Current_UPH * 8.5) / dataUIByOper.Daily_plan) * 100;
                        }   

                        viewModels.Add(viewModel);
                    }
                }
                return viewModels;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        public async Task<List<QtyProdResultByOperViewModel>> SummaryData_Viindoo(string date, List<OperInfo> opers, string storedProceduce, string tableName, string checkListConnection)
        {
            List<QtyProdResultByOperViewModel> viewModels = new List<QtyProdResultByOperViewModel>();
            List<SVN_production_resultUI> dataUI = new List<SVN_production_resultUI>();
            List<SVN_target> targetDataUI = new List<SVN_target>(); // khai báo lớp dto để hứng dữ liệu
            var targetdataportal = new SVN_TargetDataPortal(connectionString); // gọi dataportal để sử dụng
            var mrp_productionDataPortal = new mrp_productionDataPortal(connectionString);
            var svnqachecklistreportdataportal = new SVNQACheckListReportDataPortal(checkListConnection);

            try
            {
                targetDataUI = await targetdataportal.ReadList(date, storedProceduce);//lấy dữ liệu target từ csdl 
                dataUI = await ReadList(date, tableName);
                if (dataUI.Count > 0)
                {
                    foreach (var item in opers)
                    {
                        QtyProdResultByOperViewModel viewModel = new QtyProdResultByOperViewModel();
                        QtyProdResultViewModel val1 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val2 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val3 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val4 = new QtyProdResultViewModel();
                        QtyProdResultViewModel val5 = new QtyProdResultViewModel();

                        val1.Time = "8h-10h";
                        val2.Time = "10h10-12h";
                        val3.Time = "13h-15h";
                        val4.Time = "15h10-17h30";
                        val5.Time = "18h-20h";

                        viewModel.Operation = item.Operation;
                        viewModel.Name = item.Name;
                        if (string.IsNullOrWhiteSpace(item.WCName))
                        {
                            item.WCName = null;
                        }
                        viewModel.WC = item.WCName;

                        //sai ở đây
                        //dùng linq mà list đang bị null
                        var dataUIByOper = targetDataUI.FirstOrDefault(x => x.Operation == item.Operation && x.WC == item.WCName);//Lấy ra 1 dòng target theo opearation

                        var dataUIbyOperTarget = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.WC == item.WCName && x.Type_value == "Target");
                        if (dataUIbyOperTarget != null)
                        {
                            val1.Target = dataUIbyOperTarget.Time1;
                            val2.Target = dataUIbyOperTarget.Time2;
                            val3.Target = dataUIbyOperTarget.Time3;
                            val4.Target = dataUIbyOperTarget.Time4;
                            val5.Target = dataUIbyOperTarget.Time5;

                            if (dataUIbyOperTarget.Time1 == 0 && dataUIbyOperTarget.Time2 == 0 &&
                                dataUIbyOperTarget.Time3 == 0 && dataUIbyOperTarget.Time4 == 0 && dataUIbyOperTarget.Time5 == 0)
                            {
                                viewModel.IsProduction = false;
                            }
                            else
                            {
                                viewModel.IsProduction = true;
                            }
                        }
                        var dataUIbyOperLine = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.WC == item.WCName && x.Type_value == "Production Qty");
                        if (dataUIbyOperLine != null)
                        {
                            val1.Line = dataUIbyOperLine.Time1;
                            val2.Line = dataUIbyOperLine.Time2;
                            val3.Line = dataUIbyOperLine.Time3;
                            val4.Line = dataUIbyOperLine.Time4;
                            val5.Line = dataUIbyOperLine.Time5;

                            //Lấy dữ liệu Achieve và Forecast
                            viewModel.Achieve = dataUIbyOperLine.Achieve;
                            viewModel.Forecast = dataUIbyOperLine.Forecast;
                            viewModel.WORunning = dataUIbyOperLine.WORunning;
                            viewModel.Product = dataUIbyOperLine.Product;
                            viewModel.Customer = dataUIbyOperLine.Customer;
                        }
                        var dataUIbyOperManQty = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.WC == item.WCName && x.Type_value == "Man Q'ty");
                        if (dataUIbyOperManQty != null)
                        {
                            val1.ManQuantity = dataUIbyOperManQty.Time1;
                            val2.ManQuantity = dataUIbyOperManQty.Time2;
                            val3.ManQuantity = dataUIbyOperManQty.Time3;
                            val4.ManQuantity = dataUIbyOperManQty.Time4;
                            val5.ManQuantity = dataUIbyOperManQty.Time5;
                        }
                        var dataUIbyOperNGQty = dataUI.FirstOrDefault(x => x.Operation == item.Operation && x.WC == item.WCName && x.Type_value == "NG_Qty");
                        if (dataUIbyOperNGQty != null)
                        {
                            val1.NG = dataUIbyOperNGQty.Time1;
                            val2.NG = dataUIbyOperNGQty.Time2;
                            val3.NG = dataUIbyOperNGQty.Time3;
                            val4.NG = dataUIbyOperNGQty.Time4;
                            val5.NG = dataUIbyOperNGQty.Time5;
                        }
                        viewModel.ViewModels.Add(val1);
                        viewModel.ViewModels.Add(val2);
                        viewModel.ViewModels.Add(val3);
                        viewModel.ViewModels.Add(val4);
                        viewModel.ViewModels.Add(val5);
                        DateTime result = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                        string strDate = result.ToString("yyyy-MM-dd") + "  00:00:00.000";
                        var checkListData = await svnqachecklistreportdataportal.GetDataByDateAndOperation(strDate, item.StoreID);
                        if (checkListData != null && checkListData.Count > 0)
                        {
                            viewModel.CanProduction = true;
                        }
                        else
                        {
                            viewModel.CanProduction = false;
                        }

                        try
                        {
                            var productionUIs = mrp_productionDataPortal.GetDataByProduct_ID(item.Produce_id, item.Top_row);
                            viewModel.ProductionUIs = productionUIs;
                        }
                        catch
                        {

                        }
                        

                        if (dataUIByOper != null)
                        {
                            //tạo dong Daiily plan của 1 operation
                            SVN_targetViewModel dailyPlanVM = new SVN_targetViewModel()
                            {
                                Item = "H.Plan",
                                Target = dataUIByOper.Daily_plan,
                                Current = dataUIByOper.Total_Qty,
                                Percent = dataUIByOper.Daily_plan != 0 ? (dataUIByOper.Total_Qty / dataUIByOper.Daily_plan) * 100 : 0
                            };
                            SVN_targetViewModel UPHVM = new SVN_targetViewModel()
                            {
                                Item = "UPH",
                                Target = dataUIByOper.UPH,
                                Current = dataUIByOper.Current_UPH,
                                Percent = dataUIByOper.UPH != 0 ? (dataUIByOper.Current_UPH / dataUIByOper.UPH) * 100 : 0
                            };
                            SVN_targetViewModel UPPHVM = new SVN_targetViewModel()
                            {
                                Item = "UPPH",
                                Target = dataUIByOper.UPPH,
                                Current = dataUIByOper.Current_UPPH,
                                Percent = dataUIByOper.UPPH != 0 ? (dataUIByOper.Current_UPPH / dataUIByOper.UPPH) * 100 : 0
                            };
                            SVN_targetViewModel LaborVM = new SVN_targetViewModel()
                            {
                                Item = "Labor",
                                Target = dataUIByOper.Labor,
                                Current = dataUIByOper.MaxLabor,
                                Percent = dataUIByOper.Labor != 0 ? (dataUIByOper.MaxLabor / dataUIByOper.Labor) * 100 : 0
                            };
                            SVN_targetViewModel NGVM = new SVN_targetViewModel()
                            {
                                Item = "Defect",
                                Target = dataUIByOper.Defect * 100,
                                Current = dataUIByOper.Total_Qty != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty) * 100 : 0,
                                Percent = dataUIByOper.Total_Qty != 0 && dataUIByOper.Defect != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty / dataUIByOper.Defect) * 100 : 0
                            };
                            viewModel.TargetViewModels.Add(dailyPlanVM);
                            viewModel.TargetViewModels.Add(UPHVM);
                            viewModel.TargetViewModels.Add(UPPHVM);
                            viewModel.TargetViewModels.Add(LaborVM);
                            viewModel.TargetViewModels.Add(NGVM);
                            viewModel.Est = ((dataUIByOper.Current_UPH * 8.5) / dataUIByOper.Daily_plan) * 100;
                        }

                        viewModels.Add(viewModel);
                    }
                }
                return viewModels;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Lấy dữ liệu rồi load vào từng vùng của chart
        /// </summary>
        /// <param name="date"></param>
        /// <param name="oper"></param>
        /// <param name="storedProceduce"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public async Task<QtyProdResultByOperViewModel> GetDataByOperAndWC(string date, OperInfo oper, string storedProceduce, string tableName, string checkListConnection)
        {
            DateTime currentDate = DateTime.Now;
            try
            {
                currentDate = DateTime.ParseExact(date, "yyyyMMdd", null);
            }
            catch
            {
                currentDate = DateTime.Now;
            }
            QtyProdResultByOperViewModel viewModel = new QtyProdResultByOperViewModel();
            List<SVN_production_resultUI> dataUI = new List<SVN_production_resultUI>();
            List<SVN_target> targetDataUI = new List<SVN_target>(); // khai báo lớp dto để hứng dữ liệu
            List<SVN_Defect_recordUI> defect_RecordUI = new List<SVN_Defect_recordUI>();
            List<SVN_quantity_reasonUI> quantity_ReasonUI = new List<SVN_quantity_reasonUI>();
            var targetdataportal = new SVN_TargetDataPortal(connectionString); // gọi dataportal để sử dụng
            var mrp_productionDataPortal = new mrp_productionDataPortal(connectionString);
            var defectdataportal = new SVN_Defect_recordDataPortal(connectionString);
            var quntityreasondataportal = new SVN_quantity_reasonDataPortal(connectionString);
            var svnqachecklistreportdataportal = new SVNQACheckListReportDataPortal(checkListConnection);
            try
            {
                defect_RecordUI = await defectdataportal.ReadList(date);
                quantity_ReasonUI = await quntityreasondataportal.ReadList();
                targetDataUI = await targetdataportal.ReadList(date, storedProceduce);//lấy dữ liệu target từ csdl 
                dataUI = await ReadListByOperAndWC(date, oper.Operation, oper.WCName);
                if (dataUI.Count > 0)
                {
                    QtyProdResultViewModel val1 = new QtyProdResultViewModel();
                    QtyProdResultViewModel val2 = new QtyProdResultViewModel();
                    QtyProdResultViewModel val3 = new QtyProdResultViewModel();
                    QtyProdResultViewModel val4 = new QtyProdResultViewModel();
                    QtyProdResultViewModel val5 = new QtyProdResultViewModel();

                    val1.Time = "8h-10h";
                    val2.Time = "10h10-11h30";
                    val3.Time = "12h30-15h";
                    val4.Time = "15h10-17h30";
                    val5.Time = "18h-20h";

                    viewModel.Operation = oper.Operation;

                    //add defect by category
                    if (quantity_ReasonUI != null && defect_RecordUI != null)
                    {
                        var quantity_ReasonUI_by_oper = quantity_ReasonUI.Where(x => x.operation == oper.Operation).Select(x =>
                        {
                            DefectByCategoryViewModel model = new DefectByCategoryViewModel();
                            model.category = x.name;
                            model.value = defect_RecordUI.Where(y => y.Operation == oper.Operation && y.Defect_Code == x.code).Sum(y => y.Qty_NG).ToString();
                            viewModel.DefectByCategoryViewModels.Add(model);
                            return x;
                        }).ToList();
                    }

                    //get 5 ng lỡn nhất
                    if (viewModel.DefectByCategoryViewModels != null && viewModel.DefectByCategoryViewModels.Count > 0)
                    {
                        viewModel.DefectByCategoryViewModels = viewModel.DefectByCategoryViewModels.OrderByDescending(x => x.value).Take(3).ToList();
                    }

                    if (string.IsNullOrWhiteSpace(oper.WCName))
                    {
                        oper.WCName = null;
                    }
                    viewModel.WC = oper.WCName;

                    //sai ở đây
                    //dùng linq mà list đang bị null
                    var dataUIByOper = targetDataUI.FirstOrDefault(x => x.Operation == oper.Operation && x.WC == oper.WCName);//Lấy ra 1 dòng target theo opearation

                    var dataUIbyOperTarget = dataUI.FirstOrDefault(x => x.Operation == oper.Operation && x.WC == oper.WCName && x.Type_value == "Target");
                    if (dataUIbyOperTarget != null)
                    {
                        val1.Target = dataUIbyOperTarget.Time1;
                        val2.Target = dataUIbyOperTarget.Time2;
                        val3.Target = dataUIbyOperTarget.Time3;
                        val4.Target = dataUIbyOperTarget.Time4;
                        val5.Target = dataUIbyOperTarget.Time5;
                        if (dataUIbyOperTarget.Time1 == 0 && dataUIbyOperTarget.Time2 == 0 &&
                                dataUIbyOperTarget.Time3 == 0 && dataUIbyOperTarget.Time4 == 0 && dataUIbyOperTarget.Time5 == 0)
                        {
                            viewModel.IsProduction = false;
                        }
                        else
                        {
                            viewModel.IsProduction = true;
                        }
                    }
                    var dataUIbyOperLine = dataUI.FirstOrDefault(x => x.Operation == oper.Operation && x.WC == oper.WCName && x.Type_value == "Production Qty");
                    if (dataUIbyOperLine != null)
                    {
                        val1.Line = dataUIbyOperLine.Time1;
                        val2.Line = dataUIbyOperLine.Time2;
                        val3.Line = dataUIbyOperLine.Time3;
                        val4.Line = dataUIbyOperLine.Time4;
                        val5.Line = dataUIbyOperLine.Time5;

                        //Lấy dữ liệu Achieve và Forecast
                        viewModel.Achieve = dataUIbyOperLine.Achieve;
                        viewModel.Forecast = dataUIbyOperLine.Forecast;
                        viewModel.WORunning = dataUIbyOperLine.WORunning;
                        viewModel.Product = dataUIbyOperLine.Product;
                        viewModel.Customer = dataUIbyOperLine.Customer;
                    }
                    var dataUIbyOperManQty = dataUI.FirstOrDefault(x => x.Operation == oper.Operation && x.WC == oper.WCName && x.Type_value == "Man Q'ty");
                    if (dataUIbyOperManQty != null)
                    {
                        val1.ManQuantity = dataUIbyOperManQty.Time1;
                        val2.ManQuantity = dataUIbyOperManQty.Time2;
                        val3.ManQuantity = dataUIbyOperManQty.Time3;
                        val4.ManQuantity = dataUIbyOperManQty.Time4;
                        val5.ManQuantity = dataUIbyOperManQty.Time5;
                    }
                    var dataUIbyOperNGQty = dataUI.FirstOrDefault(x => x.Operation == oper.Operation && x.WC == oper.WCName && x.Type_value == "NG_Qty");
                    if (dataUIbyOperNGQty != null)
                    {
                        val1.NG = dataUIbyOperNGQty.Time1;
                        val2.NG = dataUIbyOperNGQty.Time2;
                        val3.NG = dataUIbyOperNGQty.Time3;
                        val4.NG = dataUIbyOperNGQty.Time4;
                        val5.NG = dataUIbyOperNGQty.Time5;
                    }
                    viewModel.ViewModels.Add(val1);
                    viewModel.ViewModels.Add(val2);
                    viewModel.ViewModels.Add(val3);
                    viewModel.ViewModels.Add(val4);
                    viewModel.ViewModels.Add(val5);

                    DateTime result = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                    string strDate = result.ToString("yyyy-MM-dd") + "  00:00:00.000";
                    var checkListData = await svnqachecklistreportdataportal.GetDataByDateAndOperation(strDate, oper.StoreID);
                    if (checkListData != null && checkListData.Count > 0)
                    {
                        if (checkListData[0].RestaurantStaffs.Contains("PD checked"))
                        {
                            viewModel.IsPDChecked = true;
                        }
                        else
                        {
                            viewModel.IsPDChecked = false;
                        }
                        if (checkListData[0].RestaurantStaffs.Contains("MT checked") || checkListData[0].RestaurantStaffs.Contains("ENG checked"))
                        {
                            viewModel.IsMTChecked = true;
                        }
                        else
                        {
                            viewModel.IsMTChecked = false;
                        }
                        if (checkListData[0].RestaurantStaffs.Contains("QC checked"))
                        {
                            viewModel.IsQCChecked = true;
                        }
                        else
                        {
                            viewModel.IsQCChecked = false;
                        }
                        if (checkListData[0].ConfirmStatus == "Y")
                        {
                            viewModel.IsPDConfirmed = true;
                        }
                        else
                        {
                            viewModel.IsPDConfirmed = false;
                        }
                        if (checkListData[0].PointBSC >= 4)
                        {
                            viewModel.IsQCConfirmed = true;
                        }
                        else
                        {
                            viewModel.IsQCConfirmed = false;
                        }


                        if (viewModel.IsPDChecked && viewModel.IsMTChecked && viewModel.IsQCChecked &&
                            viewModel.IsPDConfirmed && viewModel.IsQCConfirmed)
                        {
                            viewModel.CanProduction = true;
                        }
                        else
                        {
                            viewModel.CanProduction = false;
                        }
                    }
                    else
                    {
                        viewModel.CanProduction = false;
                    }

                    try
                    {
                        var productionUIs = mrp_productionDataPortal.GetDataByProduct_ID(oper.Produce_id, oper.Top_row);
                        viewModel.ProductionUIs = productionUIs;
                    }
                    catch
                    {

                    }
                    if (dataUIByOper != null)
                    {
                        double HPlanTarget = dataUIByOper.Daily_plan;
                        double UPHCurrent = dataUIByOper.Current_UPH;
                        double UPPHCurrent = dataUIByOper.Current_UPPH;
                        double workingTime = 0;

                        //Lấy ra các section có target
                        var listSection = viewModel.ViewModels.Where(x => x.Target != 0);
                        var minSection = listSection.FirstOrDefault() != null ? listSection.FirstOrDefault().Time : "";
                        var maxSection = listSection.LastOrDefault() != null ? listSection.LastOrDefault().Time : "";

                        if (!string.IsNullOrWhiteSpace(minSection) && 
                            !string.IsNullOrWhiteSpace(maxSection) && 
                            currentDate.Date == DateTime.Now.Date)
                        {
                            DateTime curDateTime = DateTime.Now;
                            DateTime today = currentDate;
                            var minTimes = minSection.Split('-');

                            // Chuyển đổi thành định dạng HH:mm
                            string startTime = minTimes[0].Replace("h", ":");
                            if (startTime.Last() == ':')
                            {
                                startTime = startTime + "00";
                            }
                            if (startTime.Length == 4)
                            {
                                startTime = "0" + startTime;
                            }
                            DateTime startDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + startTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                            var maxTimes = maxSection.Split('-');
                            // Chuyển đổi thành định dạng HH:mm
                            string endTime = maxTimes[1].Replace("h", ":");
                            if (endTime.Last() == ':')
                            {
                                endTime = endTime + "00";
                            }
                            if (endTime.Length == 4)
                            {
                                endTime = "0" + endTime;
                            }
                            DateTime endDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + endTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                            if(curDateTime < startDatetime)
                            {
                                workingTime = 0;
                            }
                            else if(startDatetime < curDateTime && curDateTime < endDatetime)
                            {
                                // Lấy hiệu 2 thời điểm
                                TimeSpan diff = curDateTime - startDatetime;
                                workingTime = Math.Round(diff.TotalMinutes / 60.0, 2);
                            }
                            else if (endDatetime < curDateTime)
                            {
                                // Lấy hiệu 2 thời điểm
                                TimeSpan diff = endDatetime - startDatetime;
                                workingTime = Math.Round(diff.TotalMinutes / 60.0, 2);
                            }

                            // Tính Current UPH và UPPH
                            var uph = workingTime != 0 ? Math.Round(dataUIByOper.Total_Qty / workingTime, 2) : 0;
                            UPHCurrent = Math.Round(dataUIByOper.Total_Qty / workingTime, 2);
                            UPPHCurrent = Math.Round(UPHCurrent / dataUIByOper.MaxLabor, 2);
                            // Target by Hour
                            HPlanTarget = Math.Round(dataUIByOper.UPH * workingTime);
                        }

                        //tạo dong Daiily plan của 1 operation
                        SVN_targetViewModel dailyPlanVM = new SVN_targetViewModel()
                        {
                            Item = "H.Plan",
                            Target = HPlanTarget,
                            Current = dataUIByOper.Total_Qty,
                            Percent = HPlanTarget != 0 ? (dataUIByOper.Total_Qty / HPlanTarget) * 100 : 0
                        };
                        SVN_targetViewModel UPHVM = new SVN_targetViewModel()
                        {
                            Item = "UPH",
                            Target = dataUIByOper.UPH,
                            Current = UPHCurrent,
                            Percent = dataUIByOper.UPH != 0 ? (UPHCurrent / dataUIByOper.UPH) * 100 : 0
                        };
                        SVN_targetViewModel UPPHVM = new SVN_targetViewModel()
                        {
                            Item = "UPPH",
                            Target = dataUIByOper.UPPH,
                            Current = UPPHCurrent,
                            Percent = dataUIByOper.UPPH != 0 ? (UPPHCurrent / dataUIByOper.UPPH) * 100 : 0
                        };
                        SVN_targetViewModel LaborVM = new SVN_targetViewModel()
                        {
                            Item = "Labor",
                            Target = dataUIByOper.Labor,
                            Current = dataUIByOper.MaxLabor,
                            Percent = dataUIByOper.Labor != 0 ? (dataUIByOper.MaxLabor / dataUIByOper.Labor) * 100 : 0
                        };
                        SVN_targetViewModel NGVM = new SVN_targetViewModel()
                        {
                            Item = "Defect",
                            Target = Math.Round(dataUIByOper.Defect * 100, 2),
                            Current = Math.Round(dataUIByOper.Total_Qty != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty) * 100 : 0, 2),
                            Percent = Math.Round(dataUIByOper.Total_Qty != 0 && dataUIByOper.Defect != 0 ? (dataUIByOper.Total_NG_Qty / dataUIByOper.Total_Qty / dataUIByOper.Defect) * 100 : 0, 2)
                        };
                        viewModel.TargetViewModels.Add(dailyPlanVM);
                        viewModel.TargetViewModels.Add(UPHVM);
                        viewModel.TargetViewModels.Add(UPPHVM);
                        viewModel.TargetViewModels.Add(LaborVM);
                        viewModel.TargetViewModels.Add(NGVM);
                        viewModel.Est = ((dataUIByOper.Current_UPH * 8.5) / dataUIByOper.Daily_plan) * 100;
                    }
                }
                return viewModel;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
