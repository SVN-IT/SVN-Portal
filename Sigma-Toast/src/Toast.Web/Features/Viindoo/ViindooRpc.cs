using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Toast.Web.Features.Viindoo;

/// <summary>Kết nối Viindoo trực tiếp (mục Viindoo). Mật khẩu: user-secrets (máy dev) / appsettings.Production.json (server).</summary>
public class ViindooRpcOptions
{
    public const string Section = "Viindoo";
    public string Url { get; set; } = "";
    public string Db { get; set; } = "";
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
}

// Nhân viên (hr.employee) có mã SVN: Code = "SVN0902", Phone = work_phone (thường là số tổng đài công ty), trống = không có
public sealed record ViindooEmployee(string Code, string Name, string Phone);

// Sản phẩm (product.product) đọc theo default_code. QtyPerCode = x_quantity_per_code, null khi Viindoo để trống / 0.
public sealed record ViindooProduct(int ProductId, string DefaultCode, string Name, int? QtyPerCode);

// Tra cứu đơn bán (ViindooRpc.GetSaleOrderOverviewAsync)
public sealed record SaleOrderOverview(string Name, string State, string Customer, string DateOrder,
    string RawPo, string RawLot, string RawDeadline, List<SaleOrderLineInfo> Lines, List<WoGroupInfo> UnmatchedGroups,
    int WoCount, List<string> Warnings);

// Group: nhóm WO ghép được với dòng (null = chưa ghép được)
public sealed record SaleOrderLineInfo(int No, int ProductId, string Product, decimal Qty, string Po, string Lot, string Deadline, WoGroupInfo? Group);

// Nhóm WO (procurement_group_id): lệnh gốc + backorder. OpenWos = lệnh chưa xong / huỷ. LatestWo = lệnh tạo sau cùng.
public sealed record WoGroupInfo(int GroupId, string GroupName, int ProductId, string Product, int Count, decimal TotalQty,
    List<string> OpenWos, string LatestWo, string LatestState);

/// <summary>
/// Đọc Viindoo (Odoo 16) bằng JSON-RPC: /web/session/authenticate + /web/dataset/call_kw. Chỉ đọc: search_read / read / fields_get,
/// search_read luôn có limit. Ngoại lệ duy nhất: nhập KQSX (ViindooRpc.Production.cs, PushProductionAsync) được gọi đúng các hàm ghi
/// trong <see cref="WriteMethods"/>.
/// Đăng ký singleton; HttpClient tên <see cref="HttpClientName"/> giữ cookie session suốt đời app, hết phiên thì đăng nhập lại 1 lần.
/// </summary>
public partial class ViindooRpc(IHttpClientFactory httpFactory, IOptionsMonitor<ViindooRpcOptions> options)
{
    public const string HttpClientName = "viindoo-rpc";
    static readonly HashSet<string> ReadMethods = ["search_read", "read", "fields_get"];
    readonly SemaphoreSlim loginLock = new(1, 1);
    bool loggedIn;

    // Tìm sản phẩm theo đúng mã (default_code). Trả lỗi dễ đọc khi không có / trùng mã.
    public async Task<(ViindooProduct? product, string? error)> FindProductByCodeAsync(string? defaultCode)
    {
        defaultCode = (defaultCode ?? "").Trim();
        if (defaultCode.Length == 0) return (null, "Vui lòng nhập mã sản phẩm.");

        var rows = await SearchReadAsync("product.product",
            [new object[] { "default_code", "=", defaultCode }],
            ["id", "default_code", "name", "x_quantity_per_code"], limit: 2);
        if (rows.Count == 0) return (null, $"Không tìm thấy sản phẩm có mã {defaultCode} trên Viindoo.");
        if (rows.Count > 1) return (null, $"Viindoo có nhiều sản phẩm cùng mã {defaultCode}. Kiểm tra lại trên Viindoo.");

        var r = rows[0]!;
        decimal qty = r["x_quantity_per_code"] is JsonValue v && v.TryGetValue(out decimal d) ? d : 0;
        return (new ViindooProduct(
            r["id"]!.GetValue<int>(),
            Str(r["default_code"]) is { Length: > 0 } code ? code : defaultCode,
            Str(r["name"]),
            qty > 0 ? (int)qty : null), null);
    }

