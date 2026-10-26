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
            RejectPO
        }

        public enum LogType
        {
            Info,
            Warning,
            Error
        }
    }
}
