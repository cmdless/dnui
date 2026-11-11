# Contract: Tray Protocol v1

**Protocol version**: `1` (integer; `initialize` requires an exact match)

## Transport

- JSON-RPC 2.0 over the runtime's stdin (caller → runtime) and stdout (runtime → caller).
- **Framing**: newline-delimited. Each message is one JSON object on one line, ending with
  `\n`. Messages must not contain raw line breaks. Standard JSON serializers already escape
  newlines inside strings.
- UTF-8, no byte-order mark.
- Requests sent before the runtime's UI is ready are queued and handled in order.
- **Shutdown**: close the runtime's stdin, or exit. There is no shutdown method.
- **Limit**: native code writing directly to file descriptor 1 inside the runtime isn't
  intercepted. Managed `Console` output is.

## Requests (caller → runtime)

Every request gets its response only after the requested change is visible.

### `initialize`

Must be the first request. It succeeds at most once.

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{
  "protocolVersion":1,
  "icon":{"path":"icon.png"},
  "tooltip":"My App",
  "items":[
    {"id":"open","text":"Open"},
    {"separator":true},
    {"id":"more","text":"More","items":[{"id":"about","text":"About"}]},
    {"id":"quit","text":"Quit"}
  ]}}
```

`icon`, `tooltip`, and `items` are optional. If present, they're validated together, and
nothing is applied unless all of them are valid.

Result: `{"runtimeVersion":"0.1.0"}`

### `tray/setItems`

```json
{"jsonrpc":"2.0","id":2,"method":"tray/setItems","params":{"items":[ ... ]}}
```

Replaces the whole menu. `[]` shows the placeholder item (see [data-model.md](../data-model.md)). Result: `{}`.

### `tray/setIcon`

```json
{"jsonrpc":"2.0","id":3,"method":"tray/setIcon","params":{"icon":{"png":"iVBORw0..."},"tooltip":"Connected"}}
```

If `tooltip` is omitted, the current tooltip stays. Result: `{}`.

### `tray/getState`

```json
{"jsonrpc":"2.0","id":4,"method":"tray/getState","params":{}}
```

Result: what the tray is showing right now.

```json
{"visible":true,"hasIcon":true,"tooltip":"My App","items":[{"id":"open","text":"Open","enabled":true,"checked":null,"separator":false,"items":null}]}
```

`items` is the caller's last applied list, not the placeholder. The icon image isn't returned.

### `tray/simulateOpen`

```json
{"jsonrpc":"2.0","id":5,"method":"tray/simulateOpen","params":{}}
```

Sends `tray/opened` exactly as a real menu opening would, then responds `{}`. The notification
is always written before the response.

### `tray/simulateClick`

```json
{"jsonrpc":"2.0","id":6,"method":"tray/simulateClick","params":{"id":"open"}}
```

Sends `tray/clicked {"id":"open"}` exactly as a real selection would, then responds `{}`. It
follows the same rules as a user: an unknown id, a disabled item, a separator, or a submenu
parent → -32012. While the placeholder is shown, there's nothing selectable.

All three methods require `initialize` first (-32001), and work the same with or without
`--headless`.

### Menu item and icon shapes

See [data-model.md](../data-model.md) (MenuItem, Icon).

## Notifications (runtime → caller)

```json
{"jsonrpc":"2.0","method":"tray/opened","params":{}}
{"jsonrpc":"2.0","method":"tray/clicked","params":{"id":"open"}}
```

- `tray/opened`: sent each time the tray menu opens, on every platform, whichever click opens
  it. Clicks on the icon that don't open the menu (for example, a Windows left click) aren't
  reported. The notification arrives after the menu has started opening. A `tray/setItems`
  sent in response is guaranteed to apply by the next opening. Whether it updates the
  already-open menu depends on the platform.
- `tray/clicked`: sent when a menu item is selected. `id` is the item's id. `""` appears only if
  a platform lets the user select the disabled placeholder item, and callers should ignore it.
- An empty menu always shows the placeholder item, so `tray/opened` behaves the same with or
  without items.

## Errors

Standard JSON-RPC errors (-32700 parse error, -32600 invalid request, -32601 method not
found, -32602 invalid params) apply. Application errors:

| Code | Name | When | `data` |
|---|---|---|---|
| -32001 | NotInitialized | `tray/*` request before `initialize` | — |
| -32002 | AlreadyInitialized | second `initialize` | — |
| -32003 | ProtocolVersionMismatch | `initialize.protocolVersion` ≠ 1 | `{"requested":n,"supported":[1]}` |
| -32010 | InvalidItems | validation failure (data-model rules) | `{"path":"items[1]","id":"x","reason":"duplicate-id"}` |
| -32011 | InvalidIcon | icon unreadable, ambiguous, or undecodable | `{"reason":"..."}` |
| -32012 | InvalidSimulation | `tray/simulateClick` target is unknown, disabled, a separator, or a submenu parent | `{"id":"x","reason":"unknown" \| "disabled" \| "separator" \| "parent"}` |

A malformed message never crashes the runtime. If a request id can be recovered, an error
response is sent.

## Compatibility

Adding optional fields, methods, or notifications is a MINOR change and keeps protocol
version `1`. Removing or changing the meaning of any of these bumps the protocol version and is
a MAJOR runtime version change (constitution V).