    const string WoPrefix = "NM/MO/";

    // Thông tin WO (mrp.production) theo ĐÚNG tên (không ilike): name, state, product_id, product_code, product_name,
    // product_qty, origin. Thiếu tiền tố thì thêm NM/MO/. Lệnh đã xong / huỷ trả lỗi (giống GetWorkOrder cũ),
    // trừ khi allowClosed = true (màn nháp /toastv2, tra cứu: chỉ xem).
    // Không có lệnh đúng tên (đã tách backorder: NM/MO/03975 -> -001, -002...) thì lấy lệnh ĐANG MỞ mới nhất của nhóm cùng tên
    // (procurement_group_id); khi đó requested_name = tên đã nhập, name = lệnh thật (giống Twist lấy backorder gần nhất,
    // nhưng không dùng ilike nên không ra nhầm WO khác).
    public async Task<(Dictionary<string, string>? wo, string? error)> GetWorkOrderAsync(string? woCode, bool allowClosed = false)
    {
        woCode = (woCode ?? "").Trim().ToUpperInvariant();
        if (woCode.Length == 0) return (null, "Vui lòng nhập mã WO.");
        if (!woCode.StartsWith(WoPrefix)) woCode = WoPrefix + woCode;

        string[] fields = ["name", "state", "product_id", "product_qty", "origin", "procurement_group_id", "bom_id"];
        var rows = await SearchReadAsync("mrp.production", [new object[] { "name", "=", woCode }], fields, limit: 1);
        string requested = woCode;
        if (rows.Count == 0)
        {
            var open = await SearchReadAsync("mrp.production",
                [new object[] { "procurement_group_id.name", "=", woCode }, new object[] { "state", "not in", new[] { "done", "cancel" } }],
                fields, limit: 1, order: "id desc");
            if (open.Count == 0)
            {
                var group = await SearchReadAsync("mrp.production", [new object[] { "procurement_group_id.name", "=", woCode }], fields, limit: 5000, order: "id desc");
                if (group.Count == 0) return (null, $"Không tìm thấy lệnh sản xuất {woCode} trên Viindoo.");
                if (!allowClosed) return (null, $"Lệnh {woCode} đã hoàn thành hết ({group.Count} lệnh, mới nhất {Str(group[0]!["name"])}).");
                open = [group[0]!.DeepClone()];
            }
            rows = open;
        }

        var r = rows[0]!;
        string name = Str(r["name"]) is { Length: > 0 } n ? n : woCode;
        string state = Str(r["state"]);
        if (!allowClosed && state == "done") return (null, $"Lệnh {name} đã hoàn thành.");
        if (!allowClosed && state == "cancel") return (null, $"Lệnh {name} đã bị huỷ.");

        // product_id = [id, "[mã] tên"] -> tách mã và tên
        var product = r["product_id"] as JsonArray;
        int productId = product?[0] is JsonValue idValue && idValue.TryGetValue(out int pid) ? pid : 0;
        string display = Str(product?[1]), code = "", productName = display;
        int start = display.IndexOf('['), end = display.IndexOf(']');
        if (start != -1 && end > start)
        {
            code = display.Substring(start + 1, end - start - 1).Trim();
            productName = display[(end + 1)..].Trim();
        }
        decimal qty = r["product_qty"] is JsonValue q && q.TryGetValue(out decimal d) ? d : 0;

        return (new Dictionary<string, string>
        {
            // id lệnh (mrp.production) trên Viindoo: không đổi khi lệnh đổi tên (-001) hay tách backorder
            ["id"] = r["id"] is JsonValue idv && idv.TryGetValue(out int moId) ? moId.ToString(CultureInfo.InvariantCulture) : "",
            ["name"] = name,
            // Tên đã nhập; khác name khi đã chuyển sang lệnh đang mở của nhóm backorder
            ["requested_name"] = requested,
            ["state"] = state,
            ["product_id"] = productId > 0 ? productId.ToString(CultureInfo.InvariantCulture) : "",
            ["product_code"] = code,
            ["product_name"] = productName,
            ["product_qty"] = qty.ToString("0.##", CultureInfo.InvariantCulture),
            ["origin"] = Str(r["origin"]),
            // BoM của lệnh (tên hiển thị), trống = lệnh không có BoM
            ["bom"] = Str((r["bom_id"] as JsonArray)?[1]),
            // Nhóm cung ứng: lệnh gốc + các backorder (-001, -002...) cùng nhóm
            ["procurement_group_id"] = r["procurement_group_id"] is JsonArray g && g[0] is JsonValue gv && gv.TryGetValue(out int gid)
                ? gid.ToString(CultureInfo.InvariantCulture) : "",
            // Tên nhóm = tên gốc của lệnh (VD NM/MO/06230 cho cả 06230-001, -002...)
            ["procurement_group_name"] = Str((r["procurement_group_id"] as JsonArray)?[1])
        }, null);
    }

