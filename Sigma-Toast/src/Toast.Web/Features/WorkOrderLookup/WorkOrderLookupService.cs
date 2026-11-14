using System.Text.Json.Nodes;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.WorkOrderLookup;

/// <summary>
/// Tra cứu lệnh sản xuất (mrp.production) trên Viindoo cho màn /toast/admin/work-order. CHỈ ĐỌC, mọi truy vấn có limit.
/// Danh sách: lọc theo dự án / loại (nhóm sản phẩm), trạng thái, ô tìm (tên WO, mã sản phẩm, đơn bán).
/// Chi tiết: WO + sản phẩm + phân loại + PO / Lot (đơn bán) + nhóm backorder + NVL.
/// </summary>
public sealed class WorkOrderLookupService(ViindooRpc viindoo, ViindooCatalog catalog)
{
    public const int PageSize = 50;
    static readonly string[] ListFields = ["name", "state", "product_id", "product_qty", "qty_produced", "date_deadline", "date_planned_start", "origin", "backorder_sequence"];

    // Cột được sắp xếp (khoá gửi từ màn hình -> trường Odoo). Không có / sai = mới nhất trước (id desc).
    static readonly Dictionary<string, string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "name", ["product"] = "product_id", ["qty"] = "product_qty", ["state"] = "state",
        ["deadline"] = "date_deadline", ["origin"] = "origin"
    };

    public async Task<object> SearchAsync(string? q, string? project, string? kind, string? state, int offset, string? sort = null, string? dir = null)
    {
        var domain = new List<object>();
        switch (state)
        {
            case "done": domain.Add(new object[] { "state", "=", "done" }); break;
            case "all": break;
            default: domain.Add(new object[] { "state", "not in", new[] { "done", "cancel" } }); break; // đang mở
        }
        var categIds = await catalog.CategoryIdsAsync(project, kind);
        if (categIds != null) domain.Add(new object[] { "product_id.categ_id", "in", categIds });

        string term = (q ?? "").Trim();
        if (term.Length > 0)
        {
            // Tìm theo tên WO / mã hoặc tên sản phẩm / đơn bán (origin)
            domain.Add("|"); domain.Add("|"); domain.Add("|");
            domain.Add(new object[] { "name", "ilike", term });
            domain.Add(new object[] { "product_id.default_code", "ilike", term });
            domain.Add(new object[] { "product_id.name", "ilike", term });
            domain.Add(new object[] { "origin", "ilike", term });
        }

        // Lấy dư 1 dòng để biết còn trang sau
        string order = sort != null && SortFields.TryGetValue(sort, out string? f)
            ? $"{f} {(string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc")}, id desc"
            : "id desc";
        var rows = await viindoo.SearchReadAsync("mrp.production", domain.ToArray(), ListFields, PageSize + 1, Math.Max(0, offset), order);
        bool more = rows.Count > PageSize;
        var page = rows.Take(PageSize).ToList();
        var products = await ProductsAsync(page.Select(r => ProductId(r!["product_id"])));
        var saleOrders = await ResolveSaleOrdersAsync(page.Select(r => ViindooRpc.Str(r!["origin"])));
        var items = page.Select(r =>
        {
            var p = products.GetValueOrDefault(ProductId(r!["product_id"]));
            string origin = ViindooRpc.Str(r["origin"]);
            return new
            {
                name = ViindooRpc.Str(r["name"]),
                state = ViindooRpc.Str(r["state"]),
                product = ViindooRpc.Str((r["product_id"] as JsonArray)?[1]),
                productQty = ViindooRpc.Dec(r["product_qty"]),
                qtyProduced = ViindooRpc.Dec(r["qty_produced"]),
                deadline = ViindooRpc.Str(r["date_deadline"]),
                plannedStart = ViindooRpc.Str(r["date_planned_start"]),
                origin,
                // origin là lệnh khác (lệnh con, VD bán thành phẩm cho lệnh thành phẩm) -> lệnh cha + đơn bán lần lên được
                parentWo = IsWo(origin) ? origin : "",
                saleOrder = IsWo(origin) ? saleOrders.GetValueOrDefault(origin, "") : origin,
                backorderSequence = (int)ViindooRpc.Dec(r["backorder_sequence"]),
                @class = p?.Class
            };
        }).ToList();
        return new { items, more, offset = Math.Max(0, offset), pageSize = PageSize };
    }

    public async Task<(object? detail, string? error)> DetailAsync(string? wo)
    {
        var (info, error) = await viindoo.GetWorkOrderAsync(wo, allowClosed: true);
        if (info == null)
        {
            // Nhập tên gốc đã tách backorder (VD 03975): không còn lệnh nào tên đó, tìm trong nhóm cùng tên
            var group = await GroupByNameAsync(wo);
            if (group != null) return (new { notFound = true, group }, null);
            return (null, error);
        }

        var rows = await viindoo.SearchReadAsync("mrp.production", [new object[] { "name", "=", info["name"] }],
            ["name", "state", "product_id", "product_qty", "qty_produced", "product_uom_id", "origin", "date_planned_start", "date_deadline",
             "date_start", "date_finished", "create_date", "create_uid", "user_id", "bom_id", "components_availability",
             "reservation_state", "procurement_group_id", "backorder_sequence", "move_raw_ids", "company_id"], limit: 1);
        var r = rows[0]!;
        int productId = ProductId(r["product_id"]);
        var product = (await ProductsAsync([productId])).GetValueOrDefault(productId);

        // PO / Lot từ đơn bán (giống /toastv2)
        var (label, warnings) = await viindoo.GetLabelDataAsync(info);
        if (info.GetValueOrDefault("requested_name") is { Length: > 0 } requested && !string.Equals(requested, info["name"], StringComparison.OrdinalIgnoreCase))
            warnings.Insert(0, $"Không còn lệnh tên {requested} (đã tách backorder), đang xem {info["name"]}: lệnh đang mở mới nhất của nhóm (hết lệnh mở thì lệnh mới nhất).");

        // Nhóm backorder
        object? groupInfo = null;
        if (r["procurement_group_id"] is JsonArray g && g[0] is JsonValue gv && gv.TryGetValue(out int gid))
            groupInfo = await GroupAsync(gid, ViindooRpc.Str(g[1]));

        // NVL (move_raw_ids), tối đa 100 dòng
        var moveIds = (r["move_raw_ids"] as JsonArray ?? []).Select(x => x!.GetValue<int>()).Take(100).ToList();
        var moves = await viindoo.ReadAsync("stock.move", moveIds, ["product_id", "product_uom_qty", "quantity_done", "state", "forecast_availability"]);
        var components = moves.Select(m => new
        {
            product = ViindooRpc.Str((m!["product_id"] as JsonArray)?[1]),
            required = ViindooRpc.Dec(m["product_uom_qty"]),
            done = ViindooRpc.Dec(m["quantity_done"]),
            available = ViindooRpc.Dec(m["forecast_availability"]),
            state = ViindooRpc.Str(m["state"])
        }).ToList();

        return (new
        {
            notFound = false,
            wo = new
            {
                name = ViindooRpc.Str(r["name"]),
                state = ViindooRpc.Str(r["state"]),
                productQty = ViindooRpc.Dec(r["product_qty"]),
                qtyProduced = ViindooRpc.Dec(r["qty_produced"]),
                uom = ViindooRpc.Str((r["product_uom_id"] as JsonArray)?[1]),
                origin = ViindooRpc.Str(r["origin"]),
                parentWo = IsWo(ViindooRpc.Str(r["origin"])) ? ViindooRpc.Str(r["origin"]) : "",
                plannedStart = ViindooRpc.Str(r["date_planned_start"]),
                deadline = ViindooRpc.Str(r["date_deadline"]),
                started = ViindooRpc.Str(r["date_start"]),
                finished = ViindooRpc.Str(r["date_finished"]),
                created = ViindooRpc.Str(r["create_date"]),
                createdBy = ViindooRpc.Str((r["create_uid"] as JsonArray)?[1]),
                responsible = ViindooRpc.Str((r["user_id"] as JsonArray)?[1]),
                bom = ViindooRpc.Str((r["bom_id"] as JsonArray)?[1]),
                componentsAvailability = ViindooRpc.Str(r["components_availability"]),
                reservationState = ViindooRpc.Str(r["reservation_state"]),
                backorderSequence = (int)ViindooRpc.Dec(r["backorder_sequence"]),
                company = ViindooRpc.Str((r["company_id"] as JsonArray)?[1])
            },
            product,
            label,
            warnings,
            group = groupInfo,
            components
        }, null);
    }

    // Tổng quan 1 nhóm cung ứng (lệnh gốc + backorder): số lệnh, tổng SL, SL đã xong, lệnh đang mở
    async Task<object> GroupAsync(int groupId, string groupName)
    {
        var rows = await viindoo.SearchReadAsync("mrp.production", [new object[] { "procurement_group_id", "=", groupId }],
            ["name", "state", "product_qty", "date_finished"], limit: 5000, order: "id");
        var open = rows.Where(x => ViindooRpc.Str(x!["state"]) is not ("done" or "cancel")).Select(x => ViindooRpc.Str(x!["name"])).ToList();
        var done = rows.Where(x => ViindooRpc.Str(x!["state"]) == "done").ToList();
        return new
        {
            name = groupName,
            count = rows.Count,
            totalQty = rows.Sum(x => ViindooRpc.Dec(x!["product_qty"])),
            doneQty = done.Sum(x => ViindooRpc.Dec(x!["product_qty"])),
            doneCount = done.Count,
            open,
            first = rows.Count > 0 ? ViindooRpc.Str(rows[0]!["name"]) : "",
            last = rows.Count > 0 ? ViindooRpc.Str(rows[^1]!["name"]) : "",
            lastFinished = done.Select(x => ViindooRpc.Str(x!["date_finished"])).Where(x => x.Length > 0).DefaultIfEmpty("").Max()
        };
    }

    // Tên gốc của nhóm (VD "03975" -> nhóm NM/MO/03975)
    async Task<object?> GroupByNameAsync(string? wo)
    {
        string name = (wo ?? "").Trim().ToUpperInvariant();
        if (name.Length == 0) return null;
        if (!name.StartsWith("NM/MO/")) name = "NM/MO/" + name;
        var groups = await viindoo.SearchReadAsync("procurement.group", [new object[] { "name", "=", name }], ["name"], limit: 1);
        if (groups.Count == 0) return null;
        return await GroupAsync(groups[0]!["id"]!.GetValue<int>(), ViindooRpc.Str(groups[0]!["name"]));
    }

    public sealed record ProductRow(int Id, string DefaultCode, string Name, string Category, ProductClass Class, string CustomerCode, int? QtyPerCode, string Tracking);

    async Task<Dictionary<int, ProductRow>> ProductsAsync(IEnumerable<int> ids)
    {
        var rows = await viindoo.ReadAsync("product.product", ids.Where(x => x > 0),
            ["default_code", "name", "categ_id", "x_custcode", "x_quantity_per_code", "tracking"]);
        return rows.ToDictionary(p => p!["id"]!.GetValue<int>(), p =>
        {
            string name = ViindooRpc.Str(p!["name"]);
            decimal qty = ViindooRpc.Dec(p["x_quantity_per_code"]);
            return new ProductRow(p["id"]!.GetValue<int>(), ViindooRpc.Str(p["default_code"]), name,
                ViindooRpc.Str((p["categ_id"] as JsonArray)?[1]), catalog.Classify(p["categ_id"], name),
                ViindooRpc.Str(p["x_custcode"]), qty > 0 ? (int)qty : null, ViindooRpc.Str(p["tracking"]));
        });
    }

    static bool IsWo(string origin) => origin.StartsWith("NM/MO/", StringComparison.OrdinalIgnoreCase);

    // Lệnh cha -> đơn bán: lần origin lên tối đa 5 cấp, mỗi cấp 1 lần đọc cho cả trang. Không ra đơn bán thì để trống.
    async Task<Dictionary<string, string>> ResolveSaleOrdersAsync(IEnumerable<string> origins)
    {
        var start = origins.Where(IsWo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // current[gốc] = tên đang lần (lệnh cha ở cấp hiện tại)
        var current = start.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
        for (int level = 0; level < 5 && current.Count > 0; level++)
        {
            var names = current.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // Lệnh cha có thể đã tách backorder (không còn lệnh đúng tên, chỉ còn -001, -002... cùng nhóm tên đó) -> tìm cả theo nhóm
            var rows = await viindoo.SearchReadAsync("mrp.production",
                ["|", new object[] { "name", "in", names }, new object[] { "procurement_group_id.name", "in", names }],
                ["name", "origin", "procurement_group_id"], limit: 2000, order: "id");
            var originOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in rows)
            {
                string o = ViindooRpc.Str(x!["origin"]);
                originOf.TryAdd(ViindooRpc.Str(x["name"]), o);
                originOf.TryAdd(ViindooRpc.Str((x["procurement_group_id"] as JsonArray)?[1]), o);
            }
            foreach (var key in current.Keys.ToList())
            {
                string next = originOf.GetValueOrDefault(current[key], "");
                if (IsWo(next)) current[key] = next;
                else
                {
                    result[key] = next; // đơn bán (hoặc trống)
                    current.Remove(key);
                }
            }
        }
        return result;
    }

    static int ProductId(JsonNode? node) => node is JsonArray a && a[0] is JsonValue v && v.TryGetValue(out int id) ? id : 0;
}
