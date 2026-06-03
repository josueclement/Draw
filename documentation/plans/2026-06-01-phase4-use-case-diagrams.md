# Phase 4 — Use-Case Diagrams Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add UML use-case diagrams — actor, use-case and system-boundary nodes plus the use-case relationships — reusing the `NodeViewModelBase` foundation and connector machinery from Phases 1–3.

**Architecture:** Three new `NodeBase` subtypes (`ActorNode`/`UseCaseNode`/`SystemBoundaryNode`) join the polymorphic document; three VMs derive `NodeViewModelBase`; inline label editing is generalized onto the base via `HasInlineLabel`/`Label`; the system boundary is a visual-only box rendered behind (low z-index + inserted at the front of the node collection). Include/Extend (already rendered since Phase 2) are surfaced in the toolbox.

**Tech Stack:** .NET 10, C# 13, Avalonia 12, CommunityToolkit.Mvvm, System.Text.Json (polymorphic), xUnit v3 on Microsoft.Testing.Platform.

---

## Conventions for the executor

- **Branch:** work on `feature/phase4-use-case-diagrams` (already created; the spec is committed there). Git identity is already configured locally.
- **Build:** `dotnet build Draw.slnx`
- **Test a project:** `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj` (swap project). Whole suite: `dotnet test --solution Draw.slnx`. The repo's `global.json` opts into Microsoft.Testing.Platform.
- **TDD:** strict red→green→commit for model + view-model logic. AXAML/pointer-code-behind tasks have **no UI test harness** — verify with `dotnet build` (compiled XAML validates bindings) + a manual run; say "builds; manually verified", never "tested".
- **Line endings — CRITICAL:** this Windows/WSL checkout has a CRLF artifact (~90+ files show "modified"; committed blobs are LF; there is no `.gitattributes`). Write source as **LF**; before committing a file you touched, `sed -i 's/\r$//' <file>` it so the diff is only your real change. **Stage only the specific files you changed** (`git add <files>`), never `git add -A`/`git add .`.
- **Git scope — CRITICAL:** stay on `feature/phase4-use-case-diagrams`. Do NOT checkout/switch/create branches, merge, rebase, reset, cherry-pick, push, or touch `main`. Only `git add <files>` + `git commit`.
- **Commit trailer** (end every commit body):

```
Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

- **Running the app (Linux/WSL, manual tasks only):** `sudo apt-get install -y libfontconfig1 libice6 libsm6` then `dotnet run --project src/Draw.App/Draw.App.csproj`.

---

# Phase A — Model layer (`Draw.Model`)

## Task 1: Three use-case node types + polymorphic registration

**Files:**
- Create: `src/Draw.Model/Nodes/ActorNode.cs`
- Create: `src/Draw.Model/Nodes/UseCaseNode.cs`
- Create: `src/Draw.Model/Nodes/SystemBoundaryNode.cs`
- Modify: `src/Draw.Model/Nodes/NodeBase.cs:15` (add three `[JsonDerivedType]`)
- Test: `tests/Draw.Model.Tests/UseCaseNodesTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.Model.Tests/UseCaseNodesTests.cs`:
```csharp
using System;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Model.Tests;

public class UseCaseNodesTests
{
    [Fact]
    public void Clone_Actor_CopiesNameAndBase()
    {
        ActorNode node = new() { Id = Guid.NewGuid(), Name = "Customer", Bounds = new Rect2D(1, 2, 48, 84) };

        ActorNode clone = Assert.IsType<ActorNode>(node.Clone());
        clone.Name = "Admin";

        Assert.Equal(node.Id, clone.Id);
        Assert.Equal("Customer", node.Name);
        Assert.Equal("Admin", clone.Name);
        Assert.Equal(new Rect2D(1, 2, 48, 84), clone.Bounds);
    }

    [Fact]
    public void RoundTrip_PreservesAllThreeNodeKinds()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new() { DiagramType = DiagramType.UseCase };
        doc.Nodes.Add(new ActorNode { Name = "Customer", Bounds = new Rect2D(0, 0, 48, 84) });
        doc.Nodes.Add(new UseCaseNode { Text = "Place order", Bounds = new Rect2D(80, 0, 130, 72) });
        doc.Nodes.Add(new SystemBoundaryNode { Title = "Shop", Bounds = new Rect2D(0, 0, 320, 220) });

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        ActorNode actor = Assert.IsType<ActorNode>(back.Nodes[0]);
        Assert.Equal("Customer", actor.Name);
        UseCaseNode useCase = Assert.IsType<UseCaseNode>(back.Nodes[1]);
        Assert.Equal("Place order", useCase.Text);
        SystemBoundaryNode boundary = Assert.IsType<SystemBoundaryNode>(back.Nodes[2]);
        Assert.Equal("Shop", boundary.Title);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: FAIL — the three types don't exist.

- [ ] **Step 3: Create the three node types**

`src/Draw.Model/Nodes/ActorNode.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>A UML actor — a stick figure with a name label.</summary>
public sealed class ActorNode : NodeBase
{
    public string Name { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        ActorNode copy = new() { Name = Name };
        CopyBaseTo(copy);
        return copy;
    }
}
```

`src/Draw.Model/Nodes/UseCaseNode.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>A UML use case — an ellipse with centered text.</summary>
public sealed class UseCaseNode : NodeBase
{
    public string Text { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        UseCaseNode copy = new() { Text = Text };
        CopyBaseTo(copy);
        return copy;
    }
}
```

`src/Draw.Model/Nodes/SystemBoundaryNode.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>A UML system boundary — a titled rectangle drawn behind the use cases it groups.</summary>
public sealed class SystemBoundaryNode : NodeBase
{
    public string Title { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        SystemBoundaryNode copy = new() { Title = Title };
        CopyBaseTo(copy);
        return copy;
    }
}
```

- [ ] **Step 4: Register the derived types**

Modify `src/Draw.Model/Nodes/NodeBase.cs` — add three attributes after the existing `class` registration (line 15):
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ShapeNode), "shape")]
[JsonDerivedType(typeof(ClassNode), "class")]
[JsonDerivedType(typeof(ActorNode), "actor")]
[JsonDerivedType(typeof(UseCaseNode), "useCase")]
[JsonDerivedType(typeof(SystemBoundaryNode), "systemBoundary")]
public abstract class NodeBase
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit** (normalize LF first)

