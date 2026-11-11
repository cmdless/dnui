# Quickstart: Validate the Tray Command

Steps that prove the feature works end to end. Contracts:
[cli.md](contracts/cli.md), [tray-protocol.md](contracts/tray-protocol.md),
[app-model.md](contracts/app-model.md).

## Prerequisites

- .NET 10 SDK
- A desktop session with a system tray:
  - Windows: the notification area
  - macOS: the menu bar
  - Linux: a StatusNotifierItem host, for example KDE, or GNOME with the AppIndicator extension
- A small PNG to use as the icon (`icon.png`)

## 1. Build and automated tests

```bash
dotnet build Dnui.slnx
dotnet test Dnui.slnx
```

Expected: build passes with 0 warnings, and all unit and contract tests pass. On Linux CI, run
the tests under `xvfb-run`. Contract tests use `DNUI_TRAY_HOST=recording` and don't need a real
tray.

## 2. Manual: real tray round trip (each OS)

Start the runtime with piped stdio. Any tool that keeps stdin open works. With bash and a FIFO:

```bash
mkfifo /tmp/dnui-in
dotnet run --project Dnui.Runtime -- tray < /tmp/dnui-in &
exec 3> /tmp/dnui-in
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"icon":{"path":"icon.png"},"tooltip":"Dnui test","items":[{"id":"open","text":"Open"},{"separator":true},{"id":"quit","text":"Quit"}]}}' >&3
```

Expected:
- A response `{"jsonrpc":"2.0","id":1,"result":{"runtimeVersion":"0.1.0"}}` on stdout.
- The icon and the tooltip "Dnui test" in the tray.
- Opening the menu prints `{"jsonrpc":"2.0","method":"tray/opened","params":{}}`. Confirm this
  on every OS. Note whether a Windows left click opens the menu.
- Clicking **Open** prints `{"jsonrpc":"2.0","method":"tray/clicked","params":{"id":"open"}}`.
- On macOS, no Dock icon appears.

Update the menu:

```bash
echo '{"jsonrpc":"2.0","id":2,"method":"tray/setItems","params":{"items":[{"id":"a","text":"A"},{"id":"a","text":"Dup"}]}}' >&3
```

Expected: error `-32010` naming duplicate id `a`. The menu is unchanged.

Clear the menu:

```bash
echo '{"jsonrpc":"2.0","id":3,"method":"tray/setItems","params":{"items":[]}}' >&3
```

Expected: opening the menu shows a single grayed-out "No items" entry and still prints
`tray/opened`. Selecting it does nothing. If a platform lets you select it anyway, it prints
`tray/clicked` with `"id":""`. Record which platforms do that.

Shut down:

```bash
exec 3>&-
```

Expected: the icon disappears and the runtime exits with code 0 within 2 seconds.

## 3. Manual: caller crash

Repeat step 2, then kill the shell that holds file descriptor 3, for example by closing the
terminal. Expected: the icon disappears and the runtime process exits within 2 seconds.

## 4. Failure paths

| Run | Expected exit |
|---|---|
| `dotnet run --project Dnui.Runtime -- tray` from an interactive terminal | 20 |
| `... -- --app-assembly missing.dll --app-type X tray` | 10 |
| `... -- --app-assembly tests/Dnui.TestApps/bin/Debug/net10.0/Dnui.TestApps.dll --app-type Dnui.TestApps.NotAnApp tray` | 12 |
| Linux session without a tray host: `... -- tray` with piped stdio | 21 |
| `... -- --app-assembly <TestApps.dll> --app-type Dnui.TestApps.EagerSessionCommandApp tray` with piped stdio | 22 |

The tray runs write nothing to stdout, only the exit code (stderr may show diagnostics). The
same failures without `tray`, for example a bad `--app-type` with no command, write one line
like `{"error":{"code":"AppTypeInvalid","message":"..."}}` to stdout.

## 5. Custom app types

- `--app-type Dnui.TestApps.CustomCommandApp tray`: the tray works as in step 2, and the
  subclass's `Configure` marker appears on stderr.
- `--app-type Dnui.TestApps.TakeoverApp`: the takeover app's own window or behavior appears,
  and it resolves a single JSON line.
- `--app-type Dnui.TestApps.EchoSessionApp` with piped stdio: a takeover app that opens a
  session and echoes each input line. The lines come back on stdout. Closing stdin exits 0
  with no extra output.

## 6. .NET tray client

Use the minimal example in [contracts/tray-client.md](contracts/tray-client.md) from a scratch
console app that references the Dnui project, with `RuntimePath` pointing at the built
`Dnui.Runtime` executable.

Expected:
- `StartAsync` completes once the tray is visible.
- Opening the menu raises `Opened`, and selecting an item raises `Clicked` with its id.
- Selecting **Quit** disposes the tray. The icon disappears, and `Completion` returns
  `ExitCode = 0, StdOut = null`.
- With an invalid `AppType`, `StartAsync` throws `DnuiTrayStartException` with
  `Result.ExitCode` = 11 or 12.
- Killing the runtime process from a task manager makes `Completion` return a non-zero exit
  code without throwing.

