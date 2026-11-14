using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.ProductLookup;

/// <summary>
/// Tra cứu sản phẩm (product.product) trên Viindoo cho màn /toast/admin/viindoo-product. CHỈ ĐỌC, mọi truy vấn có limit.
/// Chi tiết: thông tin + phân loại, BoM (NVL), WO đang mở, đơn bán gần đây, Toast đã có quy cách / mẫu tem chưa.
/// </summary>
public sealed class ProductLookupService(ViindooRpc viindoo, ViindooCatalog catalog, ToastDbContext db)
{
    public const int PageSize = 50;
    static readonly string[] Fields = ["default_code", "name", "categ_id", "x_custcode", "x_quantity_per_code", "tracking", "uom_id", "product_tmpl_id", "active"];

    // Cột được sắp xếp (khoá gửi từ màn hình -> trường Odoo). Không có / sai = theo mã (default_code).
    static readonly Dictionary<string, string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "default_code", ["name"] = "name", ["category"] = "categ_id", ["customerCode"] = "x_custcode",
        ["qty"] = "x_quantity_per_code", ["tracking"] = "tracking", ["id"] = "id"
    };

    public async Task<object> SearchAsync(string? q, string? project, string? kind, int offset, string? sort = null, string? dir = null)
    {
        var domain = new List<object>();
        var categIds = await catalog.CategoryIdsAsync(project, kind);
        if (categIds != null) domain.Add(new object[] { "categ_id", "in", categIds });
        string term = (q ?? "").Trim();
        if (term.Length > 0)
        {
            // Mã, tên, mã khách
            domain.Add("|"); domain.Add("|");
            domain.Add(new object[] { "default_code", "ilike", term });
            domain.Add(new object[] { "name", "ilike", term });
            domain.Add(new object[] { "x_custcode", "ilike", term });
        }
        string order = sort != null && SortFields.TryGetValue(sort, out string? f)
            ? $"{f} {(string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc")}, id"
            : "default_code, id";
        var rows = await viindoo.SearchReadAsync("product.product", domain.ToArray(), Fields, PageSize + 1, Math.Max(0, offset), order);
        bool more = rows.Count > PageSize;
        return new { items = rows.Take(PageSize).Select(Row).ToList(), more, offset = Math.Max(0, offset), pageSize = PageSize };
    }

    public async Task<(object? detail, string? error)> DetailAsync(int id)
    {
        var rows = await viindoo.ReadAsync("product.product", [id], Fields);
        if (rows.Count == 0) return (null, $"Không tìm thấy sản phẩm ID {id} trên Viindoo.");
        var p = rows[0]!;
        int tmplId = (p["product_tmpl_id"] as JsonArray)?[0] is JsonValue tv && tv.TryGetValue(out int t) ? t : 0;

        // BoM: theo biến thể hoặc theo template, mới nhất trước
        var boms = await viindoo.SearchReadAsync("mrp.bom",
            ["|", new object[] { "product_id", "=", id }, new object[] { "product_tmpl_id", "=", tmplId }],
            ["display_name", "product_qty", "bom_line_ids", "active"], limit: 5, order: "id desc");
        var bomLines = boms.Count == 0 ? [] : await viindoo.ReadAsync("mrp.bom.line",
            (boms[0]!["bom_line_ids"] as JsonArray ?? []).Select(x => x!.GetValue<int>()).Take(200), ["product_id", "product_qty", "product_uom_id"]);

        // WO đang mở + đơn bán gần đây
        var wos = await viindoo.SearchReadAsync("mrp.production",
            [new object[] { "product_id", "=", id }, new object[] { "state", "not in", new[] { "done", "cancel" } }],
            ["name", "state", "product_qty", "qty_produced", "origin", "date_deadline"], limit: 30, order: "id desc");
        var saleLines = await viindoo.SearchReadAsync("sale.order.line", [new object[] { "product_id", "=", id }],
            ["order_id", "product_uom_qty", "create_date"], limit: 15, order: "id desc");

        // Toast: quy cách (Toast_Product theo ProductId) + mẫu tem (theo default code)
        string code = ViindooRpc.Str(p["default_code"]);
        var toastProduct = await db.Products.AsNoTracking().Where(x => x.ProductId == id)
            .Select(x => new { x.Id, x.DefaultCode, x.ScanSource, x.Barcode, x.PcsPerBox }).FirstOrDefaultAsync();
        var templates = code.Length == 0 ? [] : await db.LabelTemplates.AsNoTracking().Where(x => x.DefaultCode == code)
            .Select(x => new { x.Id, x.Type, x.Operation, x.Size }).ToListAsync();

        return (new
        {
            product = Row(p),
            bom = boms.Count == 0 ? null : new
            {
                name = ViindooRpc.Str(boms[0]!["display_name"]),
                qty = ViindooRpc.Dec(boms[0]!["product_qty"]),
                count = boms.Count,
                lines = bomLines.Select(l => new
                {
                    product = ViindooRpc.Str((l!["product_id"] as JsonArray)?[1]),
                    qty = ViindooRpc.Dec(l["product_qty"]),
                    uom = ViindooRpc.Str((l["product_uom_id"] as JsonArray)?[1])
                })
            },
            openWos = wos.Select(w => new
            {
                name = ViindooRpc.Str(w!["name"]),
                state = ViindooRpc.Str(w["state"]),
                productQty = ViindooRpc.Dec(w["product_qty"]),
                qtyProduced = ViindooRpc.Dec(w["qty_produced"]),
                origin = ViindooRpc.Str(w["origin"]),
                deadline = ViindooRpc.Str(w["date_deadline"])
            }),
            saleOrders = saleLines.Select(l => new
            {
                order = ViindooRpc.Str((l!["order_id"] as JsonArray)?[1]),
                qty = ViindooRpc.Dec(l["product_uom_qty"]),
                created = ViindooRpc.Str(l["create_date"])
            }),
            toast = new { product = toastProduct, templates }
        }, null);
    }

    object Row(JsonNode? p)
    {
        string name = ViindooRpc.Str(p!["name"]);
        decimal qty = ViindooRpc.Dec(p["x_quantity_per_code"]);
        return new
        {
            id = p["id"]!.GetValue<int>(),
            defaultCode = ViindooRpc.Str(p["default_code"]),
            name,
            category = ViindooRpc.Str((p["categ_id"] as JsonArray)?[1]),
            @class = catalog.Classify(p["categ_id"], name),
            customerCode = ViindooRpc.Str(p["x_custcode"]),
            qtyPerCode = qty > 0 ? (int?)qty : null,
            tracking = ViindooRpc.Str(p["tracking"]),
            uom = ViindooRpc.Str((p["uom_id"] as JsonArray)?[1])
        };
    }
}