    // Dữ liệu tem theo WO (đã đọc bằng GetWorkOrderAsync), chỉ đọc:
    //   customer_part_number = product.product.x_custcode, qty_per_code = x_quantity_per_code (trống khi 0),
    //   sale_order / po / lot_id = sale.order (theo origin; lệnh con thì lần lên lệnh cha) x_ERP_PO / x_LotID.
    // x_ERP_PO / x_LotID là chuỗi nối dấu phẩy, mỗi giá trị ứng 1 dòng order_line theo thứ tự: lấy giá trị ở dòng đúng sản phẩm.
    // Không ra đúng 1 giá trị thì để trống + thêm cảnh báo.
    public async Task<(Dictionary<string, string> data, List<string> warnings)> GetLabelDataAsync(Dictionary<string, string> wo)
    {
        var data = new Dictionary<string, string> { ["customer_part_number"] = "", ["qty_per_code"] = "", ["sale_order"] = "", ["po"] = "", ["lot_id"] = "" };
        var warnings = new List<string>();
        int.TryParse(wo.GetValueOrDefault("product_id"), out int productId);

        if (productId > 0)
        {
            var products = await SearchReadAsync("product.product", [new object[] { "id", "=", productId }], ["x_custcode", "x_quantity_per_code"], limit: 1);
            if (products.Count == 0) warnings.Add($"Không đọc được sản phẩm ID {productId} trên Viindoo.");
            else
            {
                data["customer_part_number"] = Str(products[0]!["x_custcode"]);
                decimal qty = products[0]!["x_quantity_per_code"] is JsonValue v && v.TryGetValue(out decimal d) ? d : 0;
                data["qty_per_code"] = qty > 0 ? ((int)qty).ToString(CultureInfo.InvariantCulture) : "";
            }
        }

        // origin là lệnh khác (lệnh con): lần lên tối đa 5 cấp tới đơn bán. Nhóm cung ứng dùng để ghép dòng là nhóm của
        // lệnh trên cùng (lệnh có origin = đơn bán).
        string origin = wo.GetValueOrDefault("origin") ?? "";
        int.TryParse(wo.GetValueOrDefault("procurement_group_id"), out int groupId);
        for (int level = 0; level < 5 && origin.StartsWith(WoPrefix, StringComparison.OrdinalIgnoreCase); level++)
        {
            // Lệnh cha đã tách backorder thì không còn đúng tên, tìm theo nhóm cùng tên (lệnh đầu của nhóm)
            var parent = await SearchReadAsync("mrp.production",
                ["|", new object[] { "name", "=", origin }, new object[] { "procurement_group_id.name", "=", origin }],
                ["origin", "procurement_group_id"], limit: 1, order: "id");
            origin = parent.Count == 0 ? "" : Str(parent[0]!["origin"]);
            groupId = parent.Count > 0 && parent[0]!["procurement_group_id"] is JsonArray g && g[0] is JsonValue gv && gv.TryGetValue(out int gid) ? gid : 0;
        }
        if (origin.Length == 0)
        {
            warnings.Add("WO không có đơn bán (origin) nên không lấy được PO / Lot.");
            return (data, warnings);
        }

        var orders = await SearchReadAsync("sale.order", [new object[] { "name", "=", origin }], ["name", "x_ERP_PO", "x_LotID", "order_line"], limit: 1);
        if (orders.Count == 0)
        {
            warnings.Add($"Không tìm thấy đơn bán {origin} trên Viindoo nên không lấy được PO / Lot.");
            return (data, warnings);
        }
        var order = orders[0]!;
        string so = Str(order["name"]);
        data["sale_order"] = so;

        // Sản phẩm + số lượng của từng dòng đơn bán, đúng thứ tự order_line
        var lineIds = (order["order_line"] as JsonArray ?? []).Select(x => x!.GetValue<int>()).ToList();
        var byId = new Dictionary<int, SaleLine>();
        if (lineIds.Count > 0)
        {
            var rows = await SearchReadAsync("sale.order.line", [new object[] { "id", "in", lineIds }], ["product_id", "product_uom_qty"], limit: lineIds.Count);
            foreach (var l in rows)
            {
                int lp = l!["product_id"] is JsonArray p && p[0] is JsonValue pv && pv.TryGetValue(out int x) ? x : 0;
                decimal q = l["product_uom_qty"] is JsonValue qv && qv.TryGetValue(out decimal dq) ? dq : 0;
                byId[l["id"]!.GetValue<int>()] = new SaleLine(lp, q);
            }
        }
        var lines = lineIds.Select(id => byId.GetValueOrDefault(id) ?? new SaleLine(0, 0)).ToList();

        // Dòng của WO khi sản phẩm có ở nhiều dòng: chỉ tính khi cần, dùng chung cho PO và Lot
        LineGuess? guess = null;
        async Task<LineGuess> GuessLine() => guess ??= await GuessLineAsync(lines, productId, groupId, so);

        data["po"] = await PickForProductAsync(Str(order["x_ERP_PO"]), lines, productId, "PO", so, warnings, GuessLine);
        data["lot_id"] = await PickForProductAsync(Str(order["x_LotID"]), lines, productId, "Lot ID", so, warnings, GuessLine);
        return (data, warnings);
    }