```bash
sed -i 's/\r$//' src/Draw.Model/Nodes/NodeBase.cs
git add src/Draw.Model/Nodes/ActorNode.cs src/Draw.Model/Nodes/UseCaseNode.cs src/Draw.Model/Nodes/SystemBoundaryNode.cs src/Draw.Model/Nodes/NodeBase.cs tests/Draw.Model.Tests/UseCaseNodesTests.cs
git commit -m "Add actor, use-case and system-boundary node types

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase B — Inline-label generalization (`Draw.App`)

## Task 2: `HasInlineLabel` / `Label` on `NodeViewModelBase`; `ShapeNodeViewModel` overrides

**Files:**
- Modify: `src/Draw.App/ViewModels/NodeViewModelBase.cs`
- Modify: `src/Draw.App/ViewModels/ShapeNodeViewModel.cs`

Mechanical, no behavior change; the existing suite is the safety net.

- [ ] **Step 1: Add the two virtual members to the base**

In `NodeViewModelBase.cs`, add after the `BoundaryKind` property (around line 23):
```csharp
    /// <summary>True when this node has a single editable text label (inline + inspector).</summary>
    public virtual bool HasInlineLabel => false;

    /// <summary>The node's single editable label; overridden by label-bearing node kinds.</summary>
    public virtual string Label
    {
        get => string.Empty;
        set { }
    }
```

- [ ] **Step 2: Override them in `ShapeNodeViewModel`**

In `ShapeNodeViewModel.cs`, add the overrides and have `Text` re-raise `Label`:
```csharp
    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Text;
        set => Text = value;
    }
