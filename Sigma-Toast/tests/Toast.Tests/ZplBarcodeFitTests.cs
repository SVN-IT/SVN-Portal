using Toast.Web.Features.LabelTemplate;

namespace Toast.Tests;

public class ZplBarcodeFitTests
{
    // Khung 2 ô (giống tem carton Sling): viền trái 96..108, vách giữa 1175..1187, viền phải 2253..2265
    const string Label = """
        ^XA^PW2362^LL3543
        ^FO96,114^GB0,3283,12^FS
        ^FO1175,2605^GB0,789,12^FS
        ^FO2253,114^GB0,3283,12^FS
        ^FT0,2789^A0N,92,96^FDQuantity^FS^CI27
        ^FXFIT^FS^BY9,3,339^FT385,3282^BCN,,N,N
        ^FH\^FD>:{QTY}^FS
        ^FXFIT^BY6,3,339^FT1318,3282^BCN,,N,N
        ^FH\^FD>:{LOT_ID}^FS
        ^XZ
        """;

    [Theory]
    [InlineData("HW0169", ">:HW>50169", 5)]          // giống ZebraDesigner tự mã hoá
    [InlineData("4500012345", ">;4500012345", 5)]    // toàn số: kiểu C, 2 chữ số / ký tự
    [InlineData("30", ">;30", 1)]
    [InlineData("125", ">:125", 3)]                  // số lẻ ngắn: B ngắn bằng hoặc hơn
    [InlineData("PVT", ">:PVT", 3)]
    [InlineData("0001-0-0626", ">;0001>6-0->50626", 9)]
    [InlineData("A>B", ">:A><B", 3)]                 // dấu > viết là >< trong ZPL
    public void Encode_uses_fewest_symbols(string data, string expected, int symbols)
    {
        var r = ZplBarcodeFit.Encode(data);
        Assert.NotNull(r);
        Assert.Equal(expected, r.Value.Data);
        Assert.Equal(symbols, r.Value.Symbols);
    }

    [Fact]
    public void Unmarked_zpl_is_unchanged()
    {
        string zpl = Label.Replace("^FXFIT^FS", "").Replace("^FXFIT", "");
        string filled = ZplVariables.Apply(zpl, [new() { Key = "QTY", Value = "240" }, new() { Key = "LOT_ID", Value = "0001-0-0626" }]);
        Assert.Contains("^FT385,3282", filled);
        Assert.Contains("^FD>:240^FS", filled);
        Assert.Contains("^FD>:0001-0-0626^FS", filled);
    }

    [Fact]
    public void Marked_barcodes_are_centered_in_their_cell()
    {
        string filled = ZplVariables.Apply(Label, [new() { Key = "QTY", Value = "240" }, new() { Key = "LOT_ID", Value = "0001-0-0626" }]);
        // QTY ">:240" = 3 ký tự B -> 11 * 5 + 13 = 68 module * 9 = 612; ô 108..1175 -> x = 108 + (1067 - 612) / 2 = 335
        Assert.Contains("^BY9,3,339^FT335,3282", filled);
        Assert.Contains("^FD>:240^FS", filled);
        // Lot 9 ký tự -> 11 * 11 + 13 = 134 module * 6 = 804; ô 1187..2253 -> x = 1187 + (1066 - 804) / 2 = 1318
        Assert.Contains("^BY6,3,339^FT1318,3282", filled);
        Assert.Contains("^FD>;0001>6-0->50626^FS", filled);
    }

    [Fact]
    public void Too_wide_barcode_gets_narrower_bars()
    {
        string filled = ZplVariables.Apply(Label, [new() { Key = "QTY", Value = "1" }, new() { Key = "LOT_ID", Value = "0001-0-0626-REWORK-ABCDEFGH" }]);
        // ^BY6 không vừa ô 1066 dots (kể cả lề 10 module mỗi bên) -> giảm
        Assert.DoesNotContain("^BY6,3,339^FT", filled.Split("^FH")[2]);
        Assert.Matches(@"\^FXFIT\^BY[1-3],3,339\^FT\d+,3282", filled);
    }

    [Fact]
    public void Barcode_with_unfilled_variable_is_left_as_is()
    {
        // xem trước mẫu chưa điền / in khi mất mạng (trình duyệt điền biến sau)
        string filled = ZplVariables.Apply(Label, [new() { Key = "QTY", Value = "30" }]);
        Assert.Contains("^BY6,3,339^FT1318,3282", filled);
        Assert.Contains("^FD>:{LOT_ID}^FS", filled);
    }

    [Fact]
    public void Fitted_label_renders()
    {
        string filled = ZplVariables.Apply(Label, [new() { Key = "QTY", Value = "240" }, new() { Key = "LOT_ID", Value = "0001-0-0626" }]);
        var result = ZplPreviewService.Render(filled, "4x6", 24);
        Assert.Single(result.Images);
        Assert.Empty(result.SkippedElements);
    }
}
