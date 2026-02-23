using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.Services.Configurations;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SVN_Portal.Controllers
{
    public class ReportController : Controller
    {
        DBConfiguration dBConfiguration;
        string connectionString;
        public ReportController(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }
        public IActionResult Index()
        {
            return View();
        }

        #region FN
        public IActionResult WOAnalisisReport(DateTime fromDate, DateTime toDate)
        {
            string storedProcedure = "SVN_ERP_savedsearch_779_803";
            List<workOrderInfoUI> workOrderModels = new List<workOrderInfoUI>();
            List<SVN_SavedSearch_FormulaUI> sVN_SavedSearch_FormulaUIs = new List<SVN_SavedSearch_FormulaUI>();

            var dataPortal = new mrp_productionDataPortal(connectionString);
            var formulaDataPortal = new SVN_SavedSearch_FormulaDataPortal(connectionString);
            workOrderModels = dataPortal.GetWorkOrderInfo();
            sVN_SavedSearch_FormulaUIs = formulaDataPortal.GetSavedSearchFormulaData(storedProcedure);

            if (workOrderModels != null && workOrderModels.Count > 0) 
            {
                //var grouped = workOrderModels.GroupBy(x => x.WO_FGID);


                //foreach (var group in grouped)
                //{
                //    decimal mat = 0;
                //    var list = group.ToList();
                //    var matFormularUI = sVN_SavedSearch_FormulaUIs.FirstOrDefault(x => x.Field == "Mat");
                //    if (matFormularUI != null)
                //    {
                //        mat = ExecuteSumFormula(matFormularUI.Formula, matFormularUI.Regex, list);
                //    }

                //    foreach (var item in list)
                //    {
                //        item.Mat = mat;

                //    }
                //}
                var list = CalculateCost(workOrderModels);
            }


            return View();
        }
        #endregion

        #region chưa dùng
        private decimal ExecuteSumFormula(string formula, string regex, List<workOrderInfoUI> group)
        {
            // Lấy field RMB
            // Lấy điều kiện Type='Work Order Issue'

            var match = Regex.Match(formula, @regex);

            if (!match.Success) return 0;

            var sumField = match.Groups[1].Value.Trim(); // RMB
            var conditionField = match.Groups[2].Value.Trim(); // Type
            var conditionValue = match.Groups[3].Value.Trim(); // Work Order Issue

            return group
                .Where(x => GetPropertyValue(x, conditionField)?.ToString() == conditionValue)
                .Sum(x => Convert.ToDecimal(GetPropertyValue(x, sumField)));
        }

        private object GetPropertyValue(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
        }
        #endregion

        #region config
        private static decimal ParseDecimal(string? value)
        {
            return decimal.TryParse(value, out var result) ? result : 0;
        }

        public List<workOrderInfoUI> CalculateCost(List<workOrderInfoUI> list)
        {
            if (list == null || list.Count == 0)
                return new List<workOrderInfoUI>();

            // Group theo WO
            var grouped = list.GroupBy(x => x.WO_FGID);

            var result = new List<workOrderInfoUI>();

            foreach (var group in grouped)
            {
                // Tính MAT
                decimal mat = group
                    .Where(x => x.Type == "WOIssue")
                    .Sum(x => decimal.TryParse(x.RMB, out var rmb) ? rmb : 0);

                // Tính DL
                decimal dl = group
                    .Where(x => x.Account2 == "500102 Production Cost : Direct Labor")
                    .Sum(x => Math.Abs(x.Amount_Foreign_Currency));

                decimal oh = group
                    .Where(x => x.Account2 == "500103 Production Cost : Overhead")
                    .Sum(x => Math.Abs(x.Amount_Foreign_Currency));

                decimal total = mat + dl + oh;

                foreach (var item in group)
                {
                    item.Mat = mat;
                    item.DL = dl;
                    item.OH = oh;
                    item.Total = total;
                    item.UnitPrice = item.Quantity != 0 ? total / item.Quantity : 0;

                    result.Add(item);
                }
            }

            return result;
        }


        #endregion
    }
}
