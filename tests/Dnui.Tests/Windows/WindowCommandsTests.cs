using System.Text.Json.Nodes;
using Dnui.TestApps;

namespace Dnui.Tests.Windows;

/// <summary>
/// US3/US4: window commands (parented dialogs, child windows), in-process on Avalonia's headless
/// platform. Every test in this class shares the fake picker, so they run one at a time.
/// </summary>
[Collection(nameof(UiThreadCollection))]
public class WindowCommandsTests
{
    private static string PathFor(string name) => Path.Combine(Path.GetTempPath(), name);

    private static Task<JsonNode?> RunAsync(Func<DnuiWindow> create, params JsonNode?[] picks) => HeadlessTestApp.RunAsync(async () =>
    {
        TestStorage.Reset();
        TestStorage.Queue(picks);
        var window = create();
        window.Show();
        return await window.Completion;
    });

    [Fact]
    public async Task ParentedDialog_ReceivesPick()
    {
        var result = await RunAsync(() => new PickOneWindow(), new JsonArray(PathFor("x")));
        Assert.Equal(PathFor("x"), result?["paths"]?[0]?.GetValue<string>());
        Assert.Equal("open-file multiple=True filters=Text:*.txt", Assert.Single(TestStorage.Calls));
    }

    [Fact]
    public async Task TwoDialogs_AreServedInRequestOrder_OneAtATime()
    {
        var result = await RunAsync(() => new PickTwiceWindow(), PathFor("a"), PathFor("b"));
        Assert.Equal([PathFor("a"), PathFor("b")], result!["results"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(["start:a", "start:b", "end:a", "end:b"], result["order"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public async Task WindowClosingDuringDialog_CancelsIt()
    {
        var result = await RunAsync(() => new ObservesDialogCloseWindow(), "wait");
        Assert.True(result?["dialogIsNull"]?.GetValue<bool>());
    }

    [Fact]
    public async Task MissingPick_FailsTheDialog()
    {
        var failure = await HeadlessTestApp.RunAsync(async () =>
        {
            TestStorage.Reset();
            var caught = new TaskCompletionSource<Exception>();
            void OnUnhandled(object? sender, Avalonia.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                caught.TrySetResult(e.Exception);
                e.Handled = true;
            }

            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += OnUnhandled;
            var window = new PickOneWindow();
            window.Show();
            try
            {
                return await caught.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                Avalonia.Threading.Dispatcher.UIThread.UnhandledException -= OnUnhandled;
                window.Close();
            }
        });
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ChildWindow_ResultFlowsToParent()
    {
        var result = await RunAsync(() => new ParentOfChildWindow());
        Assert.Equal("x", result?["child"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task ParentClosing_ClosesChild_WithNull()
    {
        var result = await RunAsync(() => new ObservesParentCloseWindow());
        Assert.Equal("""{"parentIsNull":true,"childIsNull":true}""", result?.ToJsonString());
    }

    [Fact]
    public async Task ChildFromUnloadedAssembly_Loads()
    {
        var extra = typeof(WindowCommandsTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "DnuiTestAppsExtraDll").Value!;
        Environment.SetEnvironmentVariable("DNUI_TEST_EXTRA", Path.GetFullPath(extra));
        var result = await RunAsync(() => new ParentOfExternalChildWindow());
        Assert.True(result?["child"]?["extra"]?.GetValue<bool>());
        Assert.Contains(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Dnui.TestApps.Extra");
    }
}

[CollectionDefinition(nameof(UiThreadCollection), DisableParallelization = true)]
public sealed class UiThreadCollection;
