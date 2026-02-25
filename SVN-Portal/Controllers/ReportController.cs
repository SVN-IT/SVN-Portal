using DocumentFormat.OpenXml.Vml;
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
                var list = CalculateCost(workOrderModels);

                //Tính toán lại các trường cho FG theo WIP
                //Lọc dữ liệu của Vietnam thôi
                if (list != null && list.Count > 0) 
                {
                    list = list.Where(x => x.Subsidiary == "Sigma Worldwide : Sigma Vietnam").OrderByDescending(x => x.WO_FGID).ToList(); //&& (x.WO_FGID == 15850 || x.WO_FGID == 15849)
                    //&& (x.WO_FGID == 15752 || x.WO_FGID == 15751 || x.WO_FGID == 15752)
                    // Group toàn bộ dữ liệu theo WO
                    var woGroups = list
                        .GroupBy(x => x.WO_FGID)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // Map FGItem -> WO
                    var fgItemToWO = list
                        .GroupBy(x => x.FGitem)
                        .ToDictionary(g => g.Key, g => g.First().WO_FGID);

                    foreach (var parentWO in woGroups)
                    {
                        var parentWoId = parentWO.Key;
                        var parentRows = parentWO.Value;

                        // Mat hiện tại của WO cha
                        var parentMat = parentRows
                                    .FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead")
                                    ?.Mat ?? 0;

                        var parentDL = parentRows
                                    .FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead")
                                    ?.DL ?? 0;

                        decimal childMat = 0;
                        decimal childDL = 0;
                        decimal childOH = 0;

                        List<workOrderInfoUI> itemsForFG = new List<workOrderInfoUI>();

                        // Tìm WO con dựa vào Material_Name
                        foreach (var row in parentRows)
                        {
                            if (row.FGitem != row.Material_Name)
                            {
                                if (fgItemToWO.ContainsKey(row.Material_Name))
                                {
                                    var childWoId = fgItemToWO[row.Material_Name];

                                    if (woGroups.ContainsKey(childWoId))
                                    {
                                        var childWoinfo = woGroups[childWoId].FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead" && x.Quantity == row.Quantity);
                                        if (childWoinfo != null)
                                        {
                                            childWoinfo.CurWOID = childWoId;
                                            childWoinfo.ParentWOID = parentWoId;
                                            childWoinfo.Status = "Term";
                                            itemsForFG.Add(childWoinfo);
                                            //childMat += childWoinfo.Mat;
                                            //childDL += childWoinfo.DL;
                                            //childOH += childWoinfo.OH;
                                        }
                                        //childMat += woGroups[childWoId].FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead")?.Mat ?? 0;
                                        //childDL += woGroups[childWoId].FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead")?.DL ?? 0;
                                        //childOH += woGroups[childWoId].FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead")?.OH ?? 0;
                                    }
                                }
                            }
                        }

                        //var newMat = parentMat - childDL - childOH;
                        //var newDL = parentDL + childDL;

                        // Cập nhật toàn bộ dòng của WO cha (hoặc chỉ dòng 500103 nếu bạn muốn)
                        //foreach (var item in list)
                        //{
                        //    if(item.WO_FGID == parentWoId && item.Account2 == "500103 Production Cost : Overhead" && item.item_type == "1")
                        //    {
                        //        item.Mat = newMat;
                        //        item.DL = newDL;
                        //        item.Total = item.Mat + item.DL + item.OH;
                        //        item.UnitPrice = item.Quantity != 0 ? item.Total / item.Quantity : 0;
                        //    }
                        //}
                    }

                    list = list.OrderBy(x => x.WO_FGID).ToList();
                    list = CalculateMat(list);
                }
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

        public List<workOrderInfoUI> CalculateMat(List<workOrderInfoUI> list)
        {
            if (list == null || list.Count == 0)
                return list;

            // 1️⃣ Group theo WO_FGID (vì đây là ID thật của WO)
            var woGroupMap = list
                .GroupBy(x => x.WO_FGID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2️⃣ Build cây Parent → Child theo WO_FGID
            var childrenMap = list
                .Where(x => x.ParentWOID != 0)
                .GroupBy(x => x.ParentWOID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.WO_FGID).Distinct().ToList()
                );

            // 3️⃣ Cache tránh tính lặp
            var costCache = new Dictionary<int, decimal>();
            var dlCache = new Dictionary<int, decimal>();

            decimal GetTotalSubCost(int parentWOID)
            {
                if (costCache.ContainsKey(parentWOID))
                    return costCache[parentWOID];

                decimal total = 0;

                if (childrenMap.ContainsKey(parentWOID))
                {
                    foreach (var childWOID in childrenMap[parentWOID])
                    {
                        if (woGroupMap.ContainsKey(childWOID))
                        {
                            var childRows = woGroupMap[childWOID];

                            // 🔥 CHỈ lấy dòng Overhead của WO con
                            var overheadRow = childRows
                                .FirstOrDefault(x =>
                                    x.Account2 == "500103 Production Cost : Overhead"
                                    && x.ParentWOID > 0);

                            if (overheadRow != null)
                            {
                                var cost = overheadRow.DL + overheadRow.OH;
                                total += cost;
                            }

                            // cộng tiếp tầng dưới
                            total += GetTotalSubCost(childWOID);
                        }
                    }
                }

                costCache[parentWOID] = total;
                return total;
            }

            decimal GetTotalSubDL(int parentWOID)
            {
                if (dlCache.ContainsKey(parentWOID))
                    return dlCache[parentWOID];

                decimal total = 0;

                if (childrenMap.ContainsKey(parentWOID))
                {
                    foreach (var childWOID in childrenMap[parentWOID])
                    {
                        if (woGroupMap.ContainsKey(childWOID))
                        {
                            var childRows = woGroupMap[childWOID];

                            // 🔥 CHỈ lấy dòng Overhead của WO con
                            var overheadRow = childRows
                                .FirstOrDefault(x =>
                                    x.Account2 == "500103 Production Cost : Overhead"
                                    && x.ParentWOID > 0);

                            if (overheadRow != null)
                            {
                                total += overheadRow.DL;
                            }

                            // cộng tiếp tầng dưới
                            total += GetTotalSubDL(childWOID);
                        }
                    }
                }

                dlCache[parentWOID] = total;
                return total;
            }

            // 4️⃣ Tính lại Mat cho FG (CurWOID = 0)
            foreach (var row in list)
            {
                if (row.CurWOID == 0 &&  // là FG
                    row.item_type == "1" &&
                    row.Account2 == "500103 Production Cost : Overhead")
                {
                    var totalSubCost = GetTotalSubCost(row.WO_FGID);
                    var totalSubDL = GetTotalSubDL(row.WO_FGID);

                    row.Mat = row.Mat - totalSubCost;
                    row.DL = row.DL + totalSubDL;
                }
            }

            return list;
        }
        #endregion
    }
}
