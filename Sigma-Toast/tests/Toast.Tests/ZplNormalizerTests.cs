using System.Text.Json;
using Toast.Web.Features.LabelTemplate;

namespace Toast.Tests;

/// <summary>Công cụ Chuẩn hoá ZPL (màn Mẫu tem): đọc chữ / barcode, thay biến, căn giữa.</summary>
public class ZplNormalizerTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    // Rút gọn từ tem carton Sling (ZebraDesigner xuất, có ^FH\ và ^CI27 sau mỗi dòng chữ)
    const string Zpl = "^XA^PW2362^LL3543\r\n" +
        "^FO96,114^GB0,3283,12^FS\r\n" +
        "^FO1183,2605^GB0,789,12^FS\r\n" +
        "^FO2253,114^GB0,3283,12^FS\r\n" +
        "^FT186,1846^A0N,92,96^FH\\^CI28^FDToast PO Number:^FS^CI27\r\n" +
        "^FT1010,2022^A0N,92,96^FH\\^CI28^FDPO12345^FS^CI27\r\n" +
        "^BY14,3,339^FT474,2443^BCN,,N,N\r\n^FH\\^FD>:PO1>52345^FS\r\n" +
        "^FT0,2878^A0N,92,96^FB1292,1,24,C^FH\\^CI28^FD{QTY}\\5C&^FS^CI27\r\n" +
        "^FT186,1106^A0N,92,96^FH\\^CI28^FDModel: {MODEL}^FS^CI27\r\n" +
        "^BY9,3,339^FT389,3282^BCN,,N,N\r\n^FH\\^FD>:30^FS\r\n" +
        "^XZ";

    static ZplNormalizer.Field Find(List<ZplNormalizer.Field> fields, string kind, string sample) =>
        fields.Single(f => f.Kind == kind && f.Sample == sample);

    [Fact]
    public void Analyze_lists_text_and_barcodes_with_decoded_samples()
    {
        var fields = ZplNormalizer.Analyze(Zpl);
        Assert.Equal(6, fields.Count); // 3 đường ^GB không tính
        Assert.Equal("Code 128", Find(fields, "barcode", "PO12345").Symbology); // >:PO1>52345 -> PO12345
        Assert.False(Find(fields, "text", "PO12345").Centered);
        var qty = Find(fields, "text", "{QTY}"); // bỏ đuôi \5C& của ^FB
        Assert.Equal("QTY", qty.Variable);
        Assert.True(qty.Centered);
        Assert.True(Find(fields, "text", "Model: {MODEL}").MixedVariable);
    }

    [Fact]
    public void Apply_replaces_variable_and_centers_like_zebradesigner_block()
    {
        var fields = ZplNormalizer.Analyze(Zpl);
        var (zpl, warnings) = ZplNormalizer.Apply(Zpl, [
            new() { Id = Find(fields, "text", "PO12345").Id, Variable = "{PO}", Center = true },
            new() { Id = Find(fields, "barcode", "PO12345").Id, Variable = "PO", Center = true },
            new() { Id = Find(fields, "barcode", "30").Id, Center = true }
        ]);
        Assert.Empty(warnings);
        // chữ: x = mép trái ô (đường 96..108), rộng ô 108..2253, căn giữa, đuôi xuống dòng như ZebraDesigner
        Assert.Contains("^FT108,2022^A0N,92,96^FB2145,1,0,C^FH\\^CI28^FD{PO}\\5C&^FS", zpl);
        // barcode: ^FXFIT trước ^BY, dữ liệu không còn mã kiểu
        Assert.Contains("^FXFIT^BY14,3,339^FT474,2443^BCN,,N,N\r\n^FH\\^FD{PO}^FS", zpl);
        // căn giữa giá trị cố định: ghi lại dạng chưa mã hoá
        Assert.Contains("^FXFIT^BY9,3,339^FT389,3282^BCN,,N,N\r\n^FH\\^FD30^FS", zpl);
        // phần không chọn giữ nguyên
        Assert.Contains("^FT186,1846^A0N,92,96^FH\\^CI28^FDToast PO Number:^FS^CI27", zpl);
        Assert.Contains("^FD{QTY}\\5C&", zpl);
    }

    [Fact]
    public void Apply_without_changes_keeps_zpl_and_rejects_bad_names()
    {
        var fields = ZplNormalizer.Analyze(Zpl);
        Assert.Equal(Zpl, ZplNormalizer.Apply(Zpl, []).Zpl);
        var (zpl, warnings) = ZplNormalizer.Apply(Zpl, [new() { Id = Find(fields, "text", "PO12345").Id, Variable = "1PO" }]);
        Assert.Equal(Zpl, zpl);
        Assert.Single(warnings);
    }

    [Fact]
    public void Normalized_label_prints_centered_values()
    {
        var fields = ZplNormalizer.Analyze(Zpl);
        var (zpl, _) = ZplNormalizer.Apply(Zpl, [new() { Id = Find(fields, "barcode", "PO12345").Id, Variable = "PO", Center = true }]);
        string filled = ZplVariables.Apply(zpl, [new() { Key = "PO", Value = "125" }]);
        // ">:125" = 3 ký tự B -> 68 module * 14 = 952; ô 108..2253 -> x = 108 + (2145 - 952) / 2 = 704
        Assert.Contains("^FT704,2443", filled);
    }

    [Fact]
    public async Task Api_analyze_and_apply()
    {
        var (client, token) = await factory.LoginAsync();
        var analyzed = await ToastFactory.PostAsync(client, token, "/toast/admin/label-template/api/normalize/analyze", new { zplData = Zpl });
        Assert.True(analyzed.GetProperty("success").GetBoolean(), analyzed.ToString());
        var po = analyzed.GetProperty("fields").EnumerateArray()
            .First(f => f.GetProperty("kind").GetString() == "barcode" && f.GetProperty("sample").GetString() == "PO12345");
        Assert.NotEmpty(analyzed.GetProperty("variables").EnumerateArray());

        var applied = await ToastFactory.PostAsync(client, token, "/toast/admin/label-template/api/normalize/apply",
            new { zplData = Zpl, changes = new[] { new { id = po.GetProperty("id").GetInt32(), variable = "PO", center = true } } });
        Assert.True(applied.GetProperty("success").GetBoolean(), applied.ToString());
        Assert.Contains("^FXFIT^BY14", applied.GetProperty("zplData").GetString());

        var empty = await ToastFactory.PostAsync(client, token, "/toast/admin/label-template/api/normalize/analyze", new { zplData = "^XA^XZ" });
        Assert.False(empty.GetProperty("success").GetBoolean());
    }
}
