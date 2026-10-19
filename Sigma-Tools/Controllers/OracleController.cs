using Microsoft.AspNetCore.Mvc;
using Sigma_Tools.Models;

namespace Sigma_Tools.Controllers
{
    public class OracleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult POInbound()
        {
            string accessMode = "Tools";
            string controllerName = "Oracle";
            string indexPage = "POInbound";
            string titleName = "Create Netsuite SO";
            string selectedPage = "NetsuiteSO";

            ViewBag.AccessMode = accessMode;
            ViewBag.ControllerName = controllerName;
            ViewBag.IndexPage = indexPage;
            ViewBag.TitleName = titleName;
            ViewBag.SelectedPage = selectedPage;

            var model = new InboundPOReviewViewModel
            {
                PONumber = "PO10285",
                Date = "4/17/2026",
                Customer = "WHOOP Inc.",
                Requisitioner = "Nate Froio",
                CalculatedUnits = 19500,
                CalculatedAmount = 298790.00m,
                ExpectedUnits = 20700,
                ExpectedAmount = 315530.00m,
                LineItems = new List<POLineItem>
                {
                    new POLineItem { Line = 1, ItemCode = "810114362747", Description = "SuperKnit Band Dune...", Qty = 9000, UnitPrice = 17.21m },
                    new POLineItem { Line = 2, ItemCode = "810114362730", Description = "SuperKnit Luxe Band...", Qty = 2500, UnitPrice = 10.06m },
                    new POLineItem { Line = 3, ItemCode = "810114362723", Description = "SuperKnit Luxe Band...", Qty = 1000, UnitPrice = 13.41m },
                    new POLineItem { Line = 4, ItemCode = "810114362716", Description = "SuperKnit Luxe Band...", Qty = 1500, UnitPrice = 9.55m },
                    new POLineItem { Line = 5, ItemCode = "810114362563", Description = "SuperKnit Luxe Band...", Qty = 4500, UnitPrice = 17.83m },
                    new POLineItem { Line = 6, ItemCode = "810114362556", Description = "SuperKnit Luxe Band...", Qty = 1000, UnitPrice = 10.78m }
                }
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult UploadPOPdf(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return Json(new { success = false, message = "Không tìm thấy file tải lên." });
            }

            try
            {
                // Lấy tên file gốc
                string fileName = Path.GetFileName(pdfFile.FileName);

                // (Tùy chọn) Lưu file vào thư mục tạm trên server nếu cần xử lý OCR
                // string savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                // using (var stream = new FileStream(savePath, FileMode.Create))
                // {
                //     pdfFile.CopyTo(stream);
                // }

                // Thực hiện các logic xử lý file PDF ở đây (Ví dụ: Gọi AI/OCR đọc dữ liệu PO...)

                return Json(new
                {
                    success = true,
                    message = "Upload file thành công!",
                    fileName = fileName,
                    size = pdfFile.Length
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