```
And in the existing `Text` setter, after `OnPropertyChanged();`, add `OnPropertyChanged(nameof(Label));`:
```csharp
    public string Text
    {
        get => _model.Text;
        set
        {
            if (!string.Equals(_model.Text, value, System.StringComparison.Ordinal))
            {
                _model.Text = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }
```

- [ ] **Step 3: Build + run the full suite**

Run: `dotnet build Draw.slnx && dotnet test --solution Draw.slnx`
Expected: PASS (no behavior change).

- [ ] **Step 4: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/ViewModels/NodeViewModelBase.cs src/Draw.App/ViewModels/ShapeNodeViewModel.cs
git add src/Draw.App/ViewModels/NodeViewModelBase.cs src/Draw.App/ViewModels/ShapeNodeViewModel.cs
git commit -m "Add inline-label abstraction to NodeViewModelBase

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase C — Actor geometry + node view-models (`Draw.App`)

## Task 3: `ActorGeometry` builder

**Files:**
- Create: `src/Draw.App/Rendering/ActorGeometry.cs`

`ActorGeometry` is rendering-layer code (it builds an Avalonia `Geometry` using `StreamGeometry`/`EllipseGeometry`), exactly like `ShapeGeometryBuilder`. The repo does NOT unit-test geometry builders: the test harness has no Avalonia render backend, so `StreamGeometry.Open()` and `Geometry.Bounds` throw `Unable to locate 'IPlatformRenderInterface'` when run headless, and no existing test touches `.Geometry`/`.Bounds`. So this task is **build-verified** (the App compiles it) and exercised by the manual run in Task 9 — there is no unit test. Do NOT add a unit test for it and do NOT add Avalonia.Headless.

- [ ] **Step 1: Create `ActorGeometry`**

`src/Draw.App/Rendering/ActorGeometry.cs`:
```csharp
using System;
using Avalonia;
using Avalonia.Media;

namespace Draw.App.Rendering;

/// <summary>Builds the stick-figure outline for an actor node, scaled to its bounds and
/// reserving a bottom strip for the name label.</summary>
public static class ActorGeometry
{
    public static Geometry Build(double width, double height)
    {
        width = Math.Max(1d, width);
        height = Math.Max(1d, height);

        double labelStrip = Math.Min(18d, height * 0.25);
        double figureHeight = Math.Max(1d, height - labelStrip);
        double cx = width / 2d;
        double headRadius = Math.Min(width, figureHeight) * 0.18d;
        double neckY = headRadius * 2d;
        double hipY = figureHeight * 0.62d;
        double shoulderY = neckY + ((hipY - neckY) * 0.25d);
        double armHalf = width * 0.30d;
        double legSpread = width * 0.28d;

        GeometryGroup group = new();
        group.Children.Add(new EllipseGeometry(new Rect(cx - headRadius, 0, headRadius * 2d, headRadius * 2d)));

        StreamGeometry lines = new();
        using (StreamGeometryContext ctx = lines.Open())
        {
            ctx.BeginFigure(new Point(cx, neckY), isFilled: false);
            ctx.LineTo(new Point(cx, hipY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(cx - armHalf, shoulderY), isFilled: false);
            ctx.LineTo(new Point(cx + armHalf, shoulderY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(cx - legSpread, figureHeight), isFilled: false);
            ctx.LineTo(new Point(cx, hipY));
            ctx.LineTo(new Point(cx + legSpread, figureHeight));
            ctx.EndFigure(false);
        }

        group.Children.Add(lines);
        return group;
    }
}
```

- [ ] **Step 2: Build (the App compiles the new builder)**

Run: `dotnet build Draw.slnx`
Expected: Build succeeded, 0 errors (the 5 pre-existing AVLN5001 Watermark warnings are unrelated).

- [ ] **Step 3: Commit** (ActorGeometry.cs only — no test file)

```bash
sed -i 's/\r$//' src/Draw.App/Rendering/ActorGeometry.cs
git add src/Draw.App/Rendering/ActorGeometry.cs
git commit -m "Add actor stick-figure geometry builder

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: The three node view-models

**Files:**
- Create: `src/Draw.App/ViewModels/ActorNodeViewModel.cs`
- Create: `src/Draw.App/ViewModels/UseCaseNodeViewModel.cs`
- Create: `src/Draw.App/ViewModels/SystemBoundaryNodeViewModel.cs`
- Test: `tests/Draw.App.Tests/UseCaseNodeViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.App.Tests/UseCaseNodeViewModelTests.cs`:
```csharp
using Draw.App.ViewModels;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Styling;
using Xunit;

namespace Draw.App.Tests;

public class UseCaseNodeViewModelTests
{
    [Fact]
    public void Actor_BoundaryRectangle_LabelMapsToName_HasInlineLabel()
    {
        ActorNode model = new() { Name = "Customer", Bounds = new Rect2D(0, 0, 48, 84), Style = ShapeStyle.CreateDefault() };
        ActorNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Customer", vm.Label);
        vm.Label = "Admin";
        Assert.Equal("Admin", model.Name);
    }

    [Fact]
    public void UseCase_BoundaryEllipse_LabelMapsToText()
    {
        UseCaseNode model = new() { Text = "Place order", Bounds = new Rect2D(0, 0, 130, 72), Style = ShapeStyle.CreateDefault() };
        UseCaseNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Ellipse, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Place order", vm.Label);
        vm.Label = "Cancel order";
        Assert.Equal("Cancel order", model.Text);
    }

    [Fact]
    public void SystemBoundary_BoundaryRectangle_LabelMapsToTitle()
    {
        SystemBoundaryNode model = new() { Title = "Shop", Bounds = new Rect2D(0, 0, 320, 220), Style = ShapeStyle.CreateDefault() };
        SystemBoundaryNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Shop", vm.Label);
        vm.Label = "Store";
        Assert.Equal("Store", model.Title);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — the three VMs don't exist.

- [ ] **Step 3: Implement the three VMs**

`src/Draw.App/ViewModels/ActorNodeViewModel.cs`:
```csharp
using System;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over an <see cref="ActorNode"/>: stick figure + name label.</summary>
public sealed class ActorNodeViewModel : NodeViewModelBase
{
    private readonly ActorNode _model;

    public ActorNodeViewModel(ActorNode model)
        : base(model)
    {
        _model = model;
    }

    public new ActorNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Name;
        set => Name = value;
    }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _model.Name = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public Geometry Geometry => ActorGeometry.Build(Model.Bounds.Width, Model.Bounds.Height);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
```

`src/Draw.App/ViewModels/UseCaseNodeViewModel.cs`:
```csharp
using System;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="UseCaseNode"/>: ellipse + centered text.</summary>
public sealed class UseCaseNodeViewModel : NodeViewModelBase
{
    private readonly UseCaseNode _model;

    public UseCaseNodeViewModel(UseCaseNode model)
        : base(model)
    {
        _model = model;
    }

    public new UseCaseNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Ellipse;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Text;
        set => Text = value;
    }

    public string Text
    {
        get => _model.Text;
        set
        {
            if (!string.Equals(_model.Text, value, StringComparison.Ordinal))
            {
                _model.Text = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public Geometry Geometry
        => ShapeGeometryBuilder.Build(ShapeKind.Ellipse, Model.Bounds.Width, Model.Bounds.Height, 0d);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
```

`src/Draw.App/ViewModels/SystemBoundaryNodeViewModel.cs`:
```csharp
using System;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="SystemBoundaryNode"/>: a titled box drawn behind.</summary>
public sealed class SystemBoundaryNodeViewModel : NodeViewModelBase
{
    private readonly SystemBoundaryNode _model;

    public SystemBoundaryNodeViewModel(SystemBoundaryNode model)
        : base(model)
    {
        _model = model;
    }

    public new SystemBoundaryNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Title;
        set => Title = value;
    }

    public string Title
    {
        get => _model.Title;
        set
        {
            if (!string.Equals(_model.Title, value, StringComparison.Ordinal))
            {
                _model.Title = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/ViewModels/ActorNodeViewModel.cs src/Draw.App/ViewModels/UseCaseNodeViewModel.cs src/Draw.App/ViewModels/SystemBoundaryNodeViewModel.cs
git add src/Draw.App/ViewModels/ActorNodeViewModel.cs src/Draw.App/ViewModels/UseCaseNodeViewModel.cs src/Draw.App/ViewModels/SystemBoundaryNodeViewModel.cs tests/Draw.App.Tests/UseCaseNodeViewModelTests.cs
git commit -m "Add actor, use-case and system-boundary view-models

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase D — Document creation (`Draw.App`)

## Task 5: `AddUseCaseNode` + `CreateNodeViewModel` arms (boundary behind)

**Files:**
- Modify: `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`
- Test: `tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs`

The `UseCaseNodeKind` enum lives in the toolbox file (Task 6), but `AddUseCaseNode` needs it. To keep Task 5 self-contained and compiling, **define the enum here in this task** (in its own file) and reuse it in Task 6.

- [ ] **Step 1: Write the failing tests** (append to `DiagramDocumentViewModelTests`)

```csharp
    [Fact]
    public void AddUseCaseNode_Actor_AddsSelectedActor_AndMarksModified()
    {
        DiagramDocumentViewModel doc = CreateDocument();

        NodeViewModelBase node = doc.AddUseCaseNode(UseCaseNodeKind.Actor, new Point2D(100, 100));

        Assert.IsType<ActorNodeViewModel>(node);
        Assert.Same(node, Assert.Single(doc.Nodes));
        Assert.True(node.IsSelected);
        Assert.True(doc.IsModified);
    }

    [Fact]
    public void AddUseCaseNode_SystemBoundary_GoesBehind_WithLowestZIndex()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));

        NodeViewModelBase boundary = doc.AddUseCaseNode(UseCaseNodeKind.SystemBoundary, new Point2D(150, 150));

        Assert.IsType<SystemBoundaryNodeViewModel>(boundary);
        Assert.Same(boundary, doc.Nodes[0]); // inserted at front -> renders behind
        Assert.True(boundary.Model.ZIndex < doc.Nodes[1].Model.ZIndex);
    }

    [Fact]
    public void AddUseCaseNode_SurvivesUndoRedo_AsCorrectType()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.AddUseCaseNode(UseCaseNodeKind.UseCase, new Point2D(200, 150));

        doc.Undo();
        Assert.Empty(doc.Nodes);

        doc.Redo();
        Assert.IsType<UseCaseNodeViewModel>(Assert.Single(doc.Nodes));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `UseCaseNodeKind`/`AddUseCaseNode` not defined.

- [ ] **Step 3: Create the kind enum**

`src/Draw.App/ViewModels/UseCaseNodeKind.cs`:
```csharp
namespace Draw.App.ViewModels;

/// <summary>Which use-case node a toolbox tool creates (toolbox/creation dispatch only).</summary>
public enum UseCaseNodeKind
{
    Actor,
    UseCase,
    SystemBoundary,
}
```

- [ ] **Step 4: Add `AddUseCaseNode`, the arms, and `LowestZIndex`**

In `DiagramDocumentViewModel.cs`, add after `DefaultClassName` (around line 205):
```csharp
    private const double ActorDefaultWidth = 48d;
    private const double ActorDefaultHeight = 84d;
    private const double UseCaseDefaultWidth = 130d;
    private const double UseCaseDefaultHeight = 72d;
    private const double BoundaryDefaultWidth = 320d;
    private const double BoundaryDefaultHeight = 220d;

    public NodeViewModelBase AddUseCaseNode(UseCaseNodeKind kind, Point2D center)
    {
        CaptureUndo();

        (double w, double h) = kind switch
        {
            UseCaseNodeKind.Actor => (ActorDefaultWidth, ActorDefaultHeight),
            UseCaseNodeKind.SystemBoundary => (BoundaryDefaultWidth, BoundaryDefaultHeight),
            _ => (UseCaseDefaultWidth, UseCaseDefaultHeight),
        };

        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        NodeBase node = kind switch
        {
            UseCaseNodeKind.Actor => new ActorNode
            {
                Name = "Actor", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = NextZIndex(),
            },
            UseCaseNodeKind.SystemBoundary => new SystemBoundaryNode
            {
                Title = "System", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = LowestZIndex() - 1,
            },
            _ => new UseCaseNode
            {
                Text = "Use case", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = NextZIndex(),
            },
        };

        _document.Nodes.Add(node);
        NodeViewModelBase vm = CreateNodeViewModel(node);

        // A boundary renders behind everything: it gets the lowest z-index AND goes to the
        // front of the (insertion-ordered) collection so it draws first even before a rebuild.
        if (node is SystemBoundaryNode)
        {
            Nodes.Insert(0, vm);
        }
        else
        {
            Nodes.Add(vm);
        }

        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    private int LowestZIndex() => _document.Nodes.Count == 0 ? 0 : _document.Nodes.Min(n => n.ZIndex);
```

Extend `CreateNodeViewModel` (currently lines 463-468) with three arms — place them before the `ShapeNode` arm:
```csharp
    private NodeViewModelBase CreateNodeViewModel(NodeBase node) => node switch
    {
        ClassNode @class => new ClassNodeViewModel(@class, this),
        ActorNode actor => new ActorNodeViewModel(actor),
        UseCaseNode useCase => new UseCaseNodeViewModel(useCase),
        SystemBoundaryNode boundary => new SystemBoundaryNodeViewModel(boundary),
        ShapeNode shape => new ShapeNodeViewModel(shape),
        _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}"),
    };
```

Note: `LowestZIndex()` is computed before the node is added to `_document.Nodes`, so it reflects the existing nodes. After undo/redo/open, `RebuildNodes` orders by `ZIndex`, so the low z-index keeps the boundary behind.

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/ViewModels/DiagramDocumentViewModel.cs
git add src/Draw.App/ViewModels/UseCaseNodeKind.cs src/Draw.App/ViewModels/DiagramDocumentViewModel.cs tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs
git commit -m "Add use-case node creation with boundary drawn behind

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase E — Toolbox (`Draw.App`)

## Task 6: Use-case tools + Include/Extend in the palette

**Files:**
- Modify: `src/Draw.App/ViewModels/ToolboxViewModel.cs`
- Test: `tests/Draw.App.Tests/ToolboxViewModelTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `ToolboxViewModelTests`)

```csharp
    [Fact]
    public void HasUseCaseTools_ForEachKind()
    {
        ToolboxViewModel toolbox = new();
        Assert.Contains(toolbox.UseCaseNodes, t => t.Kind == UseCaseNodeKind.Actor);
        Assert.Contains(toolbox.UseCaseNodes, t => t.Kind == UseCaseNodeKind.UseCase);
        Assert.Contains(toolbox.UseCaseNodes, t => t.Kind == UseCaseNodeKind.SystemBoundary);
    }

    [Fact]
    public void Connectors_IncludeIncludeAndExtend()
    {
        ToolboxViewModel toolbox = new();
        Assert.Contains(toolbox.Connectors, c => c.Kind == RelationshipKind.Include);
        Assert.Contains(toolbox.Connectors, c => c.Kind == RelationshipKind.Extend);
    }

    [Fact]
    public void SelectingUseCaseNode_ClearsOthers_AndSetsMode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedShape = toolbox.Shapes.First();
        toolbox.SelectedClassNode = toolbox.ClassNodes.First();

        toolbox.SelectedUseCaseNode = toolbox.UseCaseNodes.First();

        Assert.Null(toolbox.SelectedShape);
        Assert.Null(toolbox.SelectedConnector);
        Assert.Null(toolbox.SelectedClassNode);
        Assert.True(toolbox.IsUseCaseNodeMode);
        Assert.False(toolbox.IsSelectTool);
    }

    [Fact]
    public void SelectingShape_ClearsUseCaseNode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedUseCaseNode = toolbox.UseCaseNodes.First();

        toolbox.SelectedShape = toolbox.Shapes.First();

        Assert.Null(toolbox.SelectedUseCaseNode);
        Assert.False(toolbox.IsUseCaseNodeMode);
    }
```
(`RelationshipKind` is already imported in this test file via `using Draw.Model.Nodes;`? Add `using Draw.Model.Connectors;` if missing.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `UseCaseNodes`/`SelectedUseCaseNode`/`IsUseCaseNodeMode` not defined; Include/Extend absent.

- [ ] **Step 3: Implement**

In `ToolboxViewModel.cs`:
- Add the record near the others (after `ClassNodeToolItem`):
```csharp
/// <summary>A selectable use-case-diagram node entry in the toolbox palette.</summary>
public sealed record UseCaseToolItem(string Name, UseCaseNodeKind Kind);
```
- Add `Include` and `Extend` to the `Connectors` collection initializer (after the `Dependency` entry):
```csharp
        new ConnectorToolItem("Include", RelationshipKind.Include),
        new ConnectorToolItem("Extend", RelationshipKind.Extend),
```
- Add the use-case collection (after `ClassNodes`):
```csharp
    public ObservableCollection<UseCaseToolItem> UseCaseNodes { get; } = new()
    {
        new UseCaseToolItem("Actor", UseCaseNodeKind.Actor),
        new UseCaseToolItem("Use case", UseCaseNodeKind.UseCase),
        new UseCaseToolItem("System boundary", UseCaseNodeKind.SystemBoundary),
    };
```
- Add `SelectedUseCaseNode` (after `SelectedClassNode`):
```csharp
    public UseCaseToolItem? SelectedUseCaseNode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedShape = null;
                    SelectedConnector = null;
                    SelectedClassNode = null;
                }

                RaiseModes();
            }
        }
    }
```
- In the `SelectedShape`, `SelectedConnector` and `SelectedClassNode` setters, add `SelectedUseCaseNode = null;` inside each `if (value is not null)` block.
- Update the mode flags:
```csharp
    public bool IsSelectTool => SelectedShape is null && SelectedConnector is null
        && SelectedClassNode is null && SelectedUseCaseNode is null;

    public bool IsUseCaseNodeMode => SelectedUseCaseNode is not null;
```
- In `ActivateSelectTool`, add `SelectedUseCaseNode = null;`.
- In `RaiseModes`, add `OnPropertyChanged(nameof(IsUseCaseNodeMode));`.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/ViewModels/ToolboxViewModel.cs
git add src/Draw.App/ViewModels/ToolboxViewModel.cs tests/Draw.App.Tests/ToolboxViewModelTests.cs
git commit -m "Add use-case tools and include/extend to the toolbox

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase F — Inspector (`Draw.App`)

## Task 7: Generalize the label field to all label-bearing nodes

**Files:**
- Modify: `src/Draw.App/ViewModels/InspectorViewModel.cs`
- Test: `tests/Draw.App.Tests/InspectorViewModelTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `InspectorViewModelTests`)

```csharp
    [Fact]
    public void SelectingActor_ReportsLabelNode_AndLoadsName()
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.UseCase),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        ActorNodeViewModel actor = (ActorNodeViewModel)doc.AddUseCaseNode(UseCaseNodeKind.Actor, new Point2D(100, 100));
        actor.Name = "Customer";

        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        Assert.True(inspector.IsLabelNodeSelected);
        Assert.True(inspector.IsNodeSelected);     // shared style applies
        Assert.False(inspector.IsShapeSelected);
        Assert.False(inspector.HasNoSelection);
        Assert.Equal("Customer", inspector.Text);
    }

    [Fact]
    public void SettingText_AppliesToSelectedUseCaseNode()
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.UseCase),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        UseCaseNodeViewModel useCase = (UseCaseNodeViewModel)doc.AddUseCaseNode(UseCaseNodeKind.UseCase, new Point2D(100, 100));

        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);
        inspector.Text = "Place order";

        Assert.Equal("Place order", useCase.Model.Text);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `IsLabelNodeSelected` not defined; `Text` apply only hits shapes.

- [ ] **Step 3: Implement**

In `InspectorViewModel.cs`:
- Add the new flag (after `IsClassNodeSelected`):
```csharp
    public bool IsLabelNodeSelected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(IsNodeSelected));
            }
        }
    }
```
- Replace `IsNodeSelected` and `HasNoSelection`:
```csharp
    public bool IsNodeSelected => IsLabelNodeSelected || IsClassNodeSelected;

    public bool HasNoSelection => !IsLabelNodeSelected && !IsConnectorSelected && !IsClassNodeSelected;
```
- In `LoadFromSelection`, set the new flag and load `Text` from `node.Label`. Replace the assignments inside the method:
  - After `IsClassNodeSelected = klass is not null;` add:
```csharp
            IsLabelNodeSelected = node is { HasInlineLabel: true };
```
  - In the `else if (node is not null)` block, replace the shape-specific `Text` load
    (`if (shape is not null) { Text = shape.Model.Text; }`) with:
```csharp
                if (node.HasInlineLabel)
                {
                    Text = node.Label;
                }
```
- Replace `ApplyText` to operate on all selected label nodes:
```csharp
    private void ApplyText()
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<NodeViewModelBase> selected = _target.SelectedNodes.Where(n => n.HasInlineLabel).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (NodeViewModelBase node in selected)
        {
            node.Label = Text;
        }

        _target.MarkModified();
    }
```

(`IsShapeSelected` stays as-is — still set from `shape is not null` and asserted by existing tests, but it no longer drives `IsNodeSelected`/`HasNoSelection`.)

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS (including existing shape/class inspector tests).

- [ ] **Step 5: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/ViewModels/InspectorViewModel.cs
git add src/Draw.App/ViewModels/InspectorViewModel.cs tests/Draw.App.Tests/InspectorViewModelTests.cs
git commit -m "Generalize inspector label field to all label-bearing nodes

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase G — Placement, inline edit, rendering (`Draw.App`)

## Task 8: Code-behind — use-case placement; generalize inline editing

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml.cs`

Build-verified (logic; no UI harness).

- [ ] **Step 1: Add use-case placement to `OnPointerPressed`**

After the class-node placement block (currently lines 232-238), add:
```csharp
        // Use-case-node placement.
        if (toolbox?.SelectedUseCaseNode is { } useCaseTool)
        {
            _vm.AddUseCaseNode(useCaseTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }
```

- [ ] **Step 2: Generalize `OnDoubleTapped` to any inline-label node**

Replace the body (currently lines 400-413):
```csharp
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        Point world = ScreenToWorld(e.GetPosition(Viewport));
        if (HitTestNode(world) is { HasInlineLabel: true } node)
        {
            _vm.CaptureUndo();
            node.IsEditing = true;
        }
    }
```

- [ ] **Step 3: Generalize `EndEditing`**

Replace the body (currently lines 477-499):
```csharp
    private void EndEditing()
    {
        if (_vm is null)
        {
            return;
        }

        bool labelEdited = false;
        foreach (NodeViewModelBase node in _vm.Nodes.Where(n => n.IsEditing))
        {
            node.IsEditing = false;
            labelEdited = true;
        }

        bool committed = false;
        foreach (ClassNodeViewModel klass in _vm.Nodes.OfType<ClassNodeViewModel>())
        {
            committed |= klass.CommitPendingEdits();
        }

        if (labelEdited || committed)
        {
            _vm.MarkModified();
        }
    }
```

- [ ] **Step 4: Build + run the full suite**

Run: `dotnet build Draw.slnx && dotnet test --solution Draw.slnx`
Expected: PASS (the suite covers the VM logic; this task is wiring).

- [ ] **Step 5: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/Views/DiagramView.axaml.cs
git add src/Draw.App/Views/DiagramView.axaml.cs
git commit -m "Place use-case nodes and generalize inline label editing

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: AXAML — node templates, use-case palette, broadened text field

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml`
- Modify: `src/Draw.App/Views/MainWindow.axaml`

Build + manual verification (no UI harness).

- [ ] **Step 1: Add three node DataTemplates**

In `DiagramView.axaml`, inside `<ItemsControl.DataTemplates>` (after the `ClassNodeViewModel` template, before `</ItemsControl.DataTemplates>` at line 165), add:
```xml
                    <DataTemplate DataType="vm:ActorNodeViewModel">
                        <Panel Width="{Binding Width}" Height="{Binding Height}">
                            <Path Data="{Binding Geometry}"
                                  Stroke="{Binding Stroke}"
                                  StrokeThickness="{Binding StrokeThickness}" />
                            <TextBlock Text="{Binding Name}"
                                       Foreground="{Binding Foreground}"
                                       FontFamily="{Binding FontFamily}"
                                       FontSize="{Binding FontSize}"
                                       TextAlignment="Center"
                                       VerticalAlignment="Bottom"
                                       HorizontalAlignment="Stretch"
                                       IsVisible="{Binding !IsEditing}" />
                            <TextBox Text="{Binding Label, Mode=TwoWay}"
                                     FontSize="{Binding FontSize}"
                                     VerticalAlignment="Bottom"
                                     Background="Transparent"
                                     IsVisible="{Binding IsEditing}" />
                            <Rectangle Stroke="#3D7EFF" StrokeThickness="1.5"
                                       StrokeDashArray="3,2" Fill="Transparent"
                                       IsHitTestVisible="False"
                                       IsVisible="{Binding IsSelected}" />
                        </Panel>
                    </DataTemplate>

                    <DataTemplate DataType="vm:UseCaseNodeViewModel">
                        <Panel Width="{Binding Width}" Height="{Binding Height}">
                            <Path Data="{Binding Geometry}"
                                  Fill="{Binding Fill}"
                                  Stroke="{Binding Stroke}"
                                  StrokeThickness="{Binding StrokeThickness}" />
                            <TextBlock Text="{Binding Text}"
                                       Foreground="{Binding Foreground}"
                                       FontFamily="{Binding FontFamily}"
                                       FontSize="{Binding FontSize}"
                                       FontWeight="{Binding FontWeight}"
                                       FontStyle="{Binding FontStyle}"
                                       TextAlignment="Center"
                                       TextWrapping="Wrap"
                                       VerticalAlignment="Center"
                                       Margin="10"
                                       IsVisible="{Binding !IsEditing}" />
                            <TextBox Text="{Binding Label, Mode=TwoWay}"
                                     FontSize="{Binding FontSize}"
                                     AcceptsReturn="True" TextWrapping="Wrap"
                                     VerticalAlignment="Center" Background="Transparent"
                                     IsVisible="{Binding IsEditing}" />
                            <Rectangle Stroke="#3D7EFF" StrokeThickness="1.5"
                                       StrokeDashArray="3,2" Fill="Transparent"
                                       IsHitTestVisible="False"
                                       IsVisible="{Binding IsSelected}" />
                        </Panel>
                    </DataTemplate>

                    <DataTemplate DataType="vm:SystemBoundaryNodeViewModel">
                        <Panel Width="{Binding Width}" Height="{Binding Height}">
                            <Border Background="Transparent"
                                    BorderBrush="{Binding Stroke}"
                                    BorderThickness="{Binding StrokeThickness}" />
                            <TextBlock Text="{Binding Title}"
                                       Foreground="{Binding Foreground}"
                                       FontFamily="{Binding FontFamily}"
                                       FontSize="{Binding FontSize}"
                                       FontWeight="Bold"
                                       Margin="6,4"
                                       HorizontalAlignment="Left"
                                       VerticalAlignment="Top"
                                       IsVisible="{Binding !IsEditing}" />
                            <TextBox Text="{Binding Label, Mode=TwoWay}"
                                     FontSize="{Binding FontSize}"
                                     Margin="4,2"
                                     HorizontalAlignment="Left" VerticalAlignment="Top"
                                     Background="Transparent"
                                     IsVisible="{Binding IsEditing}" />
                            <Rectangle Stroke="#3D7EFF" StrokeThickness="1.5"
                                       StrokeDashArray="3,2" Fill="Transparent"
                                       IsHitTestVisible="False"
                                       IsVisible="{Binding IsSelected}" />
                        </Panel>
                    </DataTemplate>
```

- [ ] **Step 2: Add the use-case palette to `MainWindow.axaml`**

After the "Class diagram" `ListBox` (closes at line 118), before `</StackPanel>` (line 119), add:
```xml
                        <TextBlock Text="Use case" Opacity="0.7" Margin="0,6,0,0" />
                        <ListBox ItemsSource="{Binding Toolbox.UseCaseNodes}"
                                 SelectedItem="{Binding Toolbox.SelectedUseCaseNode, Mode=TwoWay}">
                            <ListBox.ItemTemplate>
                                <DataTemplate x:DataType="vm:UseCaseToolItem">
                                    <TextBlock Text="{Binding Name}" />
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>
```

- [ ] **Step 3: Broaden the inspector text field**

In `MainWindow.axaml`, change the "Text (shapes only)" panel visibility (line 154) from
`IsVisible="{Binding Inspector.IsShapeSelected}"` to:
```xml
                        <StackPanel Spacing="10" IsVisible="{Binding Inspector.IsLabelNodeSelected}">
```
(Optionally relabel the `TextBlock Text="Text"` to `"Label"`; not required.)

- [ ] **Step 4: Build**

Run: `dotnet build Draw.slnx`
Expected: succeeds (compiled XAML validates the new templates, the `UseCaseToolItem` palette binding, and `IsLabelNodeSelected`).

- [ ] **Step 5: Manual verification**

Run: `dotnet run --project src/Draw.App/Draw.App.csproj`
Confirm: the Use-case palette places an actor (stick figure + editable name below), a use-case (ellipse + centered text), and a system boundary (titled box that renders **behind** use-cases and is still selectable on its empty interior). Double-tap edits each label. Draw association / include / extend / generalization between them. Fill/stroke/font apply. Save/open and undo/redo round-trip. Each is undoable.

- [ ] **Step 6: Commit**

```bash
sed -i 's/\r$//' src/Draw.App/Views/DiagramView.axaml src/Draw.App/Views/MainWindow.axaml
git add src/Draw.App/Views/DiagramView.axaml src/Draw.App/Views/MainWindow.axaml
git commit -m "Render actor, use-case and boundary nodes; add use-case palette

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase H — Verification & docs

## Task 10: Full verification + roadmap

**Files:**
- Modify: `documentation/roadmap.md`
- Modify: `documentation/architecture.md` (add a one-line note on use-case nodes)

- [ ] **Step 1: Full suite green**

Run: `dotnet test --solution Draw.slnx`
Expected: PASS. Record the count.

- [ ] **Step 2: Manual end-to-end** (Linux/WSL: fontconfig installed)

Run: `dotnet run --project src/Draw.App/Draw.App.csproj`
Checklist (report failures honestly): place actor/use-case/boundary; rename each; connect actor→use-case (association), use-case→use-case (include/extend), actor→actor (generalization); confirm boundary stays behind and selectable; save/open; undo/redo; theme toggle.

- [ ] **Step 3: Update docs**

In `documentation/roadmap.md`, change `## Phase 3 — UML class diagrams ✅ (current)` to `## Phase 3 — UML class diagrams ✅` and `## Phase 4 — Use-case diagrams` to `## Phase 4 — Use-case diagrams ✅ (current)`. In `documentation/architecture.md`, under the "Class diagrams (Phase 3)" section add a short "Use-case diagrams (Phase 4)" note: actor/use-case/system-boundary `NodeBase` subtypes on `NodeViewModelBase`, boundary visual-only behind via low z-index, inline label editing generalized via `HasInlineLabel`/`Label`, include/extend reuse Phase 2 connectors. Normalize both doc files to LF before committing (they carry the CRLF artifact); verify the diff is only your edits.

- [ ] **Step 4: Commit**

```bash
sed -i 's/\r$//' documentation/roadmap.md documentation/architecture.md
git add documentation/roadmap.md documentation/architecture.md
git commit -m "Mark Phase 4 complete in docs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 5: Finish the branch**

Use the superpowers:finishing-a-development-branch skill. Do not merge to `main` without the user's go-ahead. Note the CRLF working-tree quirk affects a local merge — use the fast-forward-by-ref approach (`git branch -f main <tip>` then `git checkout main`) that worked for Phase 3 rather than `git checkout main` first.

---

## Notes & honesty

- **Reuse over new code:** Phase 4 adds no parser, no member editor, no retype. The connector rendering for association/include/extend/generalization already existed (Phase 2); only the toolbox palette gains Include/Extend.
- **Boundary z-order** depends on BOTH a low `ZIndex` (survives rebuild) AND front-insertion into the `Nodes` collection (survives the live session before any rebuild). Both are in Task 5.
- **Inline label editing** is generalized: `OnDoubleTapped`/`EndEditing` key off `HasInlineLabel`, so shapes, actors, use-cases and boundaries all edit the same way, and inline commits now mark the document dirty (a small correctness improvement over the prior shape-only path).
- **AXAML/pointer tasks (8, 9)** have no UI unit harness; verified by `dotnet build` (compiled XAML) + the manual checklist. Not unit-tested — say so.
- **Logged limitation:** the system boundary does not group/move its contents (visual-only, per the approved spec A1).
