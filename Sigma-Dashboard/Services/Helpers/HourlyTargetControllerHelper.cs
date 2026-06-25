using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;

namespace Sigma_Dashboard.Services.Helpers
{
    public class HourlyTargetControllerHelper
    {
        string connectionString;
        DBConfiguration dBConfiguration;
        public HourlyTargetControllerHelper(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }

        public async Task<List<HourlyTargetViewModel>> GetHourlyTargetData(DateTime date, string shift, string companyCode)
        {
            List<HourlyTargetViewModel> hourlyTargetData = new List<HourlyTargetViewModel>();
            List<SVN_production_result_v1UI> productionDataUI = new List<SVN_production_result_v1UI>();
            SVN_production_result_v1DataPortal dataPortal = new SVN_production_result_v1DataPortal(connectionString);
            try
            {
                productionDataUI = await dataPortal.ReadList(date.ToString("yyyyMMdd"));
                if (productionDataUI != null && productionDataUI.Count > 0)
                {
                    hourlyTargetData = productionDataUI.Select(p => new HourlyTargetViewModel
                    {
                        Type_value = p.Type_value,
                        Time1 = p.Time1,
                        Time2 = p.Time2,
                        Time3 = p.Time3,
                        Time4 = p.Time4,
                        Time5 = p.Time5,
                        Time6 = p.Time6,
                        Operation = p.Operation,
                        Date_time = p.Date_time,
                        WC = p.WC,
                        Achieve = p.Achieve,
                        Forecast = p.Forecast,
                        WORunning = p.WORunning,
                        Product = p.Product,
                        Customer = p.Customer,
                        Shift = !string.IsNullOrWhiteSpace(p.Shift) ? p.Shift.Trim() : shift
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions and log errors as needed
            }
            return hourlyTargetData;
        }

        public async Task<BODataProcessResult> InsertData(HourlyTargetViewModel viewModel)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_production_result_v1DataPortal dataPortal = new SVN_production_result_v1DataPortal(connectionString);

            List<SVN_production_result_v1UI> insertData = new List<SVN_production_result_v1UI>();
            SVN_production_result_v1UI typeValueDataUI = new SVN_production_result_v1UI
            {
                Type_value = viewModel.Type_value,
                Time1 = viewModel.Time1,
                Time2 = viewModel.Time2,
                Time3 = viewModel.Time3,
                Time4 = viewModel.Time4,
                Time5 = viewModel.Time5,
                Time6 = viewModel.Time6,
                Operation = viewModel.Operation,
                Date_time = viewModel.Date_time,
                WC = viewModel.WC,
                Achieve = viewModel.Achieve,
                Forecast = viewModel.Forecast,
                WORunning = viewModel.WORunning,
                Product = viewModel.Product,
                Customer = viewModel.Customer,
                Shift = viewModel.Shift
            };
            SVN_production_result_v1UI productionDataUI = new SVN_production_result_v1UI();
            SVN_production_result_v1UI manQtyDataUI = new SVN_production_result_v1UI();
            try
            {
                var existingData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, viewModel.Type_value);
                if (existingData != null)
                {
                    processResult.OK = false;
                    processResult.Message = $"Data for Operation '{viewModel.Operation}' on Date '{viewModel.Date_time}' on Shift '{viewModel.Shift}' and Type_value '{viewModel.Type_value}' already exists.";
                    return processResult;
                }
                insertData.Add(typeValueDataUI);

                var productionData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, "Production Qty");
                if (productionData == null) 
                {
                    productionDataUI = new SVN_production_result_v1UI
                    {
                        Type_value = "Production Qty",
                        Time1 = 0,
                        Time2 = 0,
                        Time3 = 0,
                        Time4 = 0,
                        Time5 = 0,
                        Time6 = 0,
                        Operation = viewModel.Operation,
                        Date_time = viewModel.Date_time,
                        WC = viewModel.WC,
                        Achieve = 0,
                        Shift = viewModel.Shift
                    };
                    insertData.Add(productionDataUI);
                }

                var manQtyData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, "NG_Qty");
                if (manQtyData == null) 
                {
                    manQtyDataUI = new SVN_production_result_v1UI
                    {
                        Type_value = "NG_Qty",
                        Time1 = 0,
                        Time2 = 0,
                        Time3 = 0,
                        Time4 = 0,
                        Time5 = 0,
                        Time6 = 0,
                        Operation = viewModel.Operation,
                        Date_time = viewModel.Date_time,
                        WC = viewModel.WC,
                        Achieve = 0,
                        Shift = viewModel.Shift
                    };
                    insertData.Add(manQtyDataUI);
                }

                var result = dataPortal.InsertHourlyTargetBulk(insertData);
                if (result > 0)
                {
                    processResult.OK = true;
                    processResult.Message = "Data inserted successfully.";
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Failed to insert data.";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"Error inserting data: {ex.Message}";
            }
            return processResult;
        }