    sealed record SaleLine(int ProductId, decimal Qty);

    // Index = dòng đoán được (null = không đoán được), Note = cách đoán / lý do
    sealed record LineGuess(int? Index, string Note);

    // Chọn giá trị của sản phẩm trong chuỗi nối dấu phẩy (1 giá trị / dòng đơn bán, đúng thứ tự order_line).
    // 1 giá trị duy nhất = áp cho cả đơn. Sản phẩm ở nhiều dòng với giá trị khác nhau: đoán dòng của WO (GuessLineAsync).
    static async Task<string> PickForProductAsync(string raw, List<SaleLine> lines, int productId, string label, string so,
        List<string> warnings, Func<Task<LineGuess>> guessLine)
    {
        var parts = raw.Split(',').Select(x => x.Trim()).ToList();
        if (raw.Length == 0 || parts.All(x => x.Length == 0))
        {
            warnings.Add($"Đơn bán {so} chưa có {label}.");
            return "";
        }
        if (parts.Count == 1) return parts[0];
        if (parts.Count != lines.Count)
        {
            warnings.Add($"{label} của đơn bán {so} có {parts.Count} giá trị nhưng đơn có {lines.Count} dòng, không biết lấy giá trị nào.");
            return "";
        }
        var values = lines.Select((l, i) => (l.ProductId, v: parts[i])).Where(x => x.ProductId == productId && x.v.Length > 0)
            .Select(x => x.v).Distinct().ToList();
        if (values.Count == 1) return values[0];
        if (values.Count == 0)
        {
            warnings.Add($"Đơn bán {so} không có {label} cho sản phẩm này.");
            return "";
        }

        var guess = await guessLine();
        if (guess.Index is int i && parts[i].Length > 0)
        {
            warnings.Add($"{label} = {parts[i]}: {guess.Note}");
            return parts[i];
        }
        warnings.Add($"Đơn bán {so} có nhiều {label} cho sản phẩm này ({string.Join(", ", values)}), chưa biết WO thuộc dòng nào: {guess.Note}");
        return "";
    }

