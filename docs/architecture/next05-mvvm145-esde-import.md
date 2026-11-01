# NEXT-05 / #145 — First MVVM consolidation lot: standard ES-DE import

**Scope:** shell, explicit historical ES-DE import, refresh, and collection state.
Base `main=1aac0912930bd0a040e06800a1e8316fbe077dc2`. This is a
**structure-only, fixture-reviewed** step; **not** a standard-EXE human recipe.

## Responsibility boundary

| Component | Responsibility |
| --- | --- |
| `MainWindow.cs` / `MainWindow.Imported.cs` | Native Avalonia folder picker, second explicit read-only confirmation, visual layout, scroll/focus, messages, image attachment and view-only resource cleanup. No `Task.Run` import orchestration. |
| `MvvmFoundation/ImportedCatalogueViewModel` | Observable `Current`, `IsBusy`, `ImportStatus`, revision, commands `Import`/`Refresh`/`Cancel`; UI selection/filter/paging/nav adapters, cancellation and `Dispose`. No direct XML read, filesystem open, scan or permission grant. |
| `ImportedCatalogueUiState` | Existing presentation state, mode, search/filter/pagination, selected game and diagnostics. Receives an explicitly *accepted* session/snapshot; legacy synchronous `TryImport/TryReimport` test adapters now delegate to application service. |
| `IEsDeImportApplicationService` / `EsDeImportApplicationService` | Injectable staging of a read-only import on a worker and refresh of the explicit session. No automatic root selection, permission creation, saved root or new scanner. |
| `EsDeImportSession` / `EsDeCatalogueImporter` | Unchanged sole authority for XML/playlist validation, fingerprint V2, path/reparse controls, media-reference membership and permitted read-only media handle open. |

### Lifetime and failure semantics

- Normal start performs **no import**. `ImportedCatalogueViewModel.ImportConfirmedAsync` is
  invoked only after the native folder picker and a separate positive confirmation.
  `ImportCommand` requires `ConfirmedEsDeImportRequest.ExplicitUserConsent=true`.
- Single-flight `IsBusy` blocks competing imports and refreshes. The user may request
  cancellation; `Dispose` cancels active work and suppresses late notifications.
  The synchronous ES-DE parser **cannot be forcibly stopped** from the UI.
  Its completed result is discarded if the VM was cancelled or disposed.
- `ImportedCatalogueUiState.Snapshot` stores only the last **accepted** snapshot.
  Failed or cancelled attempts cannot directly replace that UI `Current`.
  In the existing backend, `EsDeImportSession.Import` may internally finish a
  refresh after cancellation; its result is **not adopted** into UI `Current`.
  The legacy media-opening path remains in its original service and will be
  disentangled in a **separate approved lot**.
- On successful refresh: preserve library ID, search/system selection, page where
  possible and game/detail when still referenced. On expected failure: redact
  underlying exception/path and keep the previously accepted snapshot.
- Filters, page navigation (maximum 50 cards), native controls, images and
  responsive placement remain visually unchanged. The native View retains
  layout and scroll restoration.

### Acceptance evidence

`ImportedCatalogueMvvmBoundaryTests` exercises service injection, opt-in refusal,
state/command notifications, 55-entry paging, refresh identity, a deferred cancel
completion, deferred completion after Dispose and redaction on failure. Verify
`dotnet test` on Windows and the complete repository CI/policy/DocFX gate at the
exact PR HEAD. An EXE-standard interactive Windows recipe is still
`NOT_EXECUTED/BLOCKED_ENV` in [#95](../product-status.md); do not substitute
CI harnesses or these unit fixtures for that proof.

### Explicit exclusions and holds

No changes to `LegacyImportedImageBudget`, historical image scheduler,
`MediaPreviewService`, G1, RS-A/MI-A/MI-B scanners, SQLite/`ICatalogStore`,
ESDE-FP-V2, DocFX infrastructure or the owner-approved media limits.
No real ROM/SSD access, no permission bypass, no automatic scan, no migration,
no merge by DEV 3, and **no Release**. All `REAL_*_HOLD` and
`NEXT05_RELEASE_HOLD` remain active.

Review/merge is strictly delegated to DEV 2 and the Controller.
