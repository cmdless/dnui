# Contract: Dnui App Model (public API)

Package `Cmdless.Dnui`, namespace `Dnui`. This is the complete public surface added by this
feature. Everything else is `internal`.

```csharp
namespace Dnui;

public abstract class DnuiApp : Avalonia.Application
{
    // Base applies the default setup: WithInterFont(), LogToTrace(),
    // and WithDeveloperTools() in Debug builds of Dnui.
    // Override to customize; call base.Configure(builder) to keep the defaults.
    protected virtual void Configure(Avalonia.AppBuilder builder);

    // The single way to produce the app's outcome. Writes `result` as one JSON line to the
    // captured stdout (nothing when null), then shuts down with `exitCode`.
    // First call wins; later calls are ignored. Safe to call from any thread.
    // Errors: pass {"error":{"code","message"}} with a non-zero exit code (64+ for app codes).
    protected void Resolve(System.Text.Json.Nodes.JsonNode? result, int exitCode = 0);

    // The process's captured stdin/stdout. Captured, and Console.Out redirected to stderr,
    // before this instance is constructed, so it is valid in the constructor.
    protected DnuiStdio Stdio { get; }
}

public sealed class DnuiStdio
{
    // True when stdin and stdout are both redirected (pipes or files).
    public bool IsRedirected { get; }

    // True once a session has been opened.
    public bool IsSessionOpen { get; }

    // Claims stdin+stdout for a session protocol. Succeeds at most once per process.
    // Throws DnuiStdioException (Reason = NotRedirected | AlreadyClaimed) otherwise.
    // After this, Resolve(nonNull) is dropped; end the session with Resolve(null, code).
    public DnuiSession OpenSession();
}

public sealed class DnuiSession
{
    public System.IO.Stream Input { get; }   // raw stdin
    public System.IO.Stream Output { get; }  // raw stdout; disposing it does not close the handle
}

public sealed class DnuiStdioException : System.InvalidOperationException
{
    public DnuiStdioFailure Reason { get; }
}

public enum DnuiStdioFailure { NotRedirected, AlreadyClaimed }

public abstract class DnuiCommandApp : DnuiApp
{
    // Runs the built-in command chosen by the runtime. Cannot be overridden.
    public sealed override void OnFrameworkInitializationCompleted();
}
```

## Modes

| Derive from | Use when | You get |
|---|---|---|
| `DnuiCommandApp` | You want the built-in commands (tray) with your own startup tweaks, styles, or resources | `Configure` override, XAML styles/resources; the command runs automatically |
| `DnuiApp` (one-shot) | You want full control and use Dnui only as a cached Avalonia host | `Configure` override; you implement `OnFrameworkInitializationCompleted` and call `Resolve` with your outcome |
| `DnuiApp` (session) | Same, but you speak your own protocol with the caller | `Stdio.OpenSession()` for raw streams; end with `Resolve(null, code)` |

## Ordering guarantees

1. The runtime checks that the type derives from `DnuiApp` before creating it.
2. For `DnuiCommandApp`, the command is set before any configuration runs.
3. The command's builder options (for example, tray hiding the macOS Dock icon) are applied
   **before** `Configure`, so an override can deliberately change them.
4. `Configure` runs after `UsePlatformDetect()` and before the app starts.
5. stdout is captured and `Console.Out` redirected to stderr before the app instance is created,
   so `Console.WriteLine` in app code (constructors included) never reaches the real stdout.
6. If an app shuts down without calling `Resolve`, it exits with the lifetime's exit code and no
   result, which callers see as `StdOut = null`.
7. While a session is open (tray, or an app's own), a non-null `Resolve` result is dropped and
   reported on stderr, so it can't corrupt the protocol. The exit code still applies.
8. The app instance is created before the command is set. A `DnuiCommandApp` subclass that opens
   a session itself (for example, in its constructor) makes a session command like tray fail
   with exit code 22.

## Compatibility

Removing any of these types or members, or changing its meaning or ordering, is a MAJOR change
(constitution V). Adding protected virtual members with no-op defaults is MINOR.
