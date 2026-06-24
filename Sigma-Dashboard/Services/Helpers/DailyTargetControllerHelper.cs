using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sigma_Dashboard.Services.Helpers
{
    public class DailyTargetControllerHelper
    {
        string connectionString;
        DBConfiguration dBConfiguration;
        public DailyTargetControllerHelper(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }

        public async Task<List<DailyTargetViewModel>> GetDailyTargetData(DateTime date, string shift, string companyCode)
        {
            List<DailyTargetViewModel> dailyTargetData = new List<DailyTargetViewModel>();
            List<SVN_target_v1UI> svnTargetData = new List<SVN_target_v1UI>();
            SVN_Target_v1DataPortal dataPortal = new SVN_Target_v1DataPortal(connectionString);
            try
            {
                svnTargetData = await dataPortal.ReadListTargetByDate(date.ToString("yyyyMMdd"));
                if (svnTargetData != null && svnTargetData.Count > 0)
                {
                    dailyTargetData = svnTargetData.Select(s => new DailyTargetViewModel
                    {
                        Operation = s.Operation,
                        Daily_plan = s.Daily_plan,
                        UPH = s.UPH,
                        UPPH = s.UPPH,
                        Labor = s.Labor,
                        Date_time = s.Date_time,
                        Defect = s.Defect,
                        Workingtime = s.Workingtime,
                        Shift = !string.IsNullOrWhiteSpace(s.Shift) ? s.Shift.Trim() : shift
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions and log errors as needed
            }
            return dailyTargetData;
        }

        public async Task<BODataProcessResult> InsertData(DailyTargetViewModel viewModel)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Target_v1DataPortal dataPortal = new SVN_Target_v1DataPortal(connectionString);
            try
            {
                var existingData = await dataPortal.ReadTargetByDateAndOperation(viewModel.Date_time, viewModel.Operation);
                if (existingData != null) 
                {
                    processResult.OK = false;
                    processResult.Message = $"Data for Operation '{viewModel.Operation}' on Date '{viewModel.Date_time}' already exists.";
                }
           

                List<SVN_target_v1UI> svnTargetData = new List<SVN_target_v1UI>
                {
                    new SVN_target_v1UI
                    {
                        Operation = viewModel.Operation,
                        Daily_plan = viewModel.Daily_plan,
                        UPH = viewModel.UPH,
                        UPPH = viewModel.UPPH,
                        Labor = viewModel.Labor,
                        Date_time = viewModel.Date_time,
                        Defect = viewModel.Defect,
                        Workingtime = viewModel.Workingtime,
                        Shift = viewModel.Shift
                    }
                };
                var result = dataPortal.InsertTargetBulk(svnTargetData);
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

        public async Task<BODataProcessResult> UpdateData(DailyTargetViewModel viewModel)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Target_v1DataPortal dataPortal = new SVN_Target_v1DataPortal(connectionString);
            try
            {
                var existingData = await dataPortal.ReadTargetByDateAndOperation(viewModel.Date_time, viewModel.Operation);
                if (existingData == null)
                {
                    processResult.OK = false;
                    processResult.Message = $"No existing data found for Operation '{viewModel.Operation}' on Date '{viewModel.Date_time}'.";
                    return processResult;
                }
                List<SVN_target_v1UI> svnTargetData = new List<SVN_target_v1UI>
                {
                    new SVN_target_v1UI
                    {
                        Operation = viewModel.Operation,
                        Daily_plan = viewModel.Daily_plan,
                        UPH = viewModel.UPH,
                        UPPH = viewModel.UPPH,
                        Labor = viewModel.Labor,
                        Date_time = viewModel.Date_time,
                        Defect = viewModel.Defect,
                        Workingtime = viewModel.Workingtime,
                        Shift = viewModel.Shift
                    }
                };
                var result = dataPortal.UpdateTargetBulk(svnTargetData);
                if (result > 0)
                {
                    processResult.OK = true;
                    processResult.Message = "Data updated successfully.";
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Failed to update data.";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"Error updating data: {ex.Message}";
            }
            return processResult;
        }

        public async Task<BODataProcessResult> DeleteData(string date, string operation)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Target_v1DataPortal dataPortal = new SVN_Target_v1DataPortal(connectionString);
            try
            {
                var existingData = await dataPortal.ReadTargetByDateAndOperation(date, operation);
                if (existingData == null)
                {
                    processResult.OK = false;
                    processResult.Message = $"No existing data found for Operation '{operation}' on Date '{date}'.";
                    return processResult;
                }
                var result = dataPortal.DeleteTarget(date, operation);
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

        public async Task<BODataProcessResult> InsertAndUpdateData(List<DailyTargetViewModel> viewModels)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Target_v1DataPortal dataPortal = new SVN_Target_v1DataPortal(connectionString);
            List<SVN_target_v1UI> InsertTargetData = new List<SVN_target_v1UI>();
            List<SVN_target_v1UI> UpdateTargetData = new List<SVN_target_v1UI>();
            try
            {
                foreach (var model in viewModels)
                {
                    var existData = await dataPortal.ReadTargetByDateAndOperation(model.Date_time, model.Operation);
                    if(existData != null)
                    {
                        UpdateTargetData.Add(new SVN_target_v1UI()
                        {
                            Operation = model.Operation,
                            Daily_plan = model.Daily_plan,
                            UPH = model.UPH,
                            UPPH = model.UPPH,
                            Labor = model.Labor,
                            Date_time = model.Date_time,
                            Defect = model.Defect,
                            Workingtime = model.Workingtime,
                            Shift = model.Shift
                        });
                    }
                    else
                    {
                        InsertTargetData.Add(new SVN_target_v1UI()
                        {
                            Operation = model.Operation,
                            Daily_plan = model.Daily_plan,
                            UPH = model.UPH,
                            UPPH = model.UPPH,
                            Labor = model.Labor,
                            Date_time = model.Date_time,
                            Defect = model.Defect,
                            Workingtime = model.Workingtime,
                            Shift = model.Shift
                        });
                    }
                }

                if(InsertTargetData.Count > 0)
                {
                    var insertResult = dataPortal.InsertTargetBulk(InsertTargetData);
                    if (insertResult <= 0)
                    {
                        processResult.OK = false;
                        processResult.Message = "Failed to insert data.";
                    }
                }

                if (UpdateTargetData.Count > 0)
                {
                    var updateResult = dataPortal.UpdateTargetBulk(UpdateTargetData);
                    if (updateResult <= 0)
                    {
                        processResult.OK = false;
                        processResult.Message = processResult.Message + " Failed to update data.";
                    }
                }

                if(InsertTargetData.Count > 0 && UpdateTargetData.Count > 0)
                {
                    processResult.OK = true;
                    processResult.Message = "Data inserted and updated successfully.";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
