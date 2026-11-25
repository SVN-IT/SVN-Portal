using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Utils
{
    public class LogService
    {
        string connectionString;
        public LogService(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void Log(LogApp App, LogAction Action, LogType Type, string Content, string userCode = "System")
        {
            SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
            SVN_summary_logUI log = new SVN_summary_logUI()
            {
                Id = Guid.NewGuid(),
                App = App.ToString(),
                Action = Action.ToString(),
                Type = Type.ToString(),
                Content = Content,
                CreatedBy = userCode,
                CreatedOn = DateTime.Now,
                ModifiedBy = userCode,
                ModifiedOn = DateTime.Now
            };

            try
            {
                var result = dataPortal.InsertBulk(new List<SVN_summary_logUI>() { log });
            }
            catch(Exception ex)
            {
                log.Content += $" | Log Error: {ex.Message}";
                var result = dataPortal.InsertBulk(new List<SVN_summary_logUI>() { log });
            }
        }

        public List<SVN_summary_logUI> GetLogs(LogAction Action, DateTime fromDate, DateTime toDate)
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                return dataPortal.ReadListFromDateToData(Action.ToString(), fromDate, toDate);
            }
            catch(Exception ex)
            {
                Log(LogApp.SVNPortal, LogAction.NetsuitePOLogInfo, LogType.Error, $"GetLogs Error: {ex.Message}");
                return null;
            }
        }

        public int SaveTransactionState(Guid id, string content, string status, string userCode = "System")
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                SVN_summary_logUI log = new SVN_summary_logUI()
                {
                    Id = id,
                    App = LogApp.SigmaTools.ToString(),
                    Action = LogAction.POTransactionState.ToString(),
                    Type = status,
                    Content = content,
                    CreatedBy = userCode,
                    CreatedOn = DateTime.Now,
                    ModifiedBy = userCode,
                    ModifiedOn = DateTime.Now
                };
                return dataPortal.Upsert(log);
            }
            catch (Exception ex)
            {
                Log(LogApp.SigmaTools, LogAction.POTransactionState, LogType.Error, $"SaveTransactionState Error: {ex.Message}", userCode);
                return -1;
            }
        }

        public SVN_summary_logUI GetTransactionState(Guid id)
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                return dataPortal.GetById(id);
            }
            catch (Exception ex)
            {
                Log(LogApp.SigmaTools, LogAction.POTransactionState, LogType.Error, $"GetTransactionState Error: {ex.Message}");
                return null;
            }
        }

        public int DeleteTransactionState(Guid id, string userCode = "System")
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                return dataPortal.DeleteTransactionState(id);
            }
            catch (Exception ex)
            {
                Log(LogApp.SigmaTools, LogAction.POTransactionState, LogType.Error, $"DeleteTransactionState Error: {ex.Message}", userCode);
                return -1;
            }
        }

        /// <summary>
        /// Tìm lịch sử các PO đã được tạo thành công trước đây theo Customer PO Number.
        /// </summary>
        public List<SVN_summary_logUI> FindCreatedPurchaseOrderLogs(string customerPoNumber)
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                return dataPortal.FindCreatedPurchaseOrderLogs(customerPoNumber);
            }
            catch (Exception ex)
            {
                Log(LogApp.SigmaTools, LogAction.POTransactionState, LogType.Warning, $"FindCreatedPurchaseOrderLogs Error: {ex.Message}");
                return new List<SVN_summary_logUI>();
            }
        }

        /// <summary>
        /// Tra cứu các log NetsuitePOLogInfo theo danh sách mã SigmaPO để lấy PO/SO URL.
        /// </summary>
        public List<SVN_summary_logUI> FindTrackingLogsBySigmaPOs(IEnumerable<string> sigmaPos)
        {
            try
            {
                SVN_summary_logDataPortal dataPortal = new SVN_summary_logDataPortal(connectionString);
                return dataPortal.FindTrackingLogsBySigmaPOs(sigmaPos);
            }
            catch (Exception ex)
            {
                Log(LogApp.SigmaTools, LogAction.NetsuitePOLogInfo, LogType.Warning, $"FindTrackingLogsBySigmaPOs Error: {ex.Message}");
                return new List<SVN_summary_logUI>();
            }
        }

        public enum LogApp
        {
            QACheckList,
            DefectManagement,
            SVNPortal,
            SigmaCloudPortal,
            SVNAPI,
            SigmaTools
        }

        public enum LogAction
        {
            AutoInputProduction,
            InputProduction,
            PurchasePrequest,
            OCRAIRequest,
            ConfigPOManual,
            CreateNetsuitePO,
            VerifyErpPrices,
            RejectPO,
            NetsuitePOLogInfo,
            POTransactionState
        }

        public enum LogType
        {
            Info,
            Warning,
            Error
        }
    }
}