        public async Task<BODataProcessResult> UpdateData(HourlyTargetViewModel viewModel)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_production_result_v1DataPortal dataPortal = new SVN_production_result_v1DataPortal(connectionString);

            List<SVN_production_result_v1UI> updateData = new List<SVN_production_result_v1UI>();
            List<SVN_production_result_v1UI> insertData = new List<SVN_production_result_v1UI>();

            // 1. Ánh xạ dữ liệu cần Update từ ViewModel sang UI Model
            SVN_production_result_v1UI typeValueDataUI = new SVN_production_result_v1UI
            {
                Type_value = viewModel.Type_value,
                Time1 = viewModel.Time1,
                Time2 = viewModel.Time2,
                Time3 = viewModel.Time3,
                Time4 = viewModel.Time4,
                Time5 = viewModel.Time5,
                Time6 = viewModel.Time6,
                Operation = viewModel.Operation,
                Date_time = viewModel.Date_time,
                WC = viewModel.WC,
                Achieve = viewModel.Achieve,
                Forecast = viewModel.Forecast,
                WORunning = viewModel.WORunning,
                Product = viewModel.Product,
                Customer = viewModel.Customer,
                Shift = viewModel.Shift
            };

            try
            {
                // 2. KIỂM TRA BẢN GHI CẦN SỬA: Ở hàm UPDATE, nếu KHÔNG tồn tại thì mới báo lỗi
                var existingData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, viewModel.Type_value);
                if (existingData == null)
                {
                    processResult.OK = false;
                    processResult.Message = $"Không tìm thấy dữ liệu của Công đoạn '{viewModel.Operation}' ngày '{viewModel.Date_time}' ca '{viewModel.Shift}' loại '{viewModel.Type_value}' để cập nhật.";
                    return processResult;
                }

                // Thêm vào danh sách chờ Update
                updateData.Add(typeValueDataUI);

                // 3. KIỂM TRA & TỰ ĐỘNG KHỞI TẠO DÒNG "Production Qty" NẾU CHƯA CÓ
                var productionData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, "Production Qty");
                if (productionData == null)
                {
                    insertData.Add(new SVN_production_result_v1UI
                    {
                        Type_value = "Production Qty",
                        Time1 = 0,
                        Time2 = 0,
                        Time3 = 0,
                        Time4 = 0,
                        Time5 = 0,
                        Time6 = 0,
                        Operation = viewModel.Operation,
                        Date_time = viewModel.Date_time,
                        WC = viewModel.WC,
                        Achieve = 0,
                        Shift = viewModel.Shift,
                        Forecast = 0,
                        WORunning = "",
                        Product = viewModel.Product,
                        Customer = ""
                    });
                }

                // 4. KIỂM TRA & TỰ ĐỘNG KHỞI TẠO DÒNG "Man Q'ty" NẾU CHƯA CÓ
                var manQtyData = await dataPortal.ReadTypeValueByOperation(viewModel.Date_time, viewModel.Operation, viewModel.Shift, "NG_Qty");
                if (manQtyData == null)
                {
                    insertData.Add(new SVN_production_result_v1UI
                    {
                        Type_value = "NG_Qty",
                        Time1 = 0,
                        Time2 = 0,
                        Time3 = 0,
                        Time4 = 0,
                        Time5 = 0,
                        Time6 = 0,
                        Operation = viewModel.Operation,
                        Date_time = viewModel.Date_time,
                        WC = viewModel.WC,
                        Achieve = 0,
                        Shift = viewModel.Shift,
                        Forecast = 0,
                        WORunning = "",
                        Product = viewModel.Product,
                        Customer = ""
                    });
                }