    // Đoán dòng đơn bán của WO (Viindoo không lưu WO thuộc dòng nào). Mỗi dòng sinh 1 nhóm WO (lệnh gốc + backorder),
    // tổng số lượng của nhóm = số lượng dòng (đã kiểm với S00589).
    // 1. Tổng SL nhóm của WO khớp đúng 1 dòng cùng sản phẩm -> dòng đó.
    // 2. Khớp nhiều dòng (cùng SP, cùng SL) -> ghép theo thứ tự: các nhóm WO của SP (cùng tổng SL) xếp theo id ↔ các dòng đó theo thứ tự đơn.
    async Task<LineGuess> GuessLineAsync(List<SaleLine> lines, int productId, int groupId, string so)
    {
        if (groupId <= 0) return new(null, "WO không có nhóm cung ứng.");

        var groupRows = await SearchReadAsync("mrp.production", [new object[] { "procurement_group_id", "=", groupId }], ["product_qty"], limit: 5000);
        decimal total = groupRows.Sum(r => r!["product_qty"] is JsonValue v && v.TryGetValue(out decimal d) ? d : 0);
        string totalText = total.ToString("0.##", CultureInfo.InvariantCulture);

        var candidates = lines.Select((l, i) => (l, i)).Where(x => x.l.ProductId == productId && x.l.Qty == total).Select(x => x.i).ToList();
        if (candidates.Count == 0)
            return new(null, $"tổng SL nhóm WO ({totalText}) không khớp số lượng dòng nào.");
        if (candidates.Count == 1)
            return new(candidates[0], $"chọn theo số lượng (tổng SL nhóm WO {totalText} = dòng {candidates[0] + 1} của {so}), Viindoo không ghi rõ WO thuộc dòng nào.");

        // Nhiều dòng cùng SP, cùng SL: nhóm WO của SP này trên đơn (lệnh có origin = đơn bán), xếp theo id nhóm
        var all = await SearchReadAsync("mrp.production",
            [new object[] { "origin", "=", so }, new object[] { "product_id", "=", productId }], ["procurement_group_id", "product_qty"], limit: 5000);
        var totals = new Dictionary<int, decimal>();
        foreach (var r in all)
            if (r!["procurement_group_id"] is JsonArray g && g[0] is JsonValue gv && gv.TryGetValue(out int gid))
                totals[gid] = totals.GetValueOrDefault(gid) + (r["product_qty"] is JsonValue v && v.TryGetValue(out decimal d) ? d : 0);
        var sameGroups = totals.Where(kv => kv.Value == total).Select(kv => kv.Key).OrderBy(x => x).ToList();
        int rank = sameGroups.IndexOf(groupId);
        if (rank < 0 || sameGroups.Count != candidates.Count)
            return new(null, $"{candidates.Count} dòng cùng số lượng {totalText} nhưng tìm được {sameGroups.Count} nhóm WO.");
        return new(candidates[rank], $"chọn theo số lượng + thứ tự ({candidates.Count} dòng cùng SL {totalText}, nhóm WO thứ {rank + 1} = dòng {candidates[rank] + 1} của {so}), Viindoo không ghi rõ WO thuộc dòng nào.");
    }

    // ===== Tra cứu đơn bán (màn /toast/admin/sale-order) =====

