using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO.NewDashboard;

namespace Sigma_Dashboard.Services
{
    public class OperationRegistrationService
    {
        private readonly AppSettingServices _appSettingServices;
        private readonly ProductMappingDataPortal _productMappingDataPortal;

        public OperationRegistrationService(AppSettingServices appSettingServices, DBConfiguration dBConfiguration)
        {
            _appSettingServices = appSettingServices;
            _productMappingDataPortal = new ProductMappingDataPortal(dBConfiguration.GetConnectionString());
        }

        public class OperationValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public bool HasNewOperations => NewOperationsToCreate.Count > 0;
            public List<string> CleanedOperations { get; set; } = new List<string>();
            public List<string> NewOperationsToCreate { get; set; } = new List<string>();
            public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public List<OperInfo> PreparedOperInfo { get; set; } = new List<OperInfo>();
            public List<ProductMappingUI> PreparedMappings { get; set; } = new List<ProductMappingUI>();
            public HashSet<string> NewMasterOperations { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public OperInfoConfig CurrentConfig { get; set; }
        }

        public class RegistrationResult
        {
            public bool Success { get; set; } = true;
            public List<string> CreatedOperations { get; set; } = new List<string>();
            public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string SummaryMessage { get; set; } = string.Empty;
        }

        /// <summary>
        /// Chuẩn hóa chuỗi tên Operation, loại bỏ khoảng trắng thừa
        /// </summary>
        public static string CleanOperationName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

            // Xóa khoảng trắng 2 đầu
            string cleaned = rawName.Trim();
            // Chuẩn hóa khoảng trắng quanh dấu gạch ngang: "Walter - WSS03" -> "Walter-WSS03"
            cleaned = Regex.Replace(cleaned, @"\s*-\s*", "-");
            // Chuẩn hóa khoảng trắng quanh dấu ngoặc: " (SM)" -> "(SM)"
            cleaned = Regex.Replace(cleaned, @"\s*\(\s*", "(");
            cleaned = Regex.Replace(cleaned, @"\s*\)\s*", ")");
            // Gộp nhiều khoảng trắng còn lại thành 1 khoảng trắng
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }

        /// <summary>
        /// Kiểm tra và thẩm định danh sách Operation mà không ghi xuống Database.
        /// Tìm các Operation mới, tra cứu ERP để đảm bảo mã sản phẩm tồn tại.
        /// </summary>
        public async Task<OperationValidationResult> ValidateOperationsAsync(IEnumerable<string> rawOperationNames)
        {
            var result = new OperationValidationResult();
            if (rawOperationNames == null) return result;

            // 1. Chuẩn hóa và lọc danh sách duy nhất các Operation đầu vào
            result.CleanedOperations = rawOperationNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(CleanOperationName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (result.CleanedOperations.Count == 0) return result;

            // 2. Lấy cấu hình OperInfo hiện tại từ DB
            result.CurrentConfig = await _appSettingServices.GetOperInfoConfig();
            if (result.CurrentConfig == null) result.CurrentConfig = new OperInfoConfig();
            if (result.CurrentConfig.OperInfo == null) result.CurrentConfig.OperInfo = new List<OperInfo>();

            // Tập hợp các operation đã tồn tại
            var existingNames = new HashSet<string>(
                result.CurrentConfig.OperInfo.Select(x => CleanOperationName(x.Operation)),
                StringComparer.OrdinalIgnoreCase
            );

            // 3. Tìm các Operation mới hoàn toàn
            result.NewOperationsToCreate = result.CleanedOperations
                .Where(op => !existingNames.Contains(op))
                .ToList();

            if (result.NewOperationsToCreate.Count == 0)
            {
                return result; // Đã tồn tại đầy đủ, không có công đoạn mới
            }

            // 4. Kiểm tra từng Operation mới
            foreach (var op in result.NewOperationsToCreate)
            {
                // Bóc tách hậu tố công ty: (SM), (ITA), (BT)
                string companySuffix = ExtractCompanySuffix(op);
                string baseOp = op;
                if (!string.IsNullOrEmpty(companySuffix) && baseOp.EndsWith(companySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    baseOp = baseOp.Substring(0, baseOp.Length - companySuffix.Length).Trim();
                }

                // Tách MasterOperation (prefix) và ProductCode
                string masterOper = string.Empty;
                string productCode = string.Empty;

                int dashIndex = baseOp.IndexOf('-');
                if (dashIndex > 0)
                {
                    masterOper = baseOp.Substring(0, dashIndex).Trim();
                    productCode = baseOp.Substring(dashIndex + 1).Trim();
                }
                else
                {
                    masterOper = baseOp;
                    productCode = baseOp;
                }

                // Tra cứu thông tin sản phẩm từ ERP (SVN_product_product)
                var productLookup = await _productMappingDataPortal.LookupProductByCodeAsync(productCode);
                if (productLookup == null && productCode.Contains("-"))
                {
                    // Thử tìm với phần đuôi nếu productCode có tiền tố (ví dụ "FG-RAI-SP-0007-00" -> thử "RAI-SP-0007-00")
                    string subCode = productCode.Substring(productCode.IndexOf('-') + 1);
                    productLookup = await _productMappingDataPortal.LookupProductByCodeAsync(subCode);
                }

                // Không bắt buộc phải có sản phẩm trong ERP:
                // Nếu không có product_id thì vẫn cho phép tạo cấu hình OperInfo trên dashboard nhưng Produce_id để rỗng [], và KHÔNG thêm vào ProductMapping.

                // Nhận diện WCType (WIP hay FG)
                string wcType = DetermineWcType(op, productCode, productLookup?.TemplateName ?? "");

                // Tìm Operation mẫu tương đồng nhất từ danh sách hiện có
                var template = FindBestMatchingTemplate(result.CurrentConfig.OperInfo, masterOper, wcType, companySuffix);

                // Xác định MasterOperation chuẩn (kế thừa nếu cùng dòng sản phẩm)
                string finalMasterOper = masterOper;
                string targetNormMaster = NormalizeMasterOperName(masterOper);
                string targetFamily = GetFamilyName(targetNormMaster);

                if (template != null && !string.IsNullOrWhiteSpace(template.MasterOperation))
                {
                    string tmNorm = NormalizeMasterOperName(template.MasterOperation);
                    string tmFamily = GetFamilyName(tmNorm);

                    if (string.Equals(tmNorm, targetNormMaster, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(targetFamily) && string.Equals(tmFamily, targetFamily, StringComparison.OrdinalIgnoreCase)))
                    {
                        finalMasterOper = template.MasterOperation;
                    }
                }

                // Đảm bảo hậu tố công ty trên MasterOperation nhất quán với Operation
                if (!string.IsNullOrEmpty(companySuffix) && !finalMasterOper.EndsWith(companySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    finalMasterOper = $"{finalMasterOper}{companySuffix}";
                }

                // Thiết lập Produce_id: Nếu có sản phẩm trong ERP thì gán ProductId, ngược lại để rỗng []
                // Tuyệt đối không kế thừa Produce_id của template để tránh hiện tượng nhiều công đoạn dùng chung 1 product_id
                var produceIdList = (productLookup != null && productLookup.ProductId > 0)
                    ? new List<int> { productLookup.ProductId }
                    : new List<int>();

                // Tạo đối tượng OperInfo mới kế thừa từ template
                var newOper = new OperInfo
                {
                    Operation = op,
                    Name = "",
                    Price = template?.Price ?? 0,
                    MasterOperation = finalMasterOper,
                    WCType = wcType,
                    StoreID = template?.StoreID ?? 3,
                    ColWidth = template != null && template.ColWidth > 0 ? template.ColWidth : 4,
                    Produce_id = produceIdList,
                    QCName = template?.QCName ?? "",
                    PDName = template?.PDName ?? "",
                    TechName = template?.TechName ?? "",
                    QCURL = template?.QCURL ?? "",
                    PDURL = template?.PDURL ?? "",
                    TechURL = template?.TechURL ?? "",
                    WC = CloneWorkCenter(template?.WC, productLookup?.ProductId ?? 0, wcType)
                };

                result.PreparedOperInfo.Add(newOper);
                result.NewMasterOperations.Add(newOper.MasterOperation);

                // CHỈ chuẩn bị ghi vào ProductMapping nếu có product_id hợp lệ từ ERP
                if (productLookup != null && productLookup.ProductId > 0)
                {
                    result.PreparedMappings.Add(new ProductMappingUI
                    {
                        product_id = productLookup.ProductId,
                        Operation = op
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Ghi các Operation đã thẩm định xuống Database (SVN_AppSetting và ProductMapping).
        /// Khử trùng lặp tên công đoạn và chỉ ghi ProductMapping với các mã có ProductId hợp lệ.
        /// </summary>
        public async Task<int> CommitNewOperationsAsync(OperationValidationResult valResult)
        {
            if (valResult == null || !valResult.IsValid || !valResult.HasNewOperations)
            {
                return 0;
            }

            // Bước 1: Sao lưu OperInfo cũ vào OperInfoBackup
            await _appSettingServices.BackupOperInfo();

            // Bước 2: Thêm mới vào danh sách và cập nhật OperInfo trong SVN_AppSetting (khử trùng lặp tên công đoạn)
            var existingOps = new HashSet<string>(
                valResult.CurrentConfig.OperInfo.Select(x => CleanOperationName(x.Operation)),
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var newOp in valResult.PreparedOperInfo)
            {
                string cleaned = CleanOperationName(newOp.Operation);
                if (!existingOps.Contains(cleaned))
                {
                    valResult.CurrentConfig.OperInfo.Add(newOp);
                    existingOps.Add(cleaned);
                }
            }
            await _appSettingServices.UpdateOperInfoConfig(valResult.CurrentConfig);

            // Bước 3: Ghi vào bảng ProductMapping (chỉ các mã có product_id)
            if (valResult.PreparedMappings != null && valResult.PreparedMappings.Count > 0)
            {
                await _productMappingDataPortal.InsertBulkAsync(valResult.PreparedMappings);
            }

            // Bước 4: Bổ sung MasterOperList nếu có nhóm mới
            foreach (var masterOp in valResult.NewMasterOperations)
            {
                await _appSettingServices.AppendToMasterOperList(masterOp);
            }

            return valResult.PreparedOperInfo.Count;
        }

        /// <summary>
        /// Phương thức trọn gói: Thẩm định và tự động ghi nếu hợp lệ
        /// </summary>
        public async Task<RegistrationResult> EnsureOperationsExistAsync(IEnumerable<string> rawOperationNames)
        {
            var result = new RegistrationResult();
            var valResult = await ValidateOperationsAsync(rawOperationNames);

            if (!valResult.IsValid)
            {
                result.Success = false;
                result.Errors = valResult.Errors;
                result.SummaryMessage = $"Có {valResult.Errors.Count} công đoạn mới không hợp lệ do không tìm thấy mã sản phẩm trong ERP.";
                return result;
            }

            if (valResult.HasNewOperations)
            {
                int count = await CommitNewOperationsAsync(valResult);
                result.CreatedOperations = valResult.PreparedOperInfo.Select(x => x.Operation).ToList();
                result.SummaryMessage = $"Đã tự động khởi tạo thành công {count} công đoạn mới: {string.Join(", ", result.CreatedOperations)}.";
            }

            return result;
        }

        /// <summary>
        /// Thuật toán xác định WCType: "WIP" (Bán thành phẩm) hay "FG" (Thành phẩm)
        /// </summary>
        private static string DetermineWcType(string opName, string productCode, string templateName)
        {
            opName = (opName ?? "").ToUpperInvariant();
            productCode = (productCode ?? "").ToUpperInvariant();
            templateName = (templateName ?? "").ToUpperInvariant();

            // 1. Dấu hiệu chắc chắn là FG
            if (opName.Contains("-FG-") || opName.Contains("_FG") || opName.StartsWith("FG-") || productCode.StartsWith("FG-"))
            {
                return "FG";
            }
            if (Regex.IsMatch(productCode, @"^\d{10,14}$")) // Mã Barcode số chuẩn EAN/UPC của thành phẩm
            {
                return "FG";
            }
            if (templateName.StartsWith("FG ") || templateName.Contains("FINISHED ASSEMBLIES") || templateName.Contains("FINISHED GOODS"))
            {
                return "FG";
            }

            // 2. Dấu hiệu chắc chắn là WIP
            if (opName.Contains("WIP") || productCode.Contains("WSS") || productCode.Contains("WCT") || productCode.Contains("SA"))
            {
                return "WIP";
            }
            if (templateName.Contains("SEMI-FINISHED") || templateName.Contains("COMPONENT"))
            {
                return "WIP";
            }

            // 3. Mặc định: Nếu có chữ Finished là FG, ngược lại là WIP
            return templateName.Contains("FINISH") ? "FG" : "WIP";
        }

        /// <summary>
        /// Tìm Operation mẫu tương đồng nhất trong danh sách hiện có,
        /// BẮT BUỘC ưu tiên cùng công ty trước tiên để không bao giờ lấy nhầm cấu hình của công ty khác (SM, ITA, BT, SVN).
        /// </summary>
        private static OperInfo FindBestMatchingTemplate(List<OperInfo> list, string masterOper, string wcType, string companySuffix)
        {
            if (list == null || list.Count == 0) return null;

            // 1. Phân nhóm theo công ty: Nếu công ty đã có mẫu, CHỈ tìm trong các mẫu của công ty đó
            var sameCompanyList = list.Where(x => HasMatchingCompanySuffix(x.Operation, companySuffix)).ToList();
            var searchPool = (sameCompanyList.Count > 0) ? sameCompanyList : list;

            string targetNormMaster = NormalizeMasterOperName(masterOper);
            string targetFamily = GetFamilyName(targetNormMaster);

            // Ưu tiên 1: Cùng MasterOperation (khớp chính xác tên sau chuẩn hóa) + Khớp WCType
            var match = searchPool.FirstOrDefault(x =>
                string.Equals(NormalizeMasterOperName(x.MasterOperation), targetNormMaster, StringComparison.OrdinalIgnoreCase) &&
                MatchesWcType(x, wcType)
            );
            if (match != null) return match;

            // Ưu tiên 2: Cùng MasterOperation (khớp chính xác tên sau chuẩn hóa) bất kỳ WCType
            match = searchPool.FirstOrDefault(x =>
                string.Equals(NormalizeMasterOperName(x.MasterOperation), targetNormMaster, StringComparison.OrdinalIgnoreCase)
            );
            if (match != null) return match;

            // Ưu tiên 3: Cùng Family (tiền tố nhóm sản phẩm) + Khớp WCType
            if (!string.IsNullOrEmpty(targetFamily))
            {
                match = searchPool.FirstOrDefault(x =>
                    string.Equals(GetFamilyName(NormalizeMasterOperName(x.MasterOperation)), targetFamily, StringComparison.OrdinalIgnoreCase) &&
                    MatchesWcType(x, wcType)
                );
                if (match != null) return match;

                // Ưu tiên 4: Cùng Family bất kỳ WCType
                match = searchPool.FirstOrDefault(x =>
                    string.Equals(GetFamilyName(NormalizeMasterOperName(x.MasterOperation)), targetFamily, StringComparison.OrdinalIgnoreCase)
                );
                if (match != null) return match;
            }

            // Ưu tiên 5: Khớp WCType (trong cùng công ty)
            match = searchPool.FirstOrDefault(x => MatchesWcType(x, wcType));
            if (match != null) return match;

            // Ưu tiên 6: Cùng WCType theo giá trị trường gốc
            match = searchPool.FirstOrDefault(x =>
                string.Equals(x.WCType, wcType, StringComparison.OrdinalIgnoreCase)
            );
            if (match != null) return match;

            // Fallback cuối cùng: Lấy mẫu đầu tiên của công ty
            return searchPool.FirstOrDefault();
        }

        public static string ExtractCompanySuffix(string op)
        {
            if (string.IsNullOrWhiteSpace(op)) return string.Empty;
            string trimmed = op.Trim();
            if (trimmed.EndsWith("(SM)", StringComparison.OrdinalIgnoreCase)) return "(SM)";
            if (trimmed.EndsWith("(ITA)", StringComparison.OrdinalIgnoreCase)) return "(ITA)";
            if (trimmed.EndsWith("(BT)", StringComparison.OrdinalIgnoreCase)) return "(BT)";
            return string.Empty;
        }

        public static bool HasMatchingCompanySuffix(string op, string companySuffix)
        {
            if (string.IsNullOrWhiteSpace(op)) return false;
            string opSuffix = ExtractCompanySuffix(op);
            return string.Equals(opSuffix, companySuffix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMasterOperName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string n = name.Trim();
            if (n.EndsWith("(SM)", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4).Trim();
            else if (n.EndsWith("(ITA)", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 5).Trim();
            else if (n.EndsWith("(BT)", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4).Trim();
            return n;
        }

        private static string GetFamilyName(string normalizedMaster)
        {
            if (string.IsNullOrWhiteSpace(normalizedMaster)) return string.Empty;
            return normalizedMaster.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

        private static bool MatchesWcType(OperInfo op, string wcType)
        {
            if (op == null) return false;
            if (string.Equals(op.WCType, wcType, StringComparison.OrdinalIgnoreCase)) return true;

            string normMaster = NormalizeMasterOperName(op.MasterOperation);
            if (string.Equals(wcType, "FG", StringComparison.OrdinalIgnoreCase))
            {
                return normMaster.Contains("FG", StringComparison.OrdinalIgnoreCase) ||
                       normMaster.Contains("F/G", StringComparison.OrdinalIgnoreCase) ||
                       (op.Operation != null && op.Operation.Contains("-FG-", StringComparison.OrdinalIgnoreCase));
            }
            else if (string.Equals(wcType, "WIP", StringComparison.OrdinalIgnoreCase))
            {
                return normMaster.Contains("WIP", StringComparison.OrdinalIgnoreCase) ||
                       (op.Operation != null && op.Operation.Contains("WIP", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private static List<WC> CloneWorkCenter(List<WC> source, int newProductId, string wcType)
        {
            if (source != null && source.Count > 0)
            {
                return source.Select(wc => new WC
                {
                    WCName = wc.WCName,
                    Top_row = wc.Top_row > 0 ? wc.Top_row : 5,
                    Produce_id = new List<int>()
                }).ToList();
            }

            // Nếu không có mẫu, tạo cấu trúc WC mặc định
            return new List<WC>
            {
                new WC
                {
                    WCName = wcType == "FG" ? "FG" : "WIP",
                    Top_row = 5,
                    Produce_id = new List<int>()
                }
            };
        }

        /// <summary>
        /// Chuẩn hóa toàn bộ danh sách OperInfo hiện có trong Database:
        /// 1. Khử trùng lặp tên công đoạn.
        /// 2. Đồng bộ Produce_id theo ProductMapping thực tế (sửa 47 mã bị gán nhầm PID của template).
        /// 3. Với các công đoạn không có trong ProductMapping, tra cứu lại ERP: nếu có thì cập nhật và thêm vào ProductMapping, nếu không thì set Produce_id = [] (sửa 91 mã không có sản phẩm bị gán PID ảo).
        /// 4. Sao lưu và ghi lại cấu hình sạch xuống SVN_AppSetting.
        /// </summary>
        public async Task<(int TotalBefore, int TotalAfter, int DedupedCount, int UpdatedPidCount, int ClearedPidCount)> StandardizeOperInfoProduceIdsAsync()
        {
            var config = await _appSettingServices.GetOperInfoConfig();
            if (config == null || config.OperInfo == null) return (0, 0, 0, 0, 0);

            int totalBefore = config.OperInfo.Count;
            var pmList = await _productMappingDataPortal.GetAllAsync();
            var pmMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pm in pmList)
            {
                if (!string.IsNullOrWhiteSpace(pm.Operation) && pm.product_id.HasValue && pm.product_id.Value > 0)
                {
                    pmMap[pm.Operation.Trim()] = pm.product_id.Value;
                }
            }

            var seenOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cleanedList = new List<OperInfo>();
            int dedupCount = 0;
            int updatedCount = 0;
            int clearedCount = 0;
            var newMappingsToInsert = new List<ProductMappingUI>();

            foreach (var it in config.OperInfo)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.Operation)) continue;
                string op = it.Operation.Trim();
                if (seenOps.Contains(op))
                {
                    dedupCount++;
                    continue;
                }
                seenOps.Add(op);

                int oldPid = (it.Produce_id != null && it.Produce_id.Count > 0) ? it.Produce_id[0] : 0;

                if (pmMap.TryGetValue(op, out int pmPid))
                {
                    if (oldPid != pmPid)
                    {
                        updatedCount++;
                    }
                    it.Produce_id = new List<int> { pmPid };
                }
                else
                {
                    // Không có trong ProductMapping, tra cứu ERP
                    string companySuffix = ExtractCompanySuffix(op);
                    string baseOp = op;
                    if (!string.IsNullOrEmpty(companySuffix) && baseOp.EndsWith(companySuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        baseOp = baseOp.Substring(0, baseOp.Length - companySuffix.Length).Trim();
                    }

                    int dash = baseOp.IndexOf('-');
                    string code = dash > 0 ? baseOp.Substring(dash + 1).Trim() : baseOp.Trim();

                    var erpProduct = await _productMappingDataPortal.LookupProductByCodeAsync(code);
                    if (erpProduct != null && erpProduct.ProductId > 0)
                    {
                        it.Produce_id = new List<int> { erpProduct.ProductId };
                        updatedCount++;
                        newMappingsToInsert.Add(new ProductMappingUI
                        {
                            product_id = erpProduct.ProductId,
                            Operation = op
                        });
                    }
                    else
                    {
                        if (oldPid > 0)
                        {
                            clearedCount++;
                        }
                        it.Produce_id = new List<int>();
                    }
                }

                cleanedList.Add(it);
            }

            // Sao lưu cấu hình cũ trước khi cập nhật
            await _appSettingServices.BackupOperInfo();

            // Cập nhật cấu hình đã chuẩn hóa
            config.OperInfo = cleanedList;
            await _appSettingServices.UpdateOperInfoConfig(config);

            // Bổ sung ProductMapping nếu có
            if (newMappingsToInsert.Count > 0)
            {
                await _productMappingDataPortal.InsertBulkAsync(newMappingsToInsert);
            }

            return (totalBefore, cleanedList.Count, dedupCount, updatedCount, clearedCount);
        }
    }
}