                // 5. THỰC THI LƯU XUỐNG CƠ SỞ DỮ LIỆU
                int rowsUpdated = 0;
                int rowsInserted = 0;

                // Chạy Update dữ liệu chính
                if (updateData.Count > 0)
                {
                    rowsUpdated = dataPortal.UpdateHourlyTargetBulk(updateData);
                }

                // CHẮT LỌC LỖI: Chạy lệnh Insert bổ sung cho các dòng phụ trợ (Dòng code cũ của bạn đang bỏ quên đoạn này)
                if (insertData.Count > 0)
                {
                    rowsInserted = dataPortal.InsertHourlyTargetBulk(insertData);
                }

                // 6. PHÂN TÍCH KẾT QUẢ ĐỂ TRẢ VỀ THÔNG BÁO CHI TIẾT
                if (rowsUpdated > 0)
                {
                    processResult.OK = true;
                    if (rowsInserted > 0)
                    {
                        processResult.Message = $"Cập nhật thành công dòng dữ liệu chính và tự động tạo mới {rowsInserted} dòng cấu hình phụ trợ hệ thống.";
                    }
                    else
                    {
                        processResult.Message = "Cập nhật dữ liệu Hourly Target thành công.";
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Quá trình cập nhật dữ liệu thất bại, không có dòng nào được thay đổi dưới DB.";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"Đã xảy ra lỗi trong tiến trình xử lý DB (Update/Insert): {ex.Message}";
            }

            return processResult;
        }

        public async Task<BODataProcessResult> DeleteData(string date, string operation, string type_value, string shift)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_production_result_v1DataPortal dataPortal = new SVN_production_result_v1DataPortal(connectionString);
            try
            {
                var existingData = await dataPortal.ReadTypeValueByOperation(date, operation, shift, type_value);
                if (existingData == null)
                {
                    processResult.OK = false;
                    processResult.Message = $"No existing data found for Operation '{operation}' on Date '{date}'.";
                    return processResult;
                }
                var result = await dataPortal.DeleteHourlyTarget(date, operation, type_value, shift);
                if (result > 0)
                {
                    processResult.OK = true;
                    processResult.Message = "Data deleted successfully.";
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Failed to delete data.";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"Error deleting data: {ex.Message}";
            }
            return processResult;
        }

        public async Task<BODataProcessResult> InsertAndUpdateData(List<HourlyTargetViewModel> listViewModels)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            if (listViewModels == null || !listViewModels.Any())
            {
                processResult.OK = true;
                processResult.Message = "Không có dữ liệu nào cần xử lý.";
                return processResult;
            }

            SVN_production_result_v1DataPortal dataPortal = new SVN_production_result_v1DataPortal(connectionString);

            // Khởi tạo các rổ chứa dữ liệu phân loại sau khi quét
            List<SVN_production_result_v1UI> finalUpdateList = new List<SVN_production_result_v1UI>();
            List<SVN_production_result_v1UI> finalInsertList = new List<SVN_production_result_v1UI>();