    // Tổng quan 1 đơn bán: các dòng (sản phẩm, SL, PO / Lot / deadline tách theo dòng) + các nhóm WO (origin = đơn bán)
    // ghép với dòng theo sản phẩm + tổng SL nhóm (trùng SL thì theo thứ tự). Chỉ đọc. so: "S00589", "s589" hoặc "589".
    public async Task<(SaleOrderOverview? order, string? error)> GetSaleOrderOverviewAsync(string? so)
    {
        string name = NormalizeSaleOrder(so);
        if (name.Length == 0) return (null, "Vui lòng nhập mã đơn bán, VD: 589 hoặc S00589.");

        var orders = await SearchReadAsync("sale.order", [new object[] { "name", "=", name }],
            ["name", "state", "partner_id", "date_order", "x_ERP_PO", "x_LotID", "x_ERP_Deadline_Shipment", "order_line"], limit: 1);
        if (orders.Count == 0) return (null, $"Không tìm thấy đơn bán {name} trên Viindoo.");
        var o = orders[0]!;
        var warnings = new List<string>();

        // Dòng đơn, đúng thứ tự order_line
        var lineIds = (o["order_line"] as JsonArray ?? []).Select(x => x!.GetValue<int>()).ToList();
        var byId = new Dictionary<int, JsonNode>();
        if (lineIds.Count > 0)
            foreach (var l in await SearchReadAsync("sale.order.line", [new object[] { "id", "in", lineIds }], ["product_id", "product_uom_qty"], limit: lineIds.Count))
                byId[l!["id"]!.GetValue<int>()] = l;

        string rawPo = Str(o["x_ERP_PO"]), rawLot = Str(o["x_LotID"]), rawDeadline = Str(o["x_ERP_Deadline_Shipment"]);
        var po = SplitPerLine(rawPo, lineIds.Count, "PO", warnings);
        var lot = SplitPerLine(rawLot, lineIds.Count, "Lot ID", warnings);
        var deadline = SplitPerLine(rawDeadline, lineIds.Count, "Deadline", warnings);

        var lines = lineIds.Select((id, i) =>
        {
            var l = byId.GetValueOrDefault(id);
            var product = l?["product_id"] as JsonArray;
            return new SaleOrderLineInfo(i + 1,
                product?[0] is JsonValue pv && pv.TryGetValue(out int pid) ? pid : 0,
                Str(product?[1]),
                Dec(l?["product_uom_qty"]),
                po[i], lot[i], deadline[i], null);
        }).ToList();

        // Nhóm WO: mọi lệnh có origin = đơn bán (lệnh gốc + backorder; lệnh con có origin = lệnh khác nên không có ở đây)
        const int MaxWo = 5000;
        var wos = await SearchReadAsync("mrp.production", [new object[] { "origin", "=", name }],
            ["name", "state", "product_id", "product_qty", "procurement_group_id"], limit: MaxWo);
        if (wos.Count >= MaxWo) warnings.Add($"Đơn có từ {MaxWo} lệnh trở lên, chỉ đọc {MaxWo} lệnh đầu, số liệu nhóm WO có thể thiếu.");
        var groups = wos
            .Where(w => w!["procurement_group_id"] is JsonArray)
            .GroupBy(w => ((JsonArray)w!["procurement_group_id"]!)[0]!.GetValue<int>())
            .Select(g =>
            {
                var list = g.OrderBy(w => w!["id"]!.GetValue<int>()).ToList();
                var latest = list[^1]!;
                var product = latest["product_id"] as JsonArray;
                return new WoGroupInfo(g.Key,
                    Str(((JsonArray)latest["procurement_group_id"]!)[1]),
                    product?[0] is JsonValue pv && pv.TryGetValue(out int pid) ? pid : 0,
                    Str(product?[1]),
                    list.Count,
                    list.Sum(w => Dec(w!["product_qty"])),
                    list.Where(w => Str(w!["state"]) is not ("done" or "cancel")).Select(w => Str(w!["name"])).ToList(),
                    Str(latest["name"]), Str(latest["state"]));
            })
            .OrderBy(g => g.GroupId).ToList();

        // Ghép nhóm với dòng: cùng sản phẩm + tổng SL nhóm = SL dòng. Nhiều dòng / nhóm cùng SL: ghép theo thứ tự nếu số lượng bằng nhau.
        var unmatched = new List<WoGroupInfo>();
        foreach (var bucket in groups.GroupBy(g => (g.ProductId, g.TotalQty)))
        {
            var lineIdx = lines.Select((l, i) => (l, i)).Where(x => x.l.ProductId == bucket.Key.ProductId && x.l.Qty == bucket.Key.TotalQty)
                .Select(x => x.i).ToList();
            var bg = bucket.ToList();
            if (lineIdx.Count == bg.Count)
                for (int k = 0; k < bg.Count; k++) lines[lineIdx[k]] = lines[lineIdx[k]] with { Group = bg[k] };
            else
                unmatched.AddRange(bg);
        }

        return (new SaleOrderOverview(Str(o["name"]), Str(o["state"]), Str((o["partner_id"] as JsonArray)?[1]), Str(o["date_order"]),
            rawPo, rawLot, rawDeadline, lines, unmatched, wos.Count, warnings), null);
    }

