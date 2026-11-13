using Dnui.Hosting;

namespace Dnui.Tests.Hosting;

/// <summary>Spec 003 FR-023 / research D1: every overload form produces the same runtime command line.</summary>
public class HostArgumentTests
{
    private readonly List<string[]> _launched = [];

    private DnuiHost Host() =>
        new(new DnuiHostOptions(), app: null, (launch, _) =>
        {
            _launched.Add([.. launch.Arguments()]);
            return Task.FromResult(new DnuiResult(0, launch.Command[0] == "window" ? """{"result":null}""" : """{"path":null}"""));
        });

    private static readonly string WindowPath = AssemblyLocator.Find(typeof(TestWindow).Assembly);
    private static readonly string AppPath = AssemblyLocator.Find(typeof(TestApp).Assembly);

    [Fact]
    public async Task WithApp_AllFormsGiveTheSameArguments()
    {
        var host = Host();
        await host.WithApp<TestApp>().SaveFileAsync();
        await host.WithApp(typeof(TestApp)).SaveFileAsync();
        await host.WithApp(AppPath, typeof(TestApp).FullName!).SaveFileAsync();

        Assert.Equal(3, _launched.Count);
        Assert.All(_launched, a => Assert.Equal(_launched[0], a));
        Assert.Equal(["--app-assembly", AppPath, "--app-type", typeof(TestApp).FullName!, "dialog", "save-file"], _launched[0]);
    }

    [Fact]
    public async Task OpenWindow_AllFormsGiveTheSameArguments()
    {
        var host = Host();
        await host.OpenWindowAsync<TestWindow>();
        await host.OpenWindowAsync(typeof(TestWindow));
        await host.OpenWindowAsync(WindowPath, typeof(TestWindow).FullName!);

        Assert.All(_launched, a => Assert.Equal(["window", WindowPath, typeof(TestWindow).FullName!], a));
    }

    [Fact]
    public async Task WithApp_OnBoundHost_ReplacesTheApp()
    {
        await Host().WithApp("/first.dll", "First").WithApp("/second.dll", "Second").OpenFilesAsync();
        Assert.Equal(["--app-assembly", "/second.dll", "--app-type", "Second", "dialog", "open-file"], Assert.Single(_launched));
    }

    [Fact]
    public void WithApp_RejectsNonCommandAppTypes() =>
        Assert.Throws<ArgumentException>(() => Host().WithApp(typeof(string)));

    [Fact]
    public void OpenWindow_RejectsNonWindowTypes() =>
        Assert.Throws<ArgumentException>(() => { _ = Host().OpenWindowAsync(typeof(string)); });

    public sealed class TestApp : DnuiCommandApp;

    public sealed class TestWindow : DnuiWindow;
}