            try
            {
                // Duyệt qua từng dòng dữ liệu truyền vào (từ Form hoặc từ File Excel)
                foreach (var viewModel in listViewModels)
                {
                    // Kiểm tra xem dòng hiện tại đã tồn tại dưới DB chưa dựa trên tổ hợp khóa chính
                    var existingData = await dataPortal.ReadTypeValueByOperation(
                        viewModel.Date_time, viewModel.Operation, viewModel.Shift, viewModel.Type_value
                    );

                    // Tạo đối tượng UI Model map từ dữ liệu hiện tại
                    var currentDataUI = new SVN_production_result_v1UI
                    {
                        Type_value = viewModel.Type_value,
                        Time1 = viewModel.Time1,
                        Time2 = viewModel.Time2,
                        Time3 = viewModel.Time3,
                        Time4 = viewModel.Time4,
                        Time5 = viewModel.Time5,
                        Time6 = viewModel.Time6,
                        Operation = viewModel.Operation,
                        Date_time = viewModel.Date_time,
                        WC = viewModel.WC,
                        Achieve = viewModel.Achieve,
                        Forecast = viewModel.Forecast,
                        WORunning = viewModel.WORunning,
                        Product = viewModel.Product,
                        Customer = viewModel.Customer,
                        Shift = viewModel.Shift
                    };

                    if (existingData != null)
                    {
                        // PHÂN LOẠI 1: Nếu đã tồn tại dòng này -> Đưa vào danh sách chờ UPDATE
                        finalUpdateList.Add(currentDataUI);
                    }
                    else
                    {
                        // PHÂN LOẠI 2: Nếu chưa có dòng này -> Đưa vào danh sách chờ INSERT dữ liệu chính
                        finalInsertList.Add(currentDataUI);
                    }

                    // ─── TỰ ĐỘNG SINH CÁC DÒNG PHỤ TRỢ ───

                    // Khởi tạo phụ trợ 1: "Production Qty" (nếu DB chưa có dòng này của công đoạn đó)
                    var productionData = await dataPortal.ReadTypeValueByOperation(
                        viewModel.Date_time, viewModel.Operation, viewModel.Shift, "Production Qty"
                    );
                    // Đồng thời check tránh add trùng lặp ngay trong danh sách insert tạm
                    if (productionData == null && !finalInsertList.Any(x => x.Operation == viewModel.Operation && x.Date_time == viewModel.Date_time && x.Shift == viewModel.Shift && x.Type_value == "Production Qty"))
                    {
                        finalInsertList.Add(new SVN_production_result_v1UI
                        {
                            Type_value = "Production Qty",
                            Time1 = 0,
                            Time2 = 0,
                            Time3 = 0,
                            Time4 = 0,
                            Time5 = 0,
                            Time6 = 0,
                            Operation = viewModel.Operation,
                            Date_time = viewModel.Date_time,
                            WC = viewModel.WC,
                            Achieve = 0,
                            Shift = viewModel.Shift,
                            Forecast = 0,
                            WORunning = "",
                            Product = viewModel.Product,
                            Customer = ""
                        });
                    }

                    // Khởi tạo phụ trợ 2: "Man Q'ty" (nếu DB chưa có dòng này của công đoạn đó)
                    var manQtyData = await dataPortal.ReadTypeValueByOperation(
                        viewModel.Date_time, viewModel.Operation, viewModel.Shift, "NG_Qty"
                    );
                    if (manQtyData == null && !finalInsertList.Any(x => x.Operation == viewModel.Operation && x.Date_time == viewModel.Date_time && x.Shift == viewModel.Shift && x.Type_value == "NG_Qty"))
                    {
                        finalInsertList.Add(new SVN_production_result_v1UI
                        {
                            Type_value = "NG_Qty",
                            Time1 = 0,
                            Time2 = 0,
                            Time3 = 0,
                            Time4 = 0,
                            Time5 = 0,
                            Time6 = 0,
                            Operation = viewModel.Operation,
                            Date_time = viewModel.Date_time,
                            WC = viewModel.WC,
                            Achieve = 0,
                            Shift = viewModel.Shift,
                            Forecast = 0,
                            WORunning = "",
                            Product = viewModel.Product,
                            Customer = ""
                        });
                    }
                }

                // ─── THỰC THI GHI XUỐNG CƠ SỞ DỮ LIỆU SỬ DỤNG TRANSACTION ───
                int totalUpdated = 0;
                int totalInserted = 0;

                // Gọi lệnh thực thi Update hàng loạt nếu có
                if (finalUpdateList.Any())
                {
                    totalUpdated = dataPortal.UpdateHourlyTargetBulk(finalUpdateList);
                }

                // Gọi lệnh thực thi Insert hàng loạt nếu có (Bao gồm cả dòng chính mới + dòng phụ trợ)
                if (finalInsertList.Any())
                {
                    totalInserted = dataPortal.InsertHourlyTargetBulk(finalInsertList);
                }

                // Trả về báo cáo tổng hợp số lượng xử lý thực tế
                processResult.OK = true;
                processResult.Message = $"Xử lý dữ liệu hoàn tất! Hệ thống đã cập nhật thành công {totalUpdated} bản ghi cũ và thêm mới thành công {totalInserted} bản ghi (bao gồm dữ liệu mới và dòng phụ trợ sinh tự động).";
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"Gặp sự cố lỗi trong quá trình đồng bộ dữ liệu Bulk: {ex.Message}";
            }

            return processResult;
        }
    }
}
