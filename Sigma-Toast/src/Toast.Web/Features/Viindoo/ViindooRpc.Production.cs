using System.Globalization;
using System.Text.Json.Nodes;

namespace Toast.Web.Features.Viindoo;

// Kết quả nhập KQSX 1 lệnh. Qty = số đã nhập; DoneName = tên lệnh sau khi xong (VD NM/MO/06384-001, không tách thì giữ tên);
// BackorderName = lệnh phần còn lại (null = lệnh xong hết); Excess = số pcs Toast có nhưng không nhập vì vượt SL còn lại của lệnh.
public sealed record ProductionPushResult(int Qty, string DoneName, string? BackorderName, int Excess);

// Chi tiết 1 lần nhập KQSX (lưu JSON vào Toast_Session.PushDetail, kể cả khi lỗi): lệnh trước / sau, số lượng, tiêu hao từng NVL,
// các bước đã chạy (read → write → mark_done → verify) và lỗi ở bước nào
public sealed class ProductionPushTrace
{
    public PushMo Mo { get; set; } = new();
    public int ToastQty { get; set; }
    public int PushQty { get; set; }
    public int Excess { get; set; }
    public string? Backorder { get; set; }
    public List<PushMaterial> Materials { get; set; } = [];
    public List<PushStep> Steps { get; set; } = [];
    // Lần gần nhất Toast thật sự gửi lệnh hoàn thành (button_mark_done) cho lệnh này: giữ qua các lần thử sau (lần thử không gửi gì
    // không xoá), để đối chiếu khi lệnh đã done
    public PushSent? LastSent { get; set; }
}

public sealed record PushSent(int Qty, DateTime At);

public sealed class PushMo
{
    public int Id { get; set; }
    public string? NameBefore { get; set; }
    public string? NameAfter { get; set; }
    public string? StateBefore { get; set; }
    public string? StateAfter { get; set; }
    public decimal PlanQty { get; set; }
}

public sealed record PushMaterial(int MoveId, string Code, decimal UnitFactor, decimal QtyDone);

// Wizard: Viindoo vẫn bật wizard khi hoàn thành lệnh (mrp.consumption.warning / mrp.production.backorder), null = không.
// Note: ghi chú thêm (VD bước đối chiếu: đã so số / giờ / người hoàn thành)
public sealed record PushStep(string Step, DateTime At, bool Ok, string? Error = null, string? Wizard = null, string? Note = null);

// Lệnh đã hoàn thành trên Viindoo khi Toast định nhập: thông tin để đối chiếu (DateFinished = giờ máy chủ Toast, Viindoo lưu UTC)
public sealed record MoDoneInfo(int Id, string Name, decimal QtyProduced, DateTime? DateFinished, string DoneBy, string? Backorder);

public sealed class MoAlreadyDoneException(MoDoneInfo info)
    : InvalidOperationException($"Lệnh {info.Name} đã hoàn thành trên Viindoo, không nhập nữa.")
{
    public MoDoneInfo Info { get; } = info;
}

/// <summary>
/// Nhập kết quả sản xuất (KQSX) 1 lệnh lên Viindoo theo id lệnh. Phần GHI duy nhất của Toast lên Viindoo.
///   1. Đọc lệnh (trạng thái, SL còn lại, tracking) + dòng NVL (unit_factor = tỉ lệ BoM / 1 thành phẩm).
///   2. write mrp.production: qty_producing = số nhập (tối đa SL còn lại của lệnh); tiêu hao mỗi NVL = số nhập × unit_factor
///      (làm tròn lên theo đơn vị NVL như Viindoo tự tính khi nhập tay).
///   3. button_mark_done 1 lần với context bỏ qua wizard: skip_consumption (cảnh báo tiêu hao) + skip_backorder,
///      mo_ids_to_backorder = [lệnh] khi còn thiếu (Viindoo tách phần còn lại sang lệnh mới -002, lệnh này đổi tên -001).
///      Viindoo vẫn trả wizard (mrp.consumption.warning / mrp.production.backorder) thì tạo + xác nhận wizard như SVN-Portal cũ.
///   4. Đọc lại lệnh: phải ở trạng thái done; lấy tên mới + lệnh backorder đang mở của nhóm.
/// Lệnh đã done / huỷ: báo lỗi, không ghi gì (chống nhập 2 lần). Sản phẩm theo dõi lot / serial: không nhập (Toast không quét serial).
/// </summary>
public partial class ViindooRpc
{
    // Wizard Viindoo có thể bật khi hoàn thành lệnh (mrp.immediate.production: Toast không xử lý, báo lỗi)
    static readonly HashSet<string> Wizards = ["mrp.consumption.warning", "mrp.production.backorder", "mrp.immediate.production"];

    static readonly HashSet<(string model, string method)> WriteMethods =
    [
        ("mrp.production", "write"), ("mrp.production", "button_mark_done"),
        ("mrp.consumption.warning", "create"), ("mrp.consumption.warning", "action_confirm"),
        ("mrp.production.backorder", "create"), ("mrp.production.backorder", "action_backorder")
    ];

