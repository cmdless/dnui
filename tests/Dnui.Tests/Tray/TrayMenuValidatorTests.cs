using Dnui.Tray;

namespace Dnui.Tests.Tray;

public class TrayMenuValidatorTests
{
    private static TrayMenuItem Item(string? id, string? text = "T", List<TrayMenuItem>? items = null) => new() { Id = id, Text = text, Items = items };

    [Fact]
    public void Valid_Tree_Passes() =>
        Assert.Null(TrayMenuValidator.Validate([Item("a"), new TrayMenuItem { Separator = true }, Item("b", items: [Item("c")])]));

    [Fact]
    public void MissingId_ReportsPath() =>
        Assert.Equal(new("items[2].items[0]", null, "missing-id"),
            TrayMenuValidator.Validate([Item("a"), Item("b"), Item("p", items: [Item(null)])]));

    [Fact]
    public void MissingText_ReportsPath()
    {
        var error = TrayMenuValidator.Validate([Item("a", text: null)]);
        Assert.Equal("items[0]", error!.Path);
        Assert.Equal("missing-text", error.Reason);
    }

    [Fact]
    public void DuplicateId_AtAnyDepth_ReportsBothPaths()
    {
        var error = TrayMenuValidator.Validate([Item("x"), Item("p", items: [Item("x")])]);
        Assert.Equal("duplicate-id", error!.Reason);
        Assert.Equal("x", error.Id);
        Assert.Equal("items[1].items[0]", error.Path);
        Assert.Equal("items[0]", error.OtherPath);
    }

    [Fact]
    public void Separators_IgnoreOtherFields() =>
        Assert.Null(TrayMenuValidator.Validate([new TrayMenuItem { Separator = true, Id = null, Text = null }, new TrayMenuItem { Separator = true }]));

    [Fact]
    public void EmptySubmenu_CountsAsNoSubmenu()
    {
        var item = Item("a", items: []);
        Assert.Null(TrayMenuValidator.Validate([item]));
        Assert.False(item.IsParent);
    }

    [Fact]
    public void FirstFailureIsReported() =>
        Assert.Equal("missing-id", TrayMenuValidator.Validate([Item(null), Item("a", text: null)])!.Reason);
}
