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
                string companySuffix = string.Empty;
                string baseOp = op;

                if (op.EndsWith("(SM)", StringComparison.OrdinalIgnoreCase))
                {
                    companySuffix = "(SM)";
                    baseOp = op.Substring(0, op.Length - 4).Trim();
                }
                else if (op.EndsWith("(ITA)", StringComparison.OrdinalIgnoreCase))
                {
                    companySuffix = "(ITA)";
                    baseOp = op.Substring(0, op.Length - 5).Trim();
                }
                else if (op.EndsWith("(BT)", StringComparison.OrdinalIgnoreCase))
                {
                    companySuffix = "(BT)";
                    baseOp = op.Substring(0, op.Length - 4).Trim();
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

                if (productLookup == null)
                {
                    result.Errors[op] = $"Mã sản phẩm '{productCode}' của công đoạn '{op}' không tìm thấy trong ERP (SVN_product_product). Vui lòng tạo sản phẩm trên Viindoo trước.";
                    continue; // Ghi nhận lỗi cho dòng này
                }

                // Nhận diện WCType (WIP hay FG)
                string wcType = DetermineWcType(op, productCode, productLookup.TemplateName);

                // Tìm Operation mẫu tương đồng nhất từ danh sách hiện có
                var template = FindBestMatchingTemplate(result.CurrentConfig.OperInfo, masterOper, wcType, companySuffix);

                // Xác định MasterOperation chuẩn (kế thừa nếu cùng dòng sản phẩm)
                string finalMasterOper = masterOper;
                if (template != null && !string.IsNullOrWhiteSpace(template.MasterOperation))
                {
                    string templateFamily = template.MasterOperation.Split(' ', '-')[0];
                    if (string.Equals(templateFamily, masterOper, StringComparison.OrdinalIgnoreCase))
                    {
                        finalMasterOper = template.MasterOperation;
                    }
                }

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
                    Produce_id = new List<int> { productLookup.ProductId },
                    QCName = template?.QCName ?? "",
                    PDName = template?.PDName ?? "",
                    TechName = template?.TechName ?? "",
                    QCURL = template?.QCURL ?? "",
                    PDURL = template?.PDURL ?? "",
                    TechURL = template?.TechURL ?? "",
                    WC = CloneWorkCenter(template?.WC, productLookup.ProductId, wcType)
                };

                result.PreparedOperInfo.Add(newOper);
                result.NewMasterOperations.Add(newOper.MasterOperation);

                // Chuẩn bị bản ghi ProductMapping (chỉ gồm product_id và Operation)
                result.PreparedMappings.Add(new ProductMappingUI
                {
                    product_id = productLookup.ProductId,
                    Operation = op
                });
            }

            return result;
        }

        /// <summary>
        /// Ghi các Operation đã thẩm định xuống Database (SVN_AppSetting và ProductMapping).
        /// Chỉ gọi khi toàn bộ validation đã thành công.
        /// </summary>
        public async Task<int> CommitNewOperationsAsync(OperationValidationResult valResult)
        {
            if (valResult == null || !valResult.IsValid || !valResult.HasNewOperations)
            {
                return 0;
            }

            // Bước 1: Sao lưu OperInfo cũ vào OperInfoBackup
            await _appSettingServices.BackupOperInfo();

            // Bước 2: Thêm mới vào danh sách và cập nhật OperInfo trong SVN_AppSetting
            valResult.CurrentConfig.OperInfo.AddRange(valResult.PreparedOperInfo);
            await _appSettingServices.UpdateOperInfoConfig(valResult.CurrentConfig);

            // Bước 3: Ghi vào bảng ProductMapping
            await _productMappingDataPortal.InsertBulkAsync(valResult.PreparedMappings);

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
        /// Tìm Operation mẫu tương đồng nhất trong danh sách hiện có
        /// </summary>
        private static OperInfo FindBestMatchingTemplate(List<OperInfo> list, string masterOper, string wcType, string companySuffix)
        {
            if (list == null || list.Count == 0) return null;

            // Ưu tiên 1: Cùng MasterOperation + Cùng WCType + Cùng Company Suffix
            var match = list.FirstOrDefault(x =>
                string.Equals(x.MasterOperation, masterOper, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.WCType, wcType, StringComparison.OrdinalIgnoreCase) &&
                HasMatchingCompanySuffix(x.Operation, companySuffix)
            );
            if (match != null) return match;

            // Ưu tiên 2: Cùng MasterOperation + Cùng WCType
            match = list.FirstOrDefault(x =>
                string.Equals(x.MasterOperation, masterOper, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.WCType, wcType, StringComparison.OrdinalIgnoreCase)
            );
            if (match != null) return match;

            // Ưu tiên 3: Cùng MasterOperation bất kỳ
            match = list.FirstOrDefault(x =>
                string.Equals(x.MasterOperation, masterOper, StringComparison.OrdinalIgnoreCase)
            );
            if (match != null) return match;

            // Ưu tiên 4: Lấy mẫu bất kỳ có cùng WCType
            match = list.FirstOrDefault(x =>
                string.Equals(x.WCType, wcType, StringComparison.OrdinalIgnoreCase)
            );
            if (match != null) return match;

            // Fallback cuối cùng: phần tử đầu tiên
            return list.FirstOrDefault();
        }

        private static bool HasMatchingCompanySuffix(string op, string companySuffix)
        {
            if (string.IsNullOrEmpty(companySuffix))
            {
                return !op.EndsWith("(SM)", StringComparison.OrdinalIgnoreCase) &&
                       !op.EndsWith("(ITA)", StringComparison.OrdinalIgnoreCase) &&
                       !op.EndsWith("(BT)", StringComparison.OrdinalIgnoreCase);
            }
            return op.EndsWith(companySuffix, StringComparison.OrdinalIgnoreCase);
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
    }
}