    // trace: ghi lại chi tiết từng bước (người gọi lưu lại kể cả khi lỗi)
    public async Task<ProductionPushResult> PushProductionAsync(int moId, int toastQty, ProductionPushTrace trace)
    {
        trace.Mo.Id = moId;
        trace.ToastQty = toastQty;
        var mo = await StepAsync(trace, "read", () => ReadMoAsync(moId));
        string name = Str(mo["name"]), state = Str(mo["state"]);
        (trace.Mo.NameBefore, trace.Mo.StateBefore, trace.Mo.PlanQty) = (name, state, Dec(mo["product_qty"]));
        // Đã hoàn thành: không ghi gì, trả thông tin để ProductionPushService đối chiếu (Toast đã nhập trước đó hay bên khác)
        if (state == "done") throw new MoAlreadyDoneException(await DoneInfoAsync(mo));
        if (state == "cancel") throw new InvalidOperationException($"Lệnh {name} đã huỷ trên Viindoo, không nhập.");
        string tracking = Str(mo["product_tracking"]);
        if (tracking is "serial" or "lot") throw new InvalidOperationException($"Sản phẩm của lệnh {name} theo dõi {tracking}, Toast không nhập KQSX.");
        int remaining = (int)Math.Round(Dec(mo["product_qty"]), MidpointRounding.AwayFromZero);
        int qty = Math.Min(toastQty, remaining);
        (trace.PushQty, trace.Excess) = (Math.Max(qty, 0), Math.Max(toastQty - Math.Max(remaining, 0), 0));
        if (qty <= 0) throw new InvalidOperationException($"Không có số lượng để nhập cho lệnh {name}.");

        // Tiêu hao NVL theo tỉ lệ BoM
        var moveIds = (mo["move_raw_ids"] as JsonArray ?? []).Select(x => x!.GetValue<int>()).ToList();
        var moves = await ReadAsync("stock.move", moveIds, ["id", "unit_factor", "state", "product_uom", "product_id"]);
        var uomIds = moves.Select(m => (m!["product_uom"] as JsonArray)?[0]?.GetValue<int>() ?? 0).Where(x => x > 0).ToList();
        var rounding = (await ReadAsync("uom.uom", uomIds, ["id", "rounding"]))
            .ToDictionary(u => u!["id"]!.GetValue<int>(), u => Dec(u!["rounding"]));
        var rawWrites = new List<object>();
        foreach (var m in moves)
        {
            if (Str(m!["state"]) is "done" or "cancel") continue;
            int uom = (m["product_uom"] as JsonArray)?[0]?.GetValue<int>() ?? 0;
            decimal factor = Dec(m["unit_factor"]);
            decimal done = RoundUp(qty * factor, rounding.GetValueOrDefault(uom, 0.0001m));
            int moveId = m["id"]!.GetValue<int>();
            rawWrites.Add(new object[] { 1, moveId, new { quantity_done = done } });
            trace.Materials.Add(new PushMaterial(moveId, ProductCode((m["product_id"] as JsonArray)?[1]), factor, done));
        }
        await StepAsync(trace, "write", () => CallKwAsync("mrp.production", "write",
            [new[] { moId }, new { qty_producing = qty, move_raw_ids = rawWrites }], new { }, write: true));

        bool last = qty >= remaining;
        var context = new Dictionary<string, object>
        {
            ["skip_consumption"] = true, ["skip_backorder"] = true,
            ["mo_ids_to_backorder"] = last ? Array.Empty<int>() : new[] { moId }
        };
        trace.LastSent = new PushSent(qty, DateTime.Now);
        var result = await StepAsync(trace, "mark_done", () => CallKwAsync("mrp.production", "button_mark_done", [new[] { moId }], new { context }, write: true));
        // Phòng Viindoo không nhận context: xử lý wizard như SVN-Portal cũ (tối đa 2 wizard: cảnh báo tiêu hao rồi backorder).
        // Kết quả khác (True, hoặc action mở lệnh backorder vừa tạo: res_model mrp.production) = đã xong, sang bước đọc lại.
        for (int i = 0; i < 2 && result is JsonObject action && Str(action["res_model"]) is { } wizard && Wizards.Contains(wizard); i++)
        {
            var current = action;
            result = await StepAsync(trace, "wizard", () => ConfirmWizardAsync(wizard, current, last), wizard);
        }

        var after = await StepAsync(trace, "verify", async () =>
        {
            var row = await ReadMoAsync(moId);
            (trace.Mo.NameAfter, trace.Mo.StateAfter) = (Str(row["name"]), Str(row["state"]));
            if (Str(row["state"]) != "done")
                throw new InvalidOperationException($"Đã gửi KQSX nhưng lệnh {name} chưa hoàn thành trên Viindoo (trạng thái {Str(row["state"])}). Kiểm tra trên Viindoo.");
            return row;
        });
        string? backorder = last ? null : await OpenBackorderAsync(after);
        trace.Backorder = backorder;
        return new ProductionPushResult(qty, Str(after["name"]), backorder, toastQty - qty);
    }