    // "589" -> "S00589", "s589" -> "S00589", "S00589" giữ nguyên
    static string NormalizeSaleOrder(string? so)
    {
        string s = (so ?? "").Trim().ToUpperInvariant();
        if (s.StartsWith('S')) s = s[1..];
        if (s.Length == 0) return "";
        return s.All(char.IsDigit) ? "S" + s.PadLeft(5, '0') : "S" + s;
    }

    // Tách chuỗi nối dấu phẩy thành giá trị từng dòng: 1 giá trị = cả đơn; số giá trị khác số dòng thì để trống + cảnh báo
    static List<string> SplitPerLine(string raw, int lineCount, string label, List<string> warnings)
    {
        var parts = raw.Split(',').Select(x => x.Trim()).ToList();
        if (raw.Length == 0) return Enumerable.Repeat("", lineCount).ToList();
        if (parts.Count == 1) return Enumerable.Repeat(parts[0], lineCount).ToList();
        if (parts.Count == lineCount) return parts;
        warnings.Add($"{label} có {parts.Count} giá trị nhưng đơn có {lineCount} dòng, không tách được theo dòng.");
        return Enumerable.Repeat("", lineCount).ToList();
    }

    public static decimal Dec(JsonNode? node) => node is JsonValue v && v.TryGetValue(out decimal d) ? d : 0;

    const int MaxEmployees = 5000;

