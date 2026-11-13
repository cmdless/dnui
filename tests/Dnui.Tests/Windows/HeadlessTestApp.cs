using Avalonia;
using Avalonia.Headless;
using Dnui.TestApps;

[assembly: AvaloniaTestApplication(typeof(Dnui.Tests.Windows.HeadlessTestApp))]

namespace Dnui.Tests.Windows;

/// <summary>
/// The Avalonia app for in-process UI tests: the headless platform plus the fake file picker, the
/// same setup as Dnui.TestApps.HeadlessApp in the runtime. <see cref="RunAsync{T}"/> runs a test
/// body on its UI thread.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    private static readonly HeadlessUnitTestSession s_session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTestApp).Assembly);

    /// <summary>Runs <paramref name="body"/> on the headless UI thread and returns its result.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> body) => s_session.Dispatch(body, TestContext.Current.CancellationToken);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<HeadlessTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
        .UseTestStorage();
}