    // Chạy 1 bước, ghi kết quả (thành công / lỗi) vào trace; lỗi thì ném tiếp
    static async Task<T> StepAsync<T>(ProductionPushTrace trace, string step, Func<Task<T>> run, string? wizard = null)
    {
        try
        {
            var r = await run();
            trace.Steps.Add(new PushStep(step, DateTime.Now, true, Wizard: wizard));
            return r;
        }
        catch (Exception ex)
        {
            trace.Steps.Add(new PushStep(step, DateTime.Now, false, ex.Message, wizard));
            throw;
        }
    }

    // "[RPA02-00565] RPA02-00565" -> "RPA02-00565"
    static string ProductCode(JsonNode? display)
    {
        string s = Str(display);
        int end = s.IndexOf(']');
        return s.StartsWith('[') && end > 0 ? s[1..end] : s;
    }

    // Lệnh đang mở mới nhất cùng nhóm (phần còn lại sau khi tách backorder), null = không còn
    async Task<string?> OpenBackorderAsync(JsonObject mo)
    {
        if (mo["procurement_group_id"] is not JsonArray g || g[0] is not JsonValue gv || !gv.TryGetValue(out int groupId)) return null;
        var open = await SearchReadAsync("mrp.production",
            [new object[] { "procurement_group_id", "=", groupId }, new object[] { "state", "not in", new[] { "done", "cancel" } }],
            ["name"], limit: 1, order: "id desc");
        return open.Count > 0 ? Str(open[0]!["name"]) : null;
    }

    async Task<MoDoneInfo> DoneInfoAsync(JsonObject mo)
    {
        DateTime? finished = DateTime.TryParse(Str(mo["date_finished"]), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) ? utc.ToLocalTime() : null;
        string by = Str((mo["write_uid"] as JsonArray)?[1]);
        return new MoDoneInfo(mo["id"]!.GetValue<int>(), Str(mo["name"]), Dec(mo["qty_produced"]), finished, by, await OpenBackorderAsync(mo));
    }

    async Task<JsonObject> ReadMoAsync(int moId)
    {
        var rows = await ReadAsync("mrp.production", [moId],
            ["id", "name", "state", "product_qty", "product_tracking", "move_raw_ids", "procurement_group_id",
             "qty_produced", "date_finished", "write_uid"]);
        return rows.Count > 0 && rows[0] is JsonObject mo ? mo : throw new InvalidOperationException($"Không tìm thấy lệnh id {moId} trên Viindoo.");
    }

    // Tạo + xác nhận wizard Viindoo trả về từ button_mark_done (dòng wizard lấy từ context default_*_line_ids)
    async Task<JsonNode?> ConfirmWizardAsync(string wizard, JsonObject action, bool last)
    {
        var ctx = action["context"] as JsonObject ?? [];
        static JsonArray Lines(JsonNode? raw) => new((raw as JsonArray ?? []).Select(l => (JsonNode?)new JsonArray(0, 0, l?[2]?.DeepClone())).ToArray());
        object context = JsonNodeToObject(ctx);
        switch (wizard)
        {
            case "mrp.consumption.warning":
            {
                var ids = ctx["button_mark_done_production_ids"]?.DeepClone() ?? new JsonArray();
                var vals = new JsonObject
                {
                    ["mrp_production_ids"] = new JsonArray(new JsonArray(6, false, ids)),
                    ["mrp_consumption_warning_line_ids"] = Lines(ctx["default_mrp_consumption_warning_line_ids"])
                };
                int id = (await CallKwAsync(wizard, "create", [vals], new { context }, write: true))!.GetValue<int>();
                return await CallKwAsync(wizard, "action_confirm", [new[] { id }], new { context }, write: true);
            }
            case "mrp.production.backorder" when !last:
            {
                var vals = new JsonObject { ["mrp_production_backorder_line_ids"] = Lines(ctx["default_mrp_production_backorder_line_ids"]) };
                int id = (await CallKwAsync(wizard, "create", [vals], new { context }, write: true))!.GetValue<int>();
                return await CallKwAsync(wizard, "action_backorder", [new[] { id }], new { context }, write: true);
            }
            default:
                throw new InvalidOperationException($"Viindoo yêu cầu xác nhận {wizard} khi hoàn thành lệnh, Toast chưa xử lý. Kiểm tra trên Viindoo.");
        }
    }

    static object JsonNodeToObject(JsonObject node) => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(node.ToJsonString())!;

    // Làm tròn lên theo bước làm tròn của đơn vị (VD 0,0001), bỏ sai số dấu phẩy động trước khi làm tròn
    static decimal RoundUp(decimal value, decimal step)
    {
        if (step <= 0) return value;
        decimal n = Math.Round(value / step, 6);
        return Math.Ceiling(n) * step;
    }
}
