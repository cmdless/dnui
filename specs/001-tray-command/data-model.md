# Data Model: Tray Command and Protocol

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## MenuItem

One entry in the tray menu, as sent by the caller.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `id` | string | yes, unless `separator` | — | Non-empty. Unique across the whole tree, at any depth. Reported back in `tray/clicked`. `""` is reserved for the placeholder. |
| `text` | string | yes, unless `separator` | — | Display text. |
| `enabled` | bool | no | `true` | Disabled items never produce clicks. |
| `checked` | bool \| null | no | `null` | `null` = not a check item. `true`/`false` = check item in that state. The runtime never toggles it. |
| `separator` | bool | no | `false` | When true, all other fields are ignored and the item produces no clicks. |
| `items` | MenuItem[] | no | `null` | Makes the item a submenu parent. Parents produce no clicks. An empty array counts as no submenu. |

**Validation** (FR-019): the whole tree is validated before anything changes. The first
failure is reported:
1. A non-separator item without a non-empty `id` → `InvalidItems` with the item's path, for
   example `items[2].items[0]`.
2. A non-separator item without `text` → `InvalidItems` with the path.
3. An `id` that appears more than once at any depth → `InvalidItems` with the duplicate `id`
   and both paths.

Valid trees are applied as a whole. Invalid trees leave the current menu untouched.

## Icon

| Form | Representation | Notes |
|---|---|---|
| File path | `{ "path": "/abs/or/relative.png" }` | Resolved against the runtime's working directory. Read when the request is handled. |
| Inline | `{ "png": "<base64>" }` | Base64-encoded PNG bytes. |

Exactly one of `path` or `png` is allowed. Both, neither, an unreadable file, bad base64, or
undecodable image data → `InvalidIcon`, and the current icon stays (FR-021).

## TrayState

What the runtime is currently showing. Changed only by caller requests.

| Field | Type | Initial | Changed by |
|---|---|---|---|
| `icon` | decoded image \| none | none | `initialize`, `tray/setIcon` |
| `tooltip` | string \| none | none | `initialize`, `tray/setIcon` (omitted = keep) |
| `items` | MenuItem[] | `[]` | `initialize`, `tray/setItems`. When empty, the menu shows the placeholder (below). |
| visible | derived | false | true once `icon` is set |

## Placeholder item

Shown whenever `items` is empty, so the menu always opens and `tray/opened` fires the same way
everywhere.

| Field | Value |
|---|---|
| `id` | `""` (reserved; caller items can't use it) |
| `text` | `No items` |
| `enabled` | `false`, where the platform supports disabled items |

The caller never sends the placeholder. It never appears in caller-visible state, and it is
replaced as soon as a non-empty item list is applied.

## Session lifecycle

```text
 Starting ──(framework ready, listening)──▶ AwaitingInitialize
 AwaitingInitialize ──initialize ok──▶ Running
 AwaitingInitialize ──initialize bad version──▶ AwaitingInitialize (error returned)
 AwaitingInitialize ──any tray/* request──▶ AwaitingInitialize (NotInitialized error)
 Running ──initialize──▶ Running (AlreadyInitialized error)
 Running ──tray/setItems | tray/setIcon | tray/getState | tray/simulate*──▶ Running
 any ──input end-of-file / write failure──▶ ShuttingDown ──▶ Resolve(null, 0): icon removed, exit 0
```

## Notifications (runtime → caller)

| Name | Payload | Raised when |
|---|---|---|
| `tray/opened` | `{}` | The tray menu opens (any platform, any click that opens it) |
| `tray/clicked` | `{ "id": string }` | An enabled, non-separator, non-parent item is selected. `id` is `""` only if a platform lets the placeholder be selected. |

Notifications are sent in the order the events happened, through a single queue, and never
block the UI thread (research R7).

## Runtime cache (library side)

| State of `{DnuiRoot}/packages/{id}/{version}/` | Meaning | Resolver action |
|---|---|---|
| missing | never downloaded | lock → download → verify → extract → publish |
| present with `.dnui-complete` | ready | return the executable path (no lock, no network) |
| present without the marker | left by an interrupted or old run | treated as missing, deleted under the lock |
| `.tmp-{guid}` sibling | in-progress or crashed extraction | deleted under the lock |

Details: [contracts/runtime-acquisition.md](contracts/runtime-acquisition.md).

## App type (runtime side)

| Kind | Rule | Outcome |
|---|---|---|
| Built-in `App` | used when `--app-assembly`/`--app-type` are absent | `DnuiCommandApp`: command parsed and run |
| Custom `DnuiCommandApp` subclass | `--app-type` derives from `DnuiCommandApp` | command parsed and run; subclass `Configure` applied |
| Custom `DnuiApp` subclass | derives from `DnuiApp` but not `DnuiCommandApp` | `Configure` applied; no command parsed; the app owns initialization |
| Anything else | doesn't derive from `DnuiApp` | exit 12; no instance created |
