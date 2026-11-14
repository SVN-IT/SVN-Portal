using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.LabelTemplate;

namespace Toast.Tests;

/// <summary>Biến tự điền (Configs/label-variables.json): file được nạp, biến Optional được để trống.</summary>
public class LabelVariableTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    static readonly LabelVariablesOptions Config = new()
    {
        Variables =
        [
            new() { Key = "NOTE", Source = "manual", Optional = true },
            new() { Key = "LOT", Source = "manual" }
        ]
    };

    [Fact]
    public void Optional_variable_may_be_blank_and_prints_empty()
    {
        const string zpl = "^XA^FD{LOT}|{NOTE}^FS^XZ";
        var optional = LabelVariableResolver.OptionalKeys(Config);

        Assert.Contains("{LOT}", LabelVariableResolver.Fill(zpl, new() { ["LOT"] = "", ["NOTE"] = "" }, out _, optional));
        Assert.Null(LabelVariableResolver.Fill(zpl, new() { ["LOT"] = "L1", ["NOTE"] = "" }, out string filled, optional));
        Assert.Equal("^XA^FDL1|^FS^XZ", filled);

        var template = new LabelTemplate { ZplData = zpl };
        Assert.Equal(["LOT"], LabelVariableResolver.MissingKeys(template, Config));
        Assert.True(LabelVariableResolver.Resolve(template, Config, new LabelContext()).Single(v => v.Key == "NOTE").Optional);
    }

    [Fact]
    public void Config_file_is_loaded()
    {
        var config = factory.Services.GetRequiredService<IOptionsMonitor<LabelVariablesOptions>>().CurrentValue;
        Assert.Contains(config.Variables, v => v.Key == "QTY");
    }
}
