# Copy/paste (shapes + connectors) and image support — Implementation plan

Branch: `feature/copy-paste-and-images`. Design: `documentation/specs/2026-06-04-copy-paste-and-images-design.md`.

Clipboard editing (Copy/Cut/Paste/Duplicate of nodes + their connectors) plus a new embedded `ImageNode`
inserted by paste / file picker / drag-&-drop, with full clipboard interop. Reuses `NodeBase.Clone()`,
the polymorphic serializer, the memento undo, and the service-abstraction pattern.

## Steps

1. **Image model** — `Draw.Model/Nodes/ImageNode.cs` (new): `byte[] Data` (base64 in JSON automatically),
   `string Format`, `int PixelWidth`/`PixelHeight`, `Clone()`. Register
   `[JsonDerivedType(typeof(ImageNode), "image")]` on `NodeBase`.

2. **Services** — `Draw.App/Services/IClipboardService.cs` (new, interface + impl): `SetClipAsync(string,
   Bitmap?)`, `TryGetClipAsync()`, `TryGetBitmapAsync()` over the Avalonia 12 typed clipboard
   (`DataFormat.CreateStringApplicationFormat`, `SetDataAsync`/`TryGetDataAsync`, `Set/TryGetBitmapAsync`);
   clipboard obtained as in `ImageExportService`. Add `PickOpenImageAsync()` to `IFileDialogService`
   (PNG/JPG/BMP/GIF). Register `IClipboardService` in `Hosting/ServiceCollectionExtensions.cs`.

3. **Image VM** — `Draw.App/ViewModels/ImageNodeViewModel.cs` (new, `: NodeViewModelBase, IDisposable`):
   decode `Bitmap` from `Data`, `BoundaryKind = Rectangle`, dispose the bitmap. Add
   `virtual bool LocksAspectRatio => false` to `NodeViewModelBase`, override `=> true` here.

4. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: inject `IClipboardService` (thread
   through `DiagramDocumentViewModelFactory`); add `ViewportWidth/Height` + `ViewportCenterWorld()`,
   `HasNodeSelection`, `SelectNodes(...)`; `CopySelectionAsync`/`CutSelectionAsync`/`PasteAsync` /
   `DuplicateSelection` (shared clone+remap helper, one undo step); `AddImageNode(centre, data, format)`;
   handle `ImageNode` in `CreateNodeViewModel`; dispose `IDisposable` node VMs in `RebuildNodes`/`Dispose`.

5. **Shell VM** — `Draw.App/ViewModels/ShellViewModel.cs`: `CopyCommand`/`CutCommand`/`PasteCommand`
   (`AsyncRelayCommand`), `DuplicateCommand`/`InsertImageCommand`, delegating to `ActiveDocument`;
   CanExecute (`HasNodeSelection` / `HasActiveDocument`); notify in selection-changed + document-commands.

6. **View** — `Draw.App/Views/DiagramView.axaml`(+`.axaml.cs`): `ImageNodeViewModel` `DataTemplate`
   (`<Image>` + selection rect); handle `Ctrl+C/X/V/D` in `OnKeyDown` (after the `TextBox` guard); enable
   drag-&-drop (`DragDrop.DropEvent`) to `AddImageNode` at the drop point; aspect-lock branch in
   `ResizeBounds`; push viewport size to the VM (`SizeChanged` + `DataContextChanged`).

7. **Menu/ribbon** — `Draw.App/Views/MainWindow.axaml`: Edit menu Cut/Copy/Paste/Duplicate (+ display
   gestures) and Insert Image; matching Home→Edit and Insert ribbon buttons.

8. **Roadmap** — `documentation/roadmap.md`: new cross-cutting section.

9. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); manual verification on Windows (no GUI
   under WSL2) — see the design's interaction notes.

## Status

- [x] 1 Image model · [x] 2 Services · [x] 3 Image VM · [x] 4 Document VM · [x] 5 Shell VM ·
  [x] 6 View · [x] 7 Menu/ribbon · [x] 8 Roadmap · [x] 9 Build (clean)

Implemented on `feature/copy-paste-and-images`; `dotnet build Draw.slnx` is clean (0 warnings,
0 errors; nullable-as-error). Clipboard uses the Avalonia 12 typed `DataFormat`/`DataTransfer` API and
drag-drop the new `DragEventArgs.DataTransfer`. Pending: manual verification on Windows (no GUI under
WSL2) — see the checklist in the design's §5 interaction notes, in particular the platform-dependent
external-app bitmap interop.
