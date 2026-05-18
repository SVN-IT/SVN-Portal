using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Utils
{
    public class SavedSearchServices
    {
        string connectionString;
        public SavedSearchServices(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<workOrderInfoUI>> GetWOData(string condition)
        {
            List<workOrderInfoUI> pagedData = new List<workOrderInfoUI>();
            List<workOrderInfoUI> workOrderModels = new List<workOrderInfoUI>();

            var dataPortal = new SVN_ERP_Final_ReportDataPortal(connectionString);
            workOrderModels = await dataPortal.GetDataByCondition(condition);

            if (workOrderModels != null && workOrderModels.Count > 0)
            {
                var list = CalculateCost(workOrderModels);

                //Tính toán lại các trường cho FG theo WIP
                //Lọc dữ liệu của Vietnam thôi
                if (list != null && list.Count > 0)
                {
                    list = list.OrderByDescending(x => x.WO_FGID).ToList();
                    //list = list.Where(x => x.Subsidiary == "Sigma Worldwide : Sigma Vietnam").OrderByDescending(x => x.WO_FGID).ToList(); //&& (x.WO_FGID == 15850 || x.WO_FGID == 15849)
                    //&& (x.WO_FGID == 15752 || x.WO_FGID == 15751 || x.WO_FGID == 15752)
                    // Group toàn bộ dữ liệu theo WO
                    var woGroups = list
                        .GroupBy(x => x.WO_FGID)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // Map FGItem -> WO
                    var fgItemToWO = list
                        .GroupBy(x => x.FGitem ?? "")
                        .ToDictionary(g => g.Key, g => g.First().WO_FGID);

                    var fgList = list
                        .GroupBy(x => new { x.FGitem, x.WO_FGID })
                        .Select(g =>
                        {
                            var first = g.First();
                            return new FGInfo
                            {
                                FGitem = first.FGitem,
                                WO_FGID = first.WO_FGID,
                                Quantity = first.Quantity
                            };
                        })
                        .ToList();

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
                                var childItemForFG = fgList.FirstOrDefault(x => x.FGitem == row.Material_Name && x.Quantity == row.Quantity);
                                if (childItemForFG != null)
                                {
                                    var childWoId = childItemForFG.WO_FGID;
                                    if (woGroups.ContainsKey(childWoId))
                                    {
                                        var childWoinfo = woGroups[childWoId].FirstOrDefault(x => x.Account2 == "500103 Production Cost : Overhead" && x.Quantity == row.Quantity);
                                        if (childWoinfo != null)
                                        {
                                            childWoinfo.CurWOID = childWoId;
                                            childWoinfo.ParentWOID = parentWoId;
                                            childWoinfo.Status = "Term";
                                            itemsForFG.Add(childWoinfo);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    list = list.OrderBy(x => x.WO_FGID).ToList();
                    list = CalculateMat(list);

                    pagedData = list;
                }
            }
            return pagedData;
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
                    row.Total = row.Mat + row.DL + row.OH;
                    row.UnitPrice = row.Quantity != 0 ? row.Total / row.Quantity : 0;
                }
            }

            return list;
        }
    }
}