    // Danh sách nhân viên có mã SVN. Viindoo không có field mã nhân viên: mã nằm ở Thẻ (hr.employee.category, tên thẻ = mã,
    // màn Nhân viên hiện thẻ dưới tên). Tên thẻ bỏ dấu cách ("SVN 0886" -> SVN0886), chỉ nhận SVN + 4 số (bỏ "061", "SVN 00892",
    // "HR Staff"...). Nhân viên không thẻ thì bỏ. 1 mã gắn nhiều nhân viên (bản ghi trùng, VD SVN0892 có 3 bản ghi "Lê Thị Thư"):
    // lấy bản ghi sửa gần nhất (__last_update), bằng nhau thì id nhỏ nhất. Nhân viên đã lưu trữ (nghỉ) Viindoo không trả về.
    public async Task<List<ViindooEmployee>> GetEmployeesAsync()
    {
        var codeByTag = new Dictionary<int, string>();
        foreach (var t in await SearchReadAsync("hr.employee.category", [], ["id", "name"], limit: MaxEmployees))
        {
            string code = new string(Str(t!["name"]).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            if (code.Length == 7 && code.StartsWith("SVN") && code[3..].All(char.IsAsciiDigit)) codeByTag[t["id"]!.GetValue<int>()] = code;
        }

        var best = new Dictionary<string, (string updated, int id, ViindooEmployee employee)>();
        foreach (var e in await SearchReadAsync("hr.employee", [], ["id", "name", "work_phone", "category_ids", "__last_update"], limit: MaxEmployees))
        {
            int id = e!["id"]!.GetValue<int>();
            string updated = Str(e["__last_update"]);
            foreach (var tag in e["category_ids"] as JsonArray ?? [])
            {
                if (tag is not JsonValue tv || !tv.TryGetValue(out int tagId) || !codeByTag.TryGetValue(tagId, out string? code)) continue;
                if (best.TryGetValue(code, out var cur) && (string.CompareOrdinal(updated, cur.updated) < 0
                    || (updated == cur.updated && id > cur.id))) continue;
                best[code] = (updated, id, new ViindooEmployee(code, Str(e["name"]), Str(e["work_phone"])));
            }
        }
        return best.Values.Select(b => b.employee).OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
    }

    // domain có thể chứa toán tử "|" / "&" của Odoo. order VD "id desc". offset để phân trang.
    public async Task<JsonArray> SearchReadAsync(string model, object[] domain, string[] fields, int limit, int offset = 0, string? order = null)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "search_read phải có limit.");
        object kwargs = order == null ? new { domain, fields, limit, offset } : new { domain, fields, limit, offset, order };
        var result = await CallKwAsync(model, "search_read", [], kwargs);
        return result as JsonArray ?? [];
    }

    // Đọc theo id (read, số bản ghi = số id nên không cần limit; chặn tối đa 500 id)
    public async Task<JsonArray> ReadAsync(string model, IEnumerable<int> ids, string[] fields)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return [];
        if (list.Count > 500) throw new ArgumentOutOfRangeException(nameof(ids), "read tối đa 500 id mỗi lần.");
        var result = await CallKwAsync(model, "read", [list, fields], new { });
        return result as JsonArray ?? [];
    }

    async Task<JsonNode?> CallKwAsync(string model, string method, object[] args, object kwargs, bool write = false)
    {
        if (write ? !WriteMethods.Contains((model, method)) : !ReadMethods.Contains(method))
            throw new InvalidOperationException($"Toast không được gọi {model}.{method} trên Viindoo.");
        var opt = options.CurrentValue;
        if (opt.Url.Length == 0 || opt.Db.Length == 0 || opt.Login.Length == 0 || opt.Password.Length == 0)
            throw new InvalidOperationException("Chưa cấu hình kết nối Viindoo (mục Viindoo: Url, Db, Login, Password).");

        for (int attempt = 0; ; attempt++)
        {
            await EnsureLoginAsync(opt);
            var (result, error) = await PostAsync(opt, "/web/dataset/call_kw", new { model, method, args, kwargs });
            if (error == null) return result;
            // Hết phiên: đăng nhập lại và thử 1 lần nữa
            if (attempt == 0 && error.Contains("Session", StringComparison.OrdinalIgnoreCase))
            {
                loggedIn = false;
                continue;
            }
            throw new InvalidOperationException("Viindoo báo lỗi: " + error);
        }
    }

    async Task EnsureLoginAsync(ViindooRpcOptions opt)
    {
        if (loggedIn) return;
        await loginLock.WaitAsync();
        try
        {
            if (loggedIn) return;
            var (result, error) = await PostAsync(opt, "/web/session/authenticate", new { db = opt.Db, login = opt.Login, password = opt.Password });
            if (error != null || result?["uid"] is not JsonValue uid || !uid.TryGetValue(out int _))
                throw new InvalidOperationException("Đăng nhập Viindoo không được" + (error != null ? ": " + error : " (sai tài khoản / mật khẩu)."));
            loggedIn = true;
        }
        finally
        {
            loginLock.Release();
        }
    }

    async Task<(JsonNode? result, string? error)> PostAsync(ViindooRpcOptions opt, string path, object parameters)
    {
        var http = httpFactory.CreateClient(HttpClientName);
        var response = await http.PostAsJsonAsync(opt.Url.TrimEnd('/') + path, new { jsonrpc = "2.0", method = "call", @params = parameters });
        if (!response.IsSuccessStatusCode) return (null, $"HTTP {(int)response.StatusCode}");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        if (body?["error"] is JsonNode err)
            return (null, Str(err["data"]?["message"]) is { Length: > 0 } m ? m : Str(err["message"]));
        return (body?["result"], null);
    }

    // Odoo trả false cho trường trống
    public static string Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : "";
}
