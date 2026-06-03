# Phase 3 — UML Class Diagrams Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add UML class-diagram support — a Class/Interface/Enum compartment node with structured, editable members — reusing Phase 2 connectors for relationships.

**Architecture:** A new polymorphic `ClassNode` model joins `ShapeNode` under `NodeBase`. The App layer extracts a `NodeViewModelBase` so selection/resize/style/canvas-placement serve both node kinds; `ClassNodeViewModel` + `ClassMemberViewModel` add compartments and members. A pure `MemberSignature` parser/formatter (Diagramming layer) round-trips members ↔ UML text. A single `INodeEditContext` (implemented by `DiagramDocumentViewModel`) gives member VMs undo capture, dirty marking, and type-autocomplete without coupling nodes to the document.

**Tech Stack:** .NET 10, C# 13, Avalonia 12, CommunityToolkit.Mvvm, System.Text.Json (polymorphic), xUnit v3 on Microsoft.Testing.Platform.

---

## Conventions for the executor

- **Branch:** work on `feature/phase3-uml-class-diagrams` (already created; the design spec is committed there).
- **Build:** `dotnet build Draw.slnx`
- **Test a project:** `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj` (swap project as needed). The repo's `global.json` already opts into Microsoft.Testing.Platform, so `dotnet test --project <csproj>` is correct. To run everything: `dotnet test --solution Draw.slnx`.
- **TDD granularity:** for pure logic (model, parser, view-model logic) follow strict red→green→commit. AXAML templates and pointer code-behind have **no UI test harness in this repo**, so those tasks are verified by `dotnet build` (compiled XAML catches binding/type errors) plus a manual run; this is called out explicitly per task. Do not claim a UI task is "tested" — say "builds; manually verified".
- **Running the app (Linux/WSL only):** native fontconfig is required or SkiaSharp fails — `sudo apt-get install -y libfontconfig1 libice6 libsm6`. Not needed on Windows/macOS. Launch with `dotnet run --project src/Draw.App/Draw.App.csproj`.
- **Commits:** git has no global identity in this environment. **Task 0** sets a local identity once; afterward `git commit` works. Every commit message ends with the trailer shown in Task 0.
- **Member is a class, not a record** (matches `ShapeStyle`). **`ClassMember.Type` is free text.** **`BoundaryKind` for class nodes is `ShapeKind.Rectangle`** (no Diagramming change).

---

## Task 0: One-time setup — git identity

**Files:** none (local git config).

- [ ] **Step 1: Set the repo-local identity** (matches existing history; avoids the "Author identity unknown" failure)

```bash
git config user.name "Josué Clément"
git config user.email "josue.d.clement@gmail.com"
```

- [ ] **Step 2: Confirm branch**

Run: `git branch --show-current`
Expected: `feature/phase3-uml-class-diagrams`

**Commit trailer to append to every commit body in this plan:**

```
Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

# Phase A — Model layer (`Draw.Model`)

## Task 1: Member enums

**Files:**
- Create: `src/Draw.Model/Nodes/ClassNodeKind.cs`
- Create: `src/Draw.Model/Nodes/MemberVisibility.cs`
- Create: `src/Draw.Model/Nodes/MemberKind.cs`

- [ ] **Step 1: Create the three enums**

`src/Draw.Model/Nodes/ClassNodeKind.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>The kind of UML classifier a <see cref="ClassNode"/> represents.</summary>
public enum ClassNodeKind
{
    Class = 0,
    Interface = 1,
    Enum = 2,
}
```

`src/Draw.Model/Nodes/MemberVisibility.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>UML member visibility, rendered as + - # ~ respectively.</summary>
public enum MemberVisibility
{
    Public = 0,
    Private = 1,
    Protected = 2,
    Package = 3,
}
```

`src/Draw.Model/Nodes/MemberKind.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>Which compartment a class member belongs to.</summary>
public enum MemberKind
{
    Field = 0,
    Operation = 1,
    EnumLiteral = 2,
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Draw.slnx`
Expected: succeeds (enums are unused so far).

- [ ] **Step 3: Commit**

```bash
git add src/Draw.Model/Nodes/ClassNodeKind.cs src/Draw.Model/Nodes/MemberVisibility.cs src/Draw.Model/Nodes/MemberKind.cs
git commit -m "Add UML class member enums

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: `ClassMember` (mutable, cloneable)

**Files:**
- Create: `src/Draw.Model/Nodes/ClassMember.cs`
- Test: `tests/Draw.Model.Tests/ClassMemberTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.Model.Tests/ClassMemberTests.cs`:
```csharp
using Draw.Model.Nodes;
using Xunit;

namespace Draw.Model.Tests;

public class ClassMemberTests
{
    [Fact]
    public void Clone_CopiesAllFields_AndIsIndependent()
    {
        ClassMember member = new()
        {
            Visibility = MemberVisibility.Protected,
            Name = "deposit",
            Type = "void",
            Parameters = "amount: decimal",
            Kind = MemberKind.Operation,
            IsStatic = true,
            IsAbstract = true,
        };

        ClassMember clone = member.Clone();
        clone.Name = "withdraw";

        Assert.Equal("deposit", member.Name);
        Assert.Equal("withdraw", clone.Name);
        Assert.Equal(MemberVisibility.Protected, clone.Visibility);
        Assert.Equal("void", clone.Type);
        Assert.Equal("amount: decimal", clone.Parameters);
        Assert.Equal(MemberKind.Operation, clone.Kind);
        Assert.True(clone.IsStatic);
        Assert.True(clone.IsAbstract);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: FAIL — `ClassMember` does not exist (compile error).

- [ ] **Step 3: Implement `ClassMember`**

`src/Draw.Model/Nodes/ClassMember.cs`:
```csharp
namespace Draw.Model.Nodes;

/// <summary>
/// One member of a <see cref="ClassNode"/>. <see cref="Type"/> is free text (the return type
/// for operations); <see cref="Parameters"/> is free text and used only by operations.
/// </summary>
public sealed class ClassMember
{
    public MemberVisibility Visibility { get; set; } = MemberVisibility.Public;

    public string Name { get; set; } = string.Empty;

    public string? Type { get; set; }

    public string? Parameters { get; set; }

    public MemberKind Kind { get; set; } = MemberKind.Field;

    public bool IsStatic { get; set; }

    public bool IsAbstract { get; set; }

    public ClassMember Clone() => new()
    {
        Visibility = Visibility,
        Name = Name,
        Type = Type,
        Parameters = Parameters,
        Kind = Kind,
        IsStatic = IsStatic,
        IsAbstract = IsAbstract,
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.Model/Nodes/ClassMember.cs tests/Draw.Model.Tests/ClassMemberTests.cs
git commit -m "Add ClassMember model with deep clone

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `ClassNode` + polymorphic serialization

**Files:**
- Create: `src/Draw.Model/Nodes/ClassNode.cs`
- Modify: `src/Draw.Model/Nodes/NodeBase.cs:14` (add `[JsonDerivedType]`)
- Test: `tests/Draw.Model.Tests/ClassNodeTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.Model.Tests/ClassNodeTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Model.Tests;

public class ClassNodeTests
{
    private static ClassNode SampleClassNode() => new()
    {
        Id = Guid.NewGuid(),
        Kind = ClassNodeKind.Interface,
        Name = "Account",
        IsAbstract = true,
        Bounds = new Rect2D(10, 20, 160, 100),
        Members = new List<ClassMember>
        {
            new() { Visibility = MemberVisibility.Private, Name = "id", Type = "Guid", Kind = MemberKind.Field },
            new() { Visibility = MemberVisibility.Public, Name = "deposit", Type = "void", Parameters = "amount: decimal", Kind = MemberKind.Operation },
        },
    };

    [Fact]
    public void Clone_DeepCopiesMembers()
    {
        ClassNode node = SampleClassNode();

        ClassNode clone = Assert.IsType<ClassNode>(node.Clone());
        clone.Members[0].Name = "changed";

        Assert.Equal(node.Id, clone.Id);
        Assert.Equal("id", node.Members[0].Name);
        Assert.Equal("changed", clone.Members[0].Name);
        Assert.NotSame(node.Members[0], clone.Members[0]);
    }

    [Fact]
    public void RoundTrip_PreservesClassNodeAndMembers()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new() { DiagramType = DiagramType.Class };
        doc.Nodes.Add(SampleClassNode());

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        ClassNode node = Assert.IsType<ClassNode>(Assert.Single(back.Nodes));
        Assert.Equal(ClassNodeKind.Interface, node.Kind);
        Assert.Equal("Account", node.Name);
        Assert.True(node.IsAbstract);
        Assert.Equal(2, node.Members.Count);
        Assert.Equal(MemberKind.Operation, node.Members[1].Kind);
        Assert.Equal("amount: decimal", node.Members[1].Parameters);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: FAIL — `ClassNode` does not exist.

- [ ] **Step 3: Implement `ClassNode`**

`src/Draw.Model/Nodes/ClassNode.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;

namespace Draw.Model.Nodes;

/// <summary>A UML classifier (class, interface or enum) drawn as a compartment box.</summary>
public sealed class ClassNode : NodeBase
{
    public ClassNodeKind Kind { get; set; } = ClassNodeKind.Class;

    public string Name { get; set; } = string.Empty;

    public bool IsAbstract { get; set; }

    public List<ClassMember> Members { get; set; } = new();

    public override NodeBase Clone()
    {
        ClassNode copy = new()
        {
            Kind = Kind,
            Name = Name,
            IsAbstract = IsAbstract,
            Members = Members.Select(m => m.Clone()).ToList(),
        };
        CopyBaseTo(copy);
        return copy;
    }
}
```

- [ ] **Step 4: Register the derived type**

Modify `src/Draw.Model/Nodes/NodeBase.cs` — add the attribute directly below the existing `[JsonDerivedType(typeof(ShapeNode), "shape")]` (line 14):
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ShapeNode), "shape")]
[JsonDerivedType(typeof(ClassNode), "class")]
public abstract class NodeBase
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --project tests/Draw.Model.Tests/Draw.Model.Tests.csproj`
Expected: PASS (both new tests + existing serializer tests).

- [ ] **Step 6: Commit**

```bash
git add src/Draw.Model/Nodes/ClassNode.cs src/Draw.Model/Nodes/NodeBase.cs tests/Draw.Model.Tests/ClassNodeTests.cs
git commit -m "Add ClassNode model and register it for polymorphic JSON

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase B — Member signature parser (`Draw.Diagramming/Uml`)

## Task 4: Primitive type list

**Files:**
- Create: `src/Draw.Diagramming/Uml/PrimitiveTypes.cs`
- Test: `tests/Draw.Diagramming.Tests/PrimitiveTypesTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.Diagramming.Tests/PrimitiveTypesTests.cs`:
```csharp
using Draw.Diagramming.Uml;
using Xunit;

namespace Draw.Diagramming.Tests;

public class PrimitiveTypesTests
{
    [Fact]
    public void All_ContainsCommonPrimitives_AndIsDistinct()
    {
        Assert.Contains("string", PrimitiveTypes.All);
        Assert.Contains("int", PrimitiveTypes.All);
        Assert.Contains("Guid", PrimitiveTypes.All);
        Assert.Contains("void", PrimitiveTypes.All);
        Assert.Equal(PrimitiveTypes.All.Count, PrimitiveTypes.All.Distinct().Count());
    }
}
```
(Add `using System.Linq;` at the top.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: FAIL — `PrimitiveTypes` does not exist.

- [ ] **Step 3: Implement**

`src/Draw.Diagramming/Uml/PrimitiveTypes.cs`:
```csharp
using System.Collections.Generic;

namespace Draw.Diagramming.Uml;

/// <summary>Common primitive/type names offered as autocomplete suggestions for member types.</summary>
public static class PrimitiveTypes
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "void", "bool", "byte", "char", "short", "int", "long",
        "float", "double", "decimal", "string", "object",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.Diagramming/Uml/PrimitiveTypes.cs tests/Draw.Diagramming.Tests/PrimitiveTypesTests.cs
git commit -m "Add primitive type suggestion list

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `MemberSignature.Format`

**Files:**
- Create: `src/Draw.Diagramming/Uml/MemberSignature.cs`
- Test: `tests/Draw.Diagramming.Tests/MemberSignatureTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Draw.Diagramming.Tests/MemberSignatureTests.cs`:
```csharp
using Draw.Diagramming.Uml;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MemberSignatureTests
{
    [Fact]
    public void Format_Field_WithType()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Private, Name = "balance", Type = "decimal", Kind = MemberKind.Field };
        Assert.Equal("- balance: decimal", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Field_WithoutType_OmitsColon()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Public, Name = "id", Kind = MemberKind.Field };
        Assert.Equal("+ id", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Operation_WithParamsAndReturn()
    {
        ClassMember m = new()
        {
            Visibility = MemberVisibility.Public, Name = "deposit",
            Parameters = "amount: decimal", Type = "void", Kind = MemberKind.Operation,
        };
        Assert.Equal("+ deposit(amount: decimal): void", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Operation_NoParams_NoReturn()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Protected, Name = "close", Kind = MemberKind.Operation };
        Assert.Equal("# close()", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_EnumLiteral_NameOnly()
    {
        ClassMember m = new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral };
        Assert.Equal("ACTIVE", MemberSignature.Format(m));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: FAIL — `MemberSignature` does not exist.

- [ ] **Step 3: Implement `Format` (and the visibility marker helper)**

`src/Draw.Diagramming/Uml/MemberSignature.cs`:
```csharp
using System;
using Draw.Model.Nodes;

namespace Draw.Diagramming.Uml;

/// <summary>Formats and parses class members to/from their UML text representation.</summary>
public static class MemberSignature
{
    public static string Format(ClassMember member)
    {
        if (member.Kind == MemberKind.EnumLiteral)
        {
            return member.Name;
        }

        string marker = Marker(member.Visibility);
        if (member.Kind == MemberKind.Operation)
        {
            string parameters = member.Parameters ?? string.Empty;
            string signature = $"{marker} {member.Name}({parameters})";
            return string.IsNullOrWhiteSpace(member.Type) ? signature : $"{signature}: {member.Type}";
        }

        return string.IsNullOrWhiteSpace(member.Type)
            ? $"{marker} {member.Name}"
            : $"{marker} {member.Name}: {member.Type}";
    }

    internal static string Marker(MemberVisibility visibility) => visibility switch
    {
        MemberVisibility.Private => "-",
        MemberVisibility.Protected => "#",
        MemberVisibility.Package => "~",
        _ => "+",
    };

    internal static MemberVisibility ParseMarker(char c) => c switch
    {
        '-' => MemberVisibility.Private,
        '#' => MemberVisibility.Protected,
        '~' => MemberVisibility.Package,
        _ => MemberVisibility.Public,
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.Diagramming/Uml/MemberSignature.cs tests/Draw.Diagramming.Tests/MemberSignatureTests.cs
git commit -m "Add MemberSignature.Format for UML member text

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: `MemberSignature.Parse` (tolerant, round-trips with Format)

**Files:**
- Modify: `src/Draw.Diagramming/Uml/MemberSignature.cs`
- Modify: `tests/Draw.Diagramming.Tests/MemberSignatureTests.cs`

`Parse(string text, MemberKind context)` always returns a `ClassMember` (best-effort; never throws). Rules: a leading `+ - # ~` sets visibility (default Public). If `context` is `EnumLiteral`, the result is an `EnumLiteral` with the trimmed text as Name. Otherwise, the presence of `(` ⇒ `Operation` (text inside `()` ⇒ `Parameters`, text after `):` ⇒ `Type`); no `(` ⇒ `Field` (text after `:` ⇒ `Type`).

- [ ] **Step 1: Add failing tests** (append to `MemberSignatureTests`)

```csharp
    [Theory]
    [InlineData("- balance: decimal", MemberVisibility.Private, "balance", "decimal", MemberKind.Field)]
    [InlineData("id", MemberVisibility.Public, "id", null, MemberKind.Field)]
    [InlineData("  + name : string ", MemberVisibility.Public, "name", "string", MemberKind.Field)]
    public void Parse_Field(string text, MemberVisibility vis, string name, string? type, MemberKind kind)
    {
        ClassMember m = MemberSignature.Parse(text, MemberKind.Field);
        Assert.Equal(vis, m.Visibility);
        Assert.Equal(name, m.Name);
        Assert.Equal(type, m.Type);
        Assert.Equal(kind, m.Kind);
    }

    [Fact]
    public void Parse_Operation_WithParamsAndReturn()
    {
        ClassMember m = MemberSignature.Parse("+ deposit(amount: decimal): void", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("deposit", m.Name);
        Assert.Equal("amount: decimal", m.Parameters);
        Assert.Equal("void", m.Type);
    }

    [Fact]
    public void Parse_Operation_NoReturn()
    {
        ClassMember m = MemberSignature.Parse("# close()", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("close", m.Name);
        Assert.Equal(string.Empty, m.Parameters);
        Assert.Null(m.Type);
    }

    [Fact]
    public void Parse_EnumLiteralContext_KeepsName()
    {
        ClassMember m = MemberSignature.Parse("ACTIVE", MemberKind.EnumLiteral);
        Assert.Equal(MemberKind.EnumLiteral, m.Kind);
        Assert.Equal("ACTIVE", m.Name);
    }

    [Theory]
    [InlineData("- balance: decimal", MemberKind.Field)]
    [InlineData("+ deposit(amount: decimal): void", MemberKind.Field)]
    [InlineData("# close()", MemberKind.Field)]
    [InlineData("ACTIVE", MemberKind.EnumLiteral)]
    public void Parse_RoundTripsWithFormat(string text, MemberKind context)
    {
        ClassMember m = MemberSignature.Parse(text, context);
        Assert.Equal(text, MemberSignature.Format(m));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: FAIL — `Parse` not defined.

- [ ] **Step 3: Implement `Parse`** (add to `MemberSignature`)

```csharp
    public static ClassMember Parse(string text, MemberKind context)
    {
        string s = (text ?? string.Empty).Trim();

        MemberVisibility visibility = MemberVisibility.Public;
        if (s.Length > 0 && (s[0] is '+' or '-' or '#' or '~'))
        {
            visibility = ParseMarker(s[0]);
            s = s[1..].Trim();
        }

        if (context == MemberKind.EnumLiteral)
        {
            return new ClassMember { Kind = MemberKind.EnumLiteral, Visibility = visibility, Name = s };
        }

        int open = s.IndexOf('(');
        if (open >= 0)
        {
            int close = s.IndexOf(')', open + 1);
            string name = s[..open].Trim();
            string parameters = close > open ? s[(open + 1)..close].Trim() : string.Empty;
            string? type = null;
            if (close >= 0)
            {
                int colon = s.IndexOf(':', close);
                if (colon >= 0)
                {
                    type = NullIfEmpty(s[(colon + 1)..].Trim());
                }
            }

            return new ClassMember
            {
                Kind = MemberKind.Operation,
                Visibility = visibility,
                Name = name,
                Parameters = parameters,
                Type = type,
            };
        }

        int fieldColon = s.IndexOf(':');
        if (fieldColon >= 0)
        {
            return new ClassMember
            {
                Kind = MemberKind.Field,
                Visibility = visibility,
                Name = s[..fieldColon].Trim(),
                Type = NullIfEmpty(s[(fieldColon + 1)..].Trim()),
            };
        }

        return new ClassMember { Kind = MemberKind.Field, Visibility = visibility, Name = s };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
```

Note: `Format` of an operation with empty `Parameters` produces `name()`; `Parse` of `name()` produces `Parameters == ""` — so the round-trip holds. Keep `Parameters` as `""` (not null) for parsed operations.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.Diagramming.Tests/Draw.Diagramming.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.Diagramming/Uml/MemberSignature.cs tests/Draw.Diagramming.Tests/MemberSignatureTests.cs
git commit -m "Add tolerant MemberSignature.Parse with Format round-trip

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase C — `NodeViewModelBase` extraction (`Draw.App`)

> This phase is a mechanical refactor that must stay green at every step. No behavior changes; existing tests are the safety net.

## Task 7: Introduce `NodeViewModelBase`; make `ShapeNodeViewModel` derive from it

**Files:**
- Create: `src/Draw.App/ViewModels/NodeViewModelBase.cs`
- Modify: `src/Draw.App/ViewModels/ShapeNodeViewModel.cs` (rewrite to derive)

- [ ] **Step 1: Create the base class**

`src/Draw.App/ViewModels/NodeViewModelBase.cs`:
```csharp
using System;
using Avalonia.Collections;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>Bindable concerns shared by every node kind: placement, selection and style.</summary>
public abstract class NodeViewModelBase : ViewModelBase
{
    protected NodeViewModelBase(NodeBase model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public NodeBase Model { get; }

    public Guid Id => Model.Id;

    /// <summary>The shape kind used for connector boundary attachment.</summary>
    public abstract ShapeKind BoundaryKind { get; }

    /// <summary>Smallest width/height a resize gesture may produce for this node.</summary>
    public virtual double MinWidth => 12d;

    public virtual double MinHeight => 12d;

    public Rect2D Bounds => Model.Bounds;

    public double X
    {
        get => Model.Bounds.X;
        set
        {
            if (!Model.Bounds.X.Equals(value))
            {
                Model.Bounds = Model.Bounds with { X = value };
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => Model.Bounds.Y;
        set
        {
            if (!Model.Bounds.Y.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Y = value };
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get => Model.Bounds.Width;
        set
        {
            if (!Model.Bounds.Width.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Width = value };
                OnPropertyChanged();
                OnSizeChanged();
            }
        }
    }

    public double Height
    {
        get => Model.Bounds.Height;
        set
        {
            if (!Model.Bounds.Height.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Height = value };
                OnPropertyChanged();
                OnSizeChanged();
            }
        }
    }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IBrush Fill => Model.Style.Fill.ToBrush();

    public IBrush Stroke => Model.Style.Stroke.Color.ToBrush();

    public double StrokeThickness => Model.Style.Stroke.Thickness;

    public AvaloniaList<double>? StrokeDashArray => Model.Style.Stroke.Dash.ToDashArray();

    public IBrush Foreground => Model.Style.Font.Color.ToBrush();

    public FontFamily FontFamily => new(Model.Style.Font.Family);

    public double FontSize => Model.Style.Font.Size;

    public FontWeight FontWeight => Model.Style.Font.Bold ? FontWeight.Bold : FontWeight.Normal;

    public FontStyle FontStyle => Model.Style.Font.Italic ? FontStyle.Italic : FontStyle.Normal;

    public TextAlignment TextAlignment => Model.Style.TextAlignment.ToAvalonia();

    /// <summary>Re-raises all style-derived properties after the inspector edits the model style.</summary>
    public virtual void RaiseStyleChanged()
    {
        OnPropertyChanged(nameof(Fill));
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(StrokeDashArray));
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontWeight));
        OnPropertyChanged(nameof(FontStyle));
        OnPropertyChanged(nameof(TextAlignment));
    }

    /// <summary>Called when width/height change so subclasses can re-raise size-dependent properties.</summary>
    protected virtual void OnSizeChanged()
    {
    }
}
```

- [ ] **Step 2: Rewrite `ShapeNodeViewModel` to derive from the base**

Replace the entire body of `src/Draw.App/ViewModels/ShapeNodeViewModel.cs` with:
```csharp
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ShapeNode"/>; the model is the backing store.</summary>
public sealed class ShapeNodeViewModel : NodeViewModelBase
{
    private readonly ShapeNode _model;

    public ShapeNodeViewModel(ShapeNode model)
        : base(model)
    {
        _model = model;
    }

    public new ShapeNode Model => _model;

    public override ShapeKind BoundaryKind => _model.Kind;

    public ShapeKind Kind => _model.Kind;

    public string Text
    {
        get => _model.Text;
        set
        {
            if (!string.Equals(_model.Text, value, System.StringComparison.Ordinal))
            {
                _model.Text = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public Geometry Geometry
        => ShapeGeometryBuilder.Build(_model.Kind, _model.Bounds.Width, _model.Bounds.Height, _model.CornerRadius);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
```

Notes for the executor:
- `public new ShapeNode Model` shadows the base `NodeBase Model` with the typed model — existing callers that use `shapeVm.Model.Text`/`.Kind`/`.CornerRadius` keep compiling.
- X/Y/Width/Height/IsSelected/IsEditing and all style brushes now come from the base; do not redeclare them.
- The base's `Width`/`Height` setters call `OnSizeChanged()`, which this class overrides to re-raise `Geometry` (preserving the old behavior).

- [ ] **Step 3: Build + run existing tests (safety net)**

Run: `dotnet build Draw.slnx && dotnet test --solution Draw.slnx`
Expected: PASS — all existing tests green. (`DiagramView.axaml.cs`, `ConnectorViewModel`, `DiagramDocumentViewModel`, `InspectorViewModel` still reference `ShapeNodeViewModel` concretely, which is unchanged in API.)

- [ ] **Step 4: Commit**

```bash
git add src/Draw.App/ViewModels/NodeViewModelBase.cs src/Draw.App/ViewModels/ShapeNodeViewModel.cs
git commit -m "Extract NodeViewModelBase from ShapeNodeViewModel

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Retype `ConnectorViewModel` endpoints to `NodeViewModelBase`

**Files:**
- Modify: `src/Draw.App/ViewModels/ConnectorViewModel.cs`

- [ ] **Step 1: Change endpoint types and routing inputs**

In `ConnectorViewModel`:
- Constructor signature: `public ConnectorViewModel(Connector model, NodeViewModelBase source, NodeViewModelBase target, IConnectorRouter router)`.
- Properties: `public NodeViewModelBase Source { get; }` and `public NodeViewModelBase Target { get; }`.
- Replace `Compute()` body to use the base members:
```csharp
    private ConnectorRoute Compute()
    {
        ConnectorRouteRequest request = new(
            Source.BoundaryKind,
            Source.Bounds,
            Target.BoundaryKind,
            Target.Bounds,
            _model.Route,
            _model.BendPoints);
        return _router.Route(request);
    }
```
- In `OnEndpointChanged`, change the `nameof` references from `ShapeNodeViewModel` to `NodeViewModelBase`:
```csharp
        if (e.PropertyName is nameof(NodeViewModelBase.X)
            or nameof(NodeViewModelBase.Y)
            or nameof(NodeViewModelBase.Width)
            or nameof(NodeViewModelBase.Height))
```

(These property names are identical strings, so behavior is unchanged; using the base type is correct now that endpoints are typed to it.)

- [ ] **Step 2: Build**

Run: `dotnet build Draw.slnx`
Expected: FAILS to compile in `DiagramDocumentViewModel` (it constructs `ConnectorViewModel` with `ShapeNodeViewModel` and `byId` is typed to `ShapeNodeViewModel`). That is fixed in Task 9. **Do not commit a broken build** — proceed straight to Task 9, then build/test/commit Tasks 8+9 together.

> Implementation note: Tasks 8 and 9 form one compile unit. Make both edits, then build, test, and commit once (see Task 9 Step 4). They are split only for review clarity.

---

## Task 9: Retype `DiagramDocumentViewModel` collections; type-aware `RebuildNodes`

**Files:**
- Modify: `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`

- [ ] **Step 1: Retype the node collection and node-typed members**

- `public ObservableCollection<NodeViewModelBase> Nodes { get; } = new();`
- `public IEnumerable<NodeViewModelBase> SelectedNodes => Nodes.Where(n => n.IsSelected);`
- `public NodeViewModelBase? FindNode(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);`
- `AddConnector`: change the two locals to the base type:
```csharp
        NodeViewModelBase? source = FindNode(sourceId);
        NodeViewModelBase? target = FindNode(targetId);
```
- `SelectOnly(NodeViewModelBase vm)`, `ToggleSelect(NodeViewModelBase vm)`, `SetNodeBounds(NodeViewModelBase vm, Rect2D bounds)` — change the parameter types from `ShapeNodeViewModel` to `NodeViewModelBase`. Their bodies are unchanged (`X/Y/Width/Height` are on the base).
- `DeleteSelected`: change `List<ShapeNodeViewModel> selectedNodes` to `List<NodeViewModelBase> selectedNodes` and the `foreach` variable type to `NodeViewModelBase`. (`vm.Model` and `vm.Id` exist on the base.)
- `MoveSelectedBy`, `SnapSelectionToGrid`, `SelectInRect`, `SelectConnector`, `ClearSelection` loops: change `foreach (ShapeNodeViewModel ...)` / `foreach (ShapeNodeViewModel n in Nodes)` to `NodeViewModelBase`.

- [ ] **Step 2: Type-aware `RebuildNodes` and `RebuildConnectors`**

Replace `RebuildNodes`:
```csharp
    private void RebuildNodes()
    {
        Nodes.Clear();
        foreach (NodeBase node in _document.Nodes.OrderBy(n => n.ZIndex))
        {
            Nodes.Add(CreateNodeViewModel(node));
        }

        RaiseSelectionChanged();
    }

    private NodeViewModelBase CreateNodeViewModel(NodeBase node) => node switch
    {
        ShapeNode shape => new ShapeNodeViewModel(shape),
        // ClassNode arm is added in Task 14 once ClassNodeViewModel exists.
        _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}"),
    };
```

In `RebuildConnectors`, change the dictionary type:
```csharp
        Dictionary<Guid, NodeViewModelBase> byId = Nodes.ToDictionary(n => n.Id);
        foreach (Connector connector in _document.Connectors)
        {
            if (byId.TryGetValue(connector.SourceNodeId, out NodeViewModelBase? source)
                && byId.TryGetValue(connector.TargetNodeId, out NodeViewModelBase? target))
            {
                Connectors.Add(new ConnectorViewModel(connector, source, target, _router));
            }
        }
```

`AddShape` keeps returning `ShapeNodeViewModel` and still does `Nodes.Add(vm)` (covariant into the base collection — fine).

- [ ] **Step 3: Build + run all tests**

Run: `dotnet build Draw.slnx && dotnet test --solution Draw.slnx`
Expected: PASS. `DiagramView.axaml.cs` still compiles because it uses `ShapeNodeViewModel` locals via `HitTestNode`/`SelectedNodes.First()` — **but** `SelectedNodes` is now `IEnumerable<NodeViewModelBase>`, so `HandlePositions(_vm.SelectedNodes.First())` and the `_resizeTarget`/`_connectSource` assignments will FAIL to compile. If the build fails here, that is expected — complete Task 10 in the same edit cycle, then build/test/commit Tasks 8–10 together.

> Implementation note: Tasks 8, 9 and 10 form one compile unit. Easiest path: make all three tasks' edits, then build once, run the full suite, and commit once with the message below.

- [ ] **Step 4: Commit (covers Tasks 8–10)**

```bash
git add src/Draw.App/ViewModels/ConnectorViewModel.cs src/Draw.App/ViewModels/DiagramDocumentViewModel.cs src/Draw.App/Views/DiagramView.axaml.cs
git commit -m "Retype node view-models to NodeViewModelBase across app

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Retype `DiagramView.axaml.cs`; class-safe double-tap; per-node resize minimum

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml.cs`

- [ ] **Step 1: Retype fields and node helpers**

- Fields: `private NodeViewModelBase? _resizeTarget;` and `private NodeViewModelBase? _connectSource;`.
- `HitTestNode` returns `NodeViewModelBase?`:
```csharp
    private NodeViewModelBase? HitTestNode(Point world)
    {
        Point2D p = new(world.X, world.Y);
        return _vm?.Nodes.LastOrDefault(n => n.Model.Bounds.Contains(p));
    }
```
- `HandlePositions(NodeViewModelBase node)` and `ResizeBounds(NodeViewModelBase node, ...)` — change the parameter type. In `ResizeBounds`, replace the two `MinShapeSize` clamps with the node's own minimum:
```csharp
        double width = Math.Max(node.MinWidth, Math.Abs(r - l));
        double height = Math.Max(node.MinHeight, Math.Abs(bottom - t));
```
(`MinShapeSize` is now only the default via `NodeViewModelBase.MinWidth/MinHeight`; you may leave the `MinShapeSize` const in place — it is no longer referenced — or delete it. Delete it to avoid an unused-field warning.)
- In `OnPointerPressed`, the connector-mode block and the move block use `HitTestNode`, which now returns the base type. Assigning to `_connectSource` (now base-typed) is fine. `_vm.SelectOnly(node)` / `ToggleSelect(node)` accept the base type (Task 9).

- [ ] **Step 2: Make canvas double-tap shape-only**

Class-node text isn't a single field, so node-level double-tap editing applies only to shapes (member rows handle their own double-tap in Task 21):
```csharp
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        Point world = ScreenToWorld(e.GetPosition(Viewport));
        if (HitTestNode(world) is ShapeNodeViewModel shape)
        {
            _vm.CaptureUndo();
            shape.IsEditing = true;
        }
    }
```

- [ ] **Step 3: `StartConnectPreview` parameter type**

Change `private void StartConnectPreview(NodeViewModelBase from, Point world)` (body unchanged; `from.Model.Bounds.Center` works on the base).

- [ ] **Step 4: Build + run all tests** (this completes the Tasks 8–10 compile unit)

Run: `dotnet build Draw.slnx && dotnet test --solution Draw.slnx`
Expected: PASS. Commit via Task 9 Step 4 (single commit for 8–10).

- [ ] **Step 5: Manual smoke (optional but recommended)**

Run the app and confirm Phase 1/2 behavior is unchanged: place shapes, move/resize, draw connectors, double-tap a shape to edit text, undo/redo.
Run: `dotnet run --project src/Draw.App/Draw.App.csproj`

---

# Phase D — Edit context + class view-models (`Draw.App`)

## Task 11: `INodeEditContext`; document implements it; `GetTypeSuggestions`

**Files:**
- Create: `src/Draw.App/ViewModels/INodeEditContext.cs`
- Modify: `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`
- Test: `tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs`

- [ ] **Step 1: Write the failing test** (append to `DiagramDocumentViewModelTests`)

This task introduces only the interface and `GetTypeSuggestions`. `AddClassNode`/`ClassNodeViewModel` don't exist yet (Tasks 13–14), so the test here asserts only the primitive baseline; the class-name assertion is added in Task 14 once class nodes exist. Add `using System.Linq;` if not already present.
```csharp
    [Fact]
    public void GetTypeSuggestions_OnEmptyDocument_ReturnsDistinctPrimitives()
    {
        DiagramDocumentViewModel doc = CreateDocument();

        var suggestions = doc.GetTypeSuggestions();

        Assert.Contains("string", suggestions);
        Assert.Contains("int", suggestions);
        Assert.Equal(suggestions.Count, suggestions.Distinct().Count());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `GetTypeSuggestions` not defined.

- [ ] **Step 3: Create the interface**

`src/Draw.App/ViewModels/INodeEditContext.cs`:
```csharp
using System.Collections.Generic;

namespace Draw.App.ViewModels;

/// <summary>
/// The document-level services a class node and its members need: undo capture before an edit,
/// dirty marking after, and the current set of type-name autocomplete suggestions.
/// </summary>
public interface INodeEditContext
{
    void BeginMemberEdit();

    void EndMemberEdit();

    IReadOnlyList<string> GetTypeSuggestions();
}
```

- [ ] **Step 4: Implement on `DiagramDocumentViewModel`**

- Change the class declaration:
```csharp
public sealed class DiagramDocumentViewModel : ViewModelBase, INodeEditContext
```
- Add `using Draw.Diagramming.Uml;` to the file's usings.
- Add the members (place near `NotifyStyleEditStarting`):
```csharp
    void INodeEditContext.BeginMemberEdit() => CaptureUndo();

    void INodeEditContext.EndMemberEdit() => MarkModified();

    public IReadOnlyList<string> GetTypeSuggestions()
    {
        IEnumerable<string> names = _document.Nodes
            .OfType<ClassNode>()
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n));

        return PrimitiveTypes.All
            .Concat(names)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Draw.App/ViewModels/INodeEditContext.cs src/Draw.App/ViewModels/DiagramDocumentViewModel.cs tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs
git commit -m "Add INodeEditContext and type-suggestion source on document

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: `ClassMemberViewModel`

**Files:**
- Create: `src/Draw.App/ViewModels/ClassMemberViewModel.cs`
- Test: `tests/Draw.App.Tests/ClassMemberViewModelTests.cs`

Behavior: write-through to the model wrapped in `BeginMemberEdit`/`EndMemberEdit` (so each inspector field edit is one undo step + marks dirty); `DisplayText`/`RawText` via `MemberSignature`; inline `BeginEdit`/`CommitEdit`/`CancelEdit`; `RowFontStyle` (italic when abstract) and `RowDecorations` (underline when static) for rendering; `TypeSuggestions` from the context.

- [ ] **Step 1: Write the failing test**

`tests/Draw.App.Tests/ClassMemberViewModelTests.cs`:
```csharp
using System.Collections.Generic;
using Draw.App.ViewModels;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.App.Tests;

public class ClassMemberViewModelTests
{
    private sealed class FakeContext : INodeEditContext
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public void BeginMemberEdit() => Begins++;
        public void EndMemberEdit() => Ends++;
        public IReadOnlyList<string> GetTypeSuggestions() => new[] { "int", "string" };
    }

    [Fact]
    public void SettingName_WritesThrough_AndBracketsWithContext()
    {
        ClassMember model = new() { Name = "x", Kind = MemberKind.Field };
        FakeContext ctx = new();
        ClassMemberViewModel vm = new(model, ctx);

        vm.Name = "balance";

        Assert.Equal("balance", model.Name);
        Assert.Equal(1, ctx.Begins);
        Assert.Equal(1, ctx.Ends);
    }

    [Fact]
    public void DisplayText_ReflectsModel()
    {
        ClassMember model = new() { Visibility = MemberVisibility.Private, Name = "id", Type = "Guid", Kind = MemberKind.Field };
        ClassMemberViewModel vm = new(model, new FakeContext());

        Assert.Equal("- id: Guid", vm.DisplayText);
    }

    [Fact]
    public void CommitEdit_ParsesRawText_IntoModel()
    {
        ClassMember model = new() { Name = "old", Kind = MemberKind.Field };
        ClassMemberViewModel vm = new(model, new FakeContext());

        vm.BeginEdit();
        vm.RawText = "+ deposit(amount: decimal): void";
        vm.CommitEdit();

        Assert.False(vm.IsEditing);
        Assert.Equal(MemberKind.Operation, model.Kind);
        Assert.Equal("deposit", model.Name);
        Assert.Equal("amount: decimal", model.Parameters);
        Assert.Equal("void", model.Type);
    }

    [Fact]
    public void EnumLiteralContext_CommitKeepsLiteralKind()
    {
        ClassMember model = new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral };
        ClassMemberViewModel vm = new(model, new FakeContext());

        vm.BeginEdit();
        vm.RawText = "INACTIVE";
        vm.CommitEdit();

        Assert.Equal(MemberKind.EnumLiteral, model.Kind);
        Assert.Equal("INACTIVE", model.Name);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `ClassMemberViewModel` not defined.

- [ ] **Step 3: Implement**

`src/Draw.App/ViewModels/ClassMemberViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using Avalonia.Media;
using Draw.Diagramming.Uml;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassMember"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class ClassMemberViewModel : ViewModelBase
{
    private readonly ClassMember _model;
    private readonly INodeEditContext _context;

    public ClassMemberViewModel(ClassMember model, INodeEditContext context)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ClassMember Model => _model;

    public IReadOnlyList<string> TypeSuggestions => _context.GetTypeSuggestions();

    public MemberVisibility Visibility
    {
        get => _model.Visibility;
        set => Edit(() => _model.Visibility = value, _model.Visibility != value);
    }

    public string Name
    {
        get => _model.Name;
        set => Edit(() => _model.Name = value ?? string.Empty, !string.Equals(_model.Name, value, StringComparison.Ordinal));
    }

    public string? Type
    {
        get => _model.Type;
        set => Edit(() => _model.Type = string.IsNullOrWhiteSpace(value) ? null : value, !string.Equals(_model.Type, value, StringComparison.Ordinal));
    }

    public string? Parameters
    {
        get => _model.Parameters;
        set => Edit(() => _model.Parameters = value, !string.Equals(_model.Parameters, value, StringComparison.Ordinal));
    }

    public bool IsStatic
    {
        get => _model.IsStatic;
        set => Edit(() => _model.IsStatic = value, _model.IsStatic != value);
    }

    public bool IsAbstract
    {
        get => _model.IsAbstract;
        set => Edit(() => _model.IsAbstract = value, _model.IsAbstract != value);
    }

    public bool IsOperation => _model.Kind == MemberKind.Operation;

    public string DisplayText => MemberSignature.Format(_model);

    public FontStyle RowFontStyle => _model.IsAbstract ? FontStyle.Italic : FontStyle.Normal;

    public TextDecorationCollection? RowDecorations => _model.IsStatic ? TextDecorations.Underline : null;

    public string RawText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsEditing
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Enters inline editing, seeding the editable text from the current model.</summary>
    public void BeginEdit()
    {
        RawText = MemberSignature.Format(_model);
        IsEditing = true;
    }

    /// <summary>Parses the edited text back into the model and leaves edit mode.</summary>
    public void CommitEdit()
    {
        MemberKind context = _model.Kind == MemberKind.EnumLiteral ? MemberKind.EnumLiteral : MemberKind.Field;
        ClassMember parsed = MemberSignature.Parse(RawText, context);
        _model.Visibility = parsed.Visibility;
        _model.Name = parsed.Name;
        _model.Type = parsed.Type;
        _model.Parameters = parsed.Parameters;
        _model.Kind = parsed.Kind;
        IsEditing = false;
        RaiseAll();
    }

    public void CancelEdit() => IsEditing = false;

    private void Edit(Action mutate, bool changed)
    {
        if (!changed)
        {
            return;
        }

        _context.BeginMemberEdit();
        mutate();
        _context.EndMemberEdit();
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Parameters));
        OnPropertyChanged(nameof(IsStatic));
        OnPropertyChanged(nameof(IsAbstract));
        OnPropertyChanged(nameof(IsOperation));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(RowFontStyle));
        OnPropertyChanged(nameof(RowDecorations));
    }
}
```

Note: inline `CommitEdit` does not call the context — undo capture and dirty marking for inline edits are driven by the code-behind (Task 21), mirroring how shape inline text editing captures undo on double-tap. Inspector field edits go through `Edit(...)` and are bracketed by the context.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/ViewModels/ClassMemberViewModel.cs tests/Draw.App.Tests/ClassMemberViewModelTests.cs
git commit -m "Add ClassMemberViewModel with inline and field editing

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 13: `ClassNodeViewModel`

**Files:**
- Create: `src/Draw.App/ViewModels/ClassNodeViewModel.cs`
- Test: `tests/Draw.App.Tests/ClassNodeViewModelTests.cs`

Behavior: derives from `NodeViewModelBase`; `BoundaryKind => Rectangle`; splits members into `PrimaryMembers` (Fields, or EnumLiterals for an enum) and `Operations`; `Name`/`IsAbstract` write-through via the context; `Stereotype`/`HasStereotype`/`HasOperations`/`IsEnum`; `AddPrimaryMember`/`AddOperation`/`RemoveMember`/`MoveMember`; computed `MinHeight` that grows with content; `CommitPendingEdits` for the Escape/blur path.

- [ ] **Step 1: Write the failing test**

`tests/Draw.App.Tests/ClassNodeViewModelTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Draw.App.ViewModels;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Styling;
using Xunit;

namespace Draw.App.Tests;

public class ClassNodeViewModelTests
{
    private sealed class FakeContext : INodeEditContext
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public void BeginMemberEdit() => Begins++;
        public void EndMemberEdit() => Ends++;
        public IReadOnlyList<string> GetTypeSuggestions() => new[] { "int" };
    }

    private static ClassNode Model(ClassNodeKind kind = ClassNodeKind.Class) => new()
    {
        Kind = kind,
        Name = "Account",
        Bounds = new Rect2D(0, 0, 160, 100),
        Style = ShapeStyle.CreateDefault(),
        Members = new List<ClassMember>
        {
            new() { Name = "id", Type = "Guid", Kind = MemberKind.Field },
            new() { Name = "deposit", Kind = MemberKind.Operation },
        },
    };

    [Fact]
    public void BoundaryKind_IsRectangle()
    {
        ClassNodeViewModel vm = new(Model(), new FakeContext());
        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
    }

    [Fact]
    public void SplitsMembers_IntoAttributesAndOperations()
    {
        ClassNodeViewModel vm = new(Model(), new FakeContext());
        Assert.Single(vm.PrimaryMembers);
        Assert.Equal("id", vm.PrimaryMembers[0].Name);
        Assert.Single(vm.Operations);
        Assert.Equal("deposit", vm.Operations[0].Name);
        Assert.True(vm.HasOperations);
    }

    [Fact]
    public void EnumNode_HasNoOperations_AndLiteralsArePrimary()
    {
        ClassNode model = Model(ClassNodeKind.Enum);
        model.Members = new List<ClassMember> { new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral } };
        ClassNodeViewModel vm = new(model, new FakeContext());

        Assert.False(vm.HasOperations);
        Assert.True(vm.IsEnum);
        Assert.Equal("ACTIVE", vm.PrimaryMembers[0].Name);
        Assert.Equal("«enumeration»", vm.Stereotype);
    }

    [Fact]
    public void AddPrimaryMember_AddsToModelAndCollection_AndCapturesUndo()
    {
        ClassNode model = Model();
        FakeContext ctx = new();
        ClassNodeViewModel vm = new(model, ctx);

        vm.AddPrimaryMember();

        Assert.Equal(2, vm.PrimaryMembers.Count);
        Assert.Equal(3, model.Members.Count);
        Assert.True(ctx.Begins >= 1);
        Assert.True(ctx.Ends >= 1);
    }

    [Fact]
    public void RemoveMember_RemovesFromModelAndCollection()
    {
        ClassNode model = Model();
        ClassNodeViewModel vm = new(model, new FakeContext());
        ClassMemberViewModel op = vm.Operations[0];

        vm.RemoveMember(op);

        Assert.Empty(vm.Operations);
        Assert.DoesNotContain(op.Model, model.Members);
    }

    [Fact]
    public void Interface_Stereotype()
    {
        ClassNodeViewModel vm = new(Model(ClassNodeKind.Interface), new FakeContext());
        Assert.Equal("«interface»", vm.Stereotype);
        Assert.True(vm.HasStereotype);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `ClassNodeViewModel` not defined.

- [ ] **Step 3: Implement**

`src/Draw.App/ViewModels/ClassNodeViewModel.cs`:
```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassNode"/>: name, stereotype and member compartments.</summary>
public sealed class ClassNodeViewModel : NodeViewModelBase
{
    private const double NameCompartmentHeight = 28d;
    private const double MemberRowHeight = 18d;
    private const double CompartmentPadding = 8d;

    private readonly ClassNode _model;
    private readonly INodeEditContext _context;

    public ClassNodeViewModel(ClassNode model, INodeEditContext context)
        : base(model)
    {
        _model = model;
        _context = context ?? throw new ArgumentNullException(nameof(context));

        PrimaryMembers = new ObservableCollection<ClassMemberViewModel>(
            _model.Members.Where(IsPrimary).Select(Wrap));
        Operations = new ObservableCollection<ClassMemberViewModel>(
            _model.Members.Where(m => m.Kind == MemberKind.Operation).Select(Wrap));
    }

    public new ClassNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public ClassNodeKind Kind => _model.Kind;

    public bool IsEnum => _model.Kind == ClassNodeKind.Enum;

    public bool HasOperations => _model.Kind != ClassNodeKind.Enum;

    public string? Stereotype => _model.Kind switch
    {
        ClassNodeKind.Interface => "«interface»",
        ClassNodeKind.Enum => "«enumeration»",
        _ => null,
    };

    public bool HasStereotype => Stereotype is not null;

    public ObservableCollection<ClassMemberViewModel> PrimaryMembers { get; }

    public ObservableCollection<ClassMemberViewModel> Operations { get; }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _context.BeginMemberEdit();
                _model.Name = value ?? string.Empty;
                _context.EndMemberEdit();
                OnPropertyChanged();
            }
        }
    }

    public bool IsAbstract
    {
        get => _model.IsAbstract;
        set
        {
            if (_model.IsAbstract != value)
            {
                _context.BeginMemberEdit();
                _model.IsAbstract = value;
                _context.EndMemberEdit();
                OnPropertyChanged();
            }
        }
    }

    public override double MinHeight
    {
        get
        {
            int rows = PrimaryMembers.Count + Operations.Count;
            return NameCompartmentHeight + (rows * MemberRowHeight) + CompartmentPadding;
        }
    }

    public override double MinWidth => 80d;

    public void AddPrimaryMember()
    {
        MemberKind kind = IsEnum ? MemberKind.EnumLiteral : MemberKind.Field;
        string name = IsEnum ? "LITERAL" : "field";
        AddMember(new ClassMember { Kind = kind, Name = name, Visibility = MemberVisibility.Public }, PrimaryMembers);
    }

    public void AddOperation()
        => AddMember(new ClassMember { Kind = MemberKind.Operation, Name = "operation", Visibility = MemberVisibility.Public }, Operations);

    public void RemoveMember(ClassMemberViewModel member)
    {
        if (member is null)
        {
            return;
        }

        _context.BeginMemberEdit();
        _model.Members.Remove(member.Model);
        PrimaryMembers.Remove(member);
        Operations.Remove(member);
        _context.EndMemberEdit();
        OnPropertyChanged(nameof(MinHeight));
    }

    public void MoveMember(ClassMemberViewModel member, int delta)
    {
        ObservableCollection<ClassMemberViewModel> list = PrimaryMembers.Contains(member) ? PrimaryMembers : Operations;
        int index = list.IndexOf(member);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return;
        }

        _context.BeginMemberEdit();
        list.Move(index, target);
        ReorderModelFromCollections();
        _context.EndMemberEdit();
    }

    /// <summary>Commits any member row currently being edited (used by the Escape/blur path).</summary>
    public bool CommitPendingEdits()
    {
        bool any = false;
        foreach (ClassMemberViewModel m in PrimaryMembers.Concat(Operations))
        {
            if (m.IsEditing)
            {
                m.CommitEdit();
                any = true;
            }
        }

        return any;
    }

    private void AddMember(ClassMember member, ObservableCollection<ClassMemberViewModel> list)
    {
        _context.BeginMemberEdit();
        _model.Members.Add(member);
        list.Add(Wrap(member));
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
    }

    private void GrowToFitContent()
    {
        if (Height < MinHeight)
        {
            Height = MinHeight; // base setter writes through to the model bounds
        }
    }

    private void ReorderModelFromCollections()
    {
        _model.Members.Clear();
        foreach (ClassMemberViewModel m in PrimaryMembers)
        {
            _model.Members.Add(m.Model);
        }

        foreach (ClassMemberViewModel m in Operations)
        {
            _model.Members.Add(m.Model);
        }
    }

    private static bool IsPrimary(ClassMember m) => m.Kind is MemberKind.Field or MemberKind.EnumLiteral;

    private ClassMemberViewModel Wrap(ClassMember m) => new(m, _context);
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/ViewModels/ClassNodeViewModel.cs tests/Draw.App.Tests/ClassNodeViewModelTests.cs
git commit -m "Add ClassNodeViewModel with member compartments

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 14: `AddClassNode`; wire `ClassNodeViewModel` into `RebuildNodes`

**Files:**
- Modify: `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`
- Test: `tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs`

- [ ] **Step 1: Write the failing tests** (append)

```csharp
    [Fact]
    public void AddClassNode_AddsSelectedClassNode_AndMarksModified()
    {
        DiagramDocumentViewModel doc = CreateDocument();

        ClassNodeViewModel node = doc.AddClassNode(ClassNodeKind.Interface, new Point2D(200, 150));

        Assert.Same(node, Assert.Single(doc.Nodes));
        Assert.True(node.IsSelected);
        Assert.True(doc.IsModified);
        Assert.Equal(ClassNodeKind.Interface, node.Kind);
    }

    [Fact]
    public void ClassNode_SurvivesUndoRedo_AsClassNodeViewModel()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.AddClassNode(ClassNodeKind.Class, new Point2D(200, 150));

        doc.Undo();
        Assert.Empty(doc.Nodes);

        doc.Redo();
        ClassNodeViewModel node = Assert.IsType<ClassNodeViewModel>(Assert.Single(doc.Nodes));
        Assert.Equal(ClassNodeKind.Class, node.Kind);
    }
```
Also strengthen the Task 11 suggestion test now that `AddClassNode` exists (replace the placeholder with the real one):
```csharp
    [Fact]
    public void GetTypeSuggestions_IncludesClassNames()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ClassNodeViewModel node = doc.AddClassNode(ClassNodeKind.Class, new Point2D(100, 100));
        node.Name = "Account";

        Assert.Contains("Account", doc.GetTypeSuggestions());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `AddClassNode` not defined.

- [ ] **Step 3: Implement `AddClassNode` and complete `CreateNodeViewModel`**

Add the constants and method near `AddShape`:
```csharp
    private const double ClassNodeDefaultWidth = 170d;
    private const double ClassNodeDefaultHeight = 110d;

    public ClassNodeViewModel AddClassNode(ClassNodeKind kind, Point2D center)
    {
        CaptureUndo();

        double w = ClassNodeDefaultWidth;
        double h = ClassNodeDefaultHeight;
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        ClassNode node = new()
        {
            Kind = kind,
            Name = DefaultClassName(kind),
            Bounds = bounds,
            Style = _document.DefaultShapeStyle.Clone(),
            ZIndex = NextZIndex(),
        };

        _document.Nodes.Add(node);
        ClassNodeViewModel vm = new(node, this);
        Nodes.Add(vm);
        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    private static string DefaultClassName(ClassNodeKind kind) => kind switch
    {
        ClassNodeKind.Interface => "Interface",
        ClassNodeKind.Enum => "Enumeration",
        _ => "Class",
    };
```

Add the `ClassNode` arm to `CreateNodeViewModel` (from Task 9):
```csharp
    private NodeViewModelBase CreateNodeViewModel(NodeBase node) => node switch
    {
        ClassNode @class => new ClassNodeViewModel(@class, this),
        ShapeNode shape => new ShapeNodeViewModel(shape),
        _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}"),
    };
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS (undo/redo reconstructs the class node via `CreateNodeViewModel`).

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/ViewModels/DiagramDocumentViewModel.cs tests/Draw.App.Tests/DiagramDocumentViewModelTests.cs
git commit -m "Add class-node creation and undo/redo reconstruction

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase E — Toolbox (`Draw.App`)

## Task 15: `ClassNodeToolItem` + toolbox state

**Files:**
- Modify: `src/Draw.App/ViewModels/ToolboxViewModel.cs`
- Test: `tests/Draw.App.Tests/ToolboxViewModelTests.cs` (create)

- [ ] **Step 1: Write the failing test**

`tests/Draw.App.Tests/ToolboxViewModelTests.cs`:
```csharp
using System.Linq;
using Draw.App.ViewModels;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.App.Tests;

public class ToolboxViewModelTests
{
    [Fact]
    public void HasClassNodeTools_ForEachKind()
    {
        ToolboxViewModel toolbox = new();
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Class);
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Interface);
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Enum);
    }

    [Fact]
    public void SelectingClassNode_ClearsShapeAndConnector_AndSetsMode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedShape = toolbox.Shapes.First();

        toolbox.SelectedClassNode = toolbox.ClassNodes.First();

        Assert.Null(toolbox.SelectedShape);
        Assert.Null(toolbox.SelectedConnector);
        Assert.True(toolbox.IsClassNodeMode);
        Assert.False(toolbox.IsSelectTool);
    }

    [Fact]
    public void SelectingShape_ClearsClassNode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedClassNode = toolbox.ClassNodes.First();

        toolbox.SelectedShape = toolbox.Shapes.First();

        Assert.Null(toolbox.SelectedClassNode);
        Assert.False(toolbox.IsClassNodeMode);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — `ClassNodes`/`SelectedClassNode`/`IsClassNodeMode` not defined.

- [ ] **Step 3: Implement**

In `ToolboxViewModel.cs`:
- Add the record near the others:
```csharp
/// <summary>A selectable class-diagram node entry in the toolbox palette.</summary>
public sealed record ClassNodeToolItem(string Name, ClassNodeKind Kind);
```
- Add the collection:
```csharp
    public ObservableCollection<ClassNodeToolItem> ClassNodes { get; } = new()
    {
        new ClassNodeToolItem("Class", ClassNodeKind.Class),
        new ClassNodeToolItem("Interface", ClassNodeKind.Interface),
        new ClassNodeToolItem("Enum", ClassNodeKind.Enum),
    };
```
- Add the selection property:
```csharp
    public ClassNodeToolItem? SelectedClassNode
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
                }

                RaiseModes();
            }
        }
    }
```
- In the existing `SelectedShape` setter, add `SelectedClassNode = null;` inside the `if (value is not null)` block (alongside `SelectedConnector = null;`). Do the same in the `SelectedConnector` setter (add `SelectedClassNode = null;`).
- Update mode flags:
```csharp
    public bool IsSelectTool => SelectedShape is null && SelectedConnector is null && SelectedClassNode is null;

    public bool IsClassNodeMode => SelectedClassNode is not null;
```
- In `ActivateSelectTool`, add `SelectedClassNode = null;`.
- In `RaiseModes`, add `OnPropertyChanged(nameof(IsClassNodeMode));`.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/ViewModels/ToolboxViewModel.cs tests/Draw.App.Tests/ToolboxViewModelTests.cs
git commit -m "Add class-node tools to the toolbox

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 16: Place class nodes on canvas; toolbox palette UI

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml.cs` (placement)
- Modify: `src/Draw.App/Views/MainWindow.axaml` (palette section)

No unit harness for pointer input — verified by build + manual run.

- [ ] **Step 1: Add class placement to `OnPointerPressed`**

In `DiagramView.axaml.cs`, in `OnPointerPressed`, immediately after the shape-placement block (the `if (toolbox?.SelectedShape is { } tool)` block ending around line 230), add:
```csharp
        // Class-node placement.
        if (toolbox?.SelectedClassNode is { } classTool)
        {
            _vm.AddClassNode(classTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }
```

- [ ] **Step 2: Add the palette section to `MainWindow.axaml`**

In the Toolbox `StackPanel` (after the Connectors `ListBox`, before the closing `</StackPanel>` around line 110), add:
```xml
                        <TextBlock Text="Class diagram" Opacity="0.7" Margin="0,6,0,0" />
                        <ListBox ItemsSource="{Binding Toolbox.ClassNodes}"
                                 SelectedItem="{Binding Toolbox.SelectedClassNode, Mode=TwoWay}">
                            <ListBox.ItemTemplate>
                                <DataTemplate x:DataType="vm:ClassNodeToolItem">
                                    <TextBlock Text="{Binding Name}" />
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>
```

- [ ] **Step 3: Build**

Run: `dotnet build Draw.slnx`
Expected: succeeds.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Draw.App/Draw.App.csproj`
Confirm: selecting "Class"/"Interface"/"Enum" then clicking the canvas places a compartment box (it will render as a plain box until Task 20), it is selected, and it is undoable. (Selecting a class tool must clear any shape/connector tool selection.)

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/Views/DiagramView.axaml.cs src/Draw.App/Views/MainWindow.axaml
git commit -m "Place class nodes from the toolbox

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase F — Inspector (`Draw.App`)

## Task 17: Inspector third mode + member-editor commands

**Files:**
- Modify: `src/Draw.App/ViewModels/InspectorViewModel.cs`
- Test: `tests/Draw.App.Tests/InspectorViewModelTests.cs`

- [ ] **Step 1: Write the failing tests** (append)

```csharp
    private static DiagramDocumentViewModel CreateDocumentWithClass(out ClassNodeViewModel node)
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.Class),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        node = doc.AddClassNode(ClassNodeKind.Class, new Point2D(100, 100));
        return doc;
    }

    [Fact]
    public void SelectingClassNode_ReportsClassMode_AndLoadsSuggestions()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        Assert.True(inspector.IsClassNodeSelected);
        Assert.False(inspector.IsShapeSelected);
        Assert.Same(node, inspector.SelectedClassNode);
        Assert.Contains("string", inspector.TypeSuggestions);
    }

    [Fact]
    public void AddPrimaryMemberCommand_AddsMemberToSelectedClass()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.AddPrimaryMemberCommand.Execute(null);

        Assert.Single(node.PrimaryMembers);
    }

    [Fact]
    public void FillHex_AppliesToSelectedClassNode()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.FillHex = "#FF112233";

        Assert.Equal(ArgbColor.FromRgb(0x11, 0x22, 0x33), node.Model.Style.Fill);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: FAIL — class-mode members not defined.

- [ ] **Step 3: Implement inspector changes**

In `InspectorViewModel.cs`:
- Add usings: `using CommunityToolkit.Mvvm.Input;` and `using Draw.Model.Nodes;`.
- Add the visibility options static list near the others:
```csharp
    public static IReadOnlyList<MemberVisibility> VisibilityOptions { get; } =
        Enum.GetValues<MemberVisibility>();
```
- Add the third-mode flag and helpers (note both raised properties in the setter):
```csharp
    public bool IsClassNodeSelected
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

    public bool IsNodeSelected => IsShapeSelected || IsClassNodeSelected;

    public ClassNodeViewModel? SelectedClassNode
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public IReadOnlyList<string> TypeSuggestions
    {
        get;
        private set => SetProperty(ref field, value);
    } = System.Array.Empty<string>();
```
- Update `HasNoSelection`:
```csharp
    public bool HasNoSelection => !IsShapeSelected && !IsConnectorSelected && !IsClassNodeSelected;
```
- Update the **existing** `IsShapeSelected` setter so it also raises `IsNodeSelected` (it currently raises only `HasNoSelection`):
```csharp
    public bool IsShapeSelected
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
(`IsConnectorSelected` does not affect `IsNodeSelected`, so leave its setter as-is.)
- Add the member-editor commands (place after the connector properties):
```csharp
    public IRelayCommand AddPrimaryMemberCommand { get; }

    public IRelayCommand AddOperationCommand { get; }

    public IRelayCommand<ClassMemberViewModel> RemoveMemberCommand { get; }

    public IRelayCommand<ClassMemberViewModel> MoveMemberUpCommand { get; }

    public IRelayCommand<ClassMemberViewModel> MoveMemberDownCommand { get; }

    public InspectorViewModel()
    {
        AddPrimaryMemberCommand = new RelayCommand(() => SelectedClassNode?.AddPrimaryMember());
        AddOperationCommand = new RelayCommand(() => SelectedClassNode?.AddOperation());
        RemoveMemberCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.RemoveMember(m); });
        MoveMemberUpCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.MoveMember(m, -1); });
        MoveMemberDownCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.MoveMember(m, +1); });
    }
```
- Rework `LoadFromSelection` to treat any node for shared style and branch for shape/class:
```csharp
    private void LoadFromSelection()
    {
        _loading = true;
        try
        {
            ConnectorViewModel? connector = _target?.SelectedConnector;
            NodeViewModelBase? node = connector is null ? _target?.SelectedNodes.FirstOrDefault() : null;
            ShapeNodeViewModel? shape = node as ShapeNodeViewModel;
            ClassNodeViewModel? klass = node as ClassNodeViewModel;

            IsConnectorSelected = connector is not null;
            IsShapeSelected = shape is not null;
            IsClassNodeSelected = klass is not null;
            SelectedClassNode = klass;

            if (connector is not null)
            {
                Connector model = connector.Model;
                ConnectorKind = model.Kind;
                ConnectorRouteStyle = model.Route;
                ConnectorStrokeHex = model.Style.Stroke.Color.ToHex();
                ConnectorThickness = model.Style.Stroke.Thickness;
                SourceLabel = model.SourceLabel ?? string.Empty;
                CenterLabel = model.CenterLabel ?? string.Empty;
                TargetLabel = model.TargetLabel ?? string.Empty;
            }
            else if (node is not null)
            {
                ModelStyle.ShapeStyle style = node.Model.Style;
                FillHex = style.Fill.ToHex();
                StrokeHex = style.Stroke.Color.ToHex();
                StrokeThickness = style.Stroke.Thickness;
                FontSize = style.Font.Size;
                Bold = style.Font.Bold;
                Italic = style.Font.Italic;
                Alignment = style.TextAlignment;

                if (shape is not null)
                {
                    Text = shape.Model.Text;
                }

                if (klass is not null)
                {
                    TypeSuggestions = _target!.GetTypeSuggestions();
                }
            }
        }
        finally
        {
            _loading = false;
        }
    }
```
- Generalize the shared style apply path to the base type:
  - `ApplyShapeStyle`: change `List<ShapeNodeViewModel> selected = _target.SelectedNodes.ToList();` to `List<NodeViewModelBase> selected = _target.SelectedNodes.ToList();` and the `foreach` variable to `NodeViewModelBase`. `node.Model.Style` and `node.RaiseStyleChanged()` exist on the base.
  - `ApplyText`: keep capturing undo, but apply only to shape nodes:
```csharp
    private void ApplyText()
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<ShapeNodeViewModel> selected = _target.SelectedNodes.OfType<ShapeNodeViewModel>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (ShapeNodeViewModel node in selected)
        {
            node.Text = Text;
        }

        _target.MarkModified();
    }
```

Note: the existing field initializers on `InspectorViewModel` properties (e.g. `Text = string.empty`, `FillHex = "#FFFFFFFF"`) remain; adding an explicit constructor is fine alongside C# field initializers.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --project tests/Draw.App.Tests/Draw.App.Tests.csproj`
Expected: PASS (including the existing shape inspector tests).

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/ViewModels/InspectorViewModel.cs tests/Draw.App.Tests/InspectorViewModelTests.cs
git commit -m "Add class-node inspector mode and member-editor commands

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 18: Inspector AXAML — shared style for nodes + class member editor

**Files:**
- Modify: `src/Draw.App/Views/MainWindow.axaml`

Build + manual verification (no AXAML unit harness).

- [ ] **Step 1: Make the shared style block visible for any node**

The existing "Shape properties" `StackPanel` (currently `IsVisible="{Binding Inspector.IsShapeSelected}"`, lines ~145-182) contains the `Text` field plus the shared style fields. Split it:
- Keep a shape-only panel for the `Text` field: `IsVisible="{Binding Inspector.IsShapeSelected}"` wrapping only the Text `StackPanel`.
- Move the Fill/Stroke/Thickness/FontSize/Bold/Italic/Alignment fields into a panel `IsVisible="{Binding Inspector.IsNodeSelected}"` (so they apply to both shapes and class nodes — `ApplyShapeStyle` now operates on the base type).

Concretely, replace the single shape panel with:
```xml
                        <!-- Text (shapes only) -->
                        <StackPanel Spacing="10" IsVisible="{Binding Inspector.IsShapeSelected}">
                            <StackPanel Spacing="3">
                                <TextBlock Text="Text" Opacity="0.7" />
                                <TextBox Text="{Binding Inspector.Text, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" AcceptsReturn="True" />
                            </StackPanel>
                        </StackPanel>

                        <!-- Shared node style (shapes and class nodes) -->
                        <StackPanel Spacing="10" IsVisible="{Binding Inspector.IsNodeSelected}">
                            <StackPanel Spacing="3">
                                <TextBlock Text="Fill (#AARRGGBB)" Opacity="0.7" />
                                <TextBox Text="{Binding Inspector.FillHex, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                            </StackPanel>
                            <StackPanel Spacing="3">
                                <TextBlock Text="Stroke (#AARRGGBB)" Opacity="0.7" />
                                <TextBox Text="{Binding Inspector.StrokeHex, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                            </StackPanel>
                            <StackPanel Spacing="3">
                                <TextBlock Text="Stroke thickness" Opacity="0.7" />
                                <TextBox Text="{Binding Inspector.StrokeThickness, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                            </StackPanel>
                            <StackPanel Spacing="3">
                                <TextBlock Text="Font size" Opacity="0.7" />
                                <TextBox Text="{Binding Inspector.FontSize, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                            </StackPanel>
                            <StackPanel Orientation="Horizontal" Spacing="14">
                                <CheckBox Content="Bold" IsChecked="{Binding Inspector.Bold, Mode=TwoWay}" />
                                <CheckBox Content="Italic" IsChecked="{Binding Inspector.Italic, Mode=TwoWay}" />
                            </StackPanel>
                            <StackPanel Spacing="3">
                                <TextBlock Text="Text alignment" Opacity="0.7" />
                                <ComboBox HorizontalAlignment="Stretch"
                                          ItemsSource="{x:Static vm:InspectorViewModel.AlignmentOptions}"
                                          SelectedItem="{Binding Inspector.Alignment, Mode=TwoWay}" />
                            </StackPanel>
                        </StackPanel>
```

- [ ] **Step 2: Add the class member editor panel**

Immediately after the shared style panel, add (DataContext of the inner editors is `SelectedClassNode` / member VMs; the type `AutoCompleteBox` binds its own `TypeSuggestions`):
```xml
                        <!-- Class node properties -->
                        <StackPanel Spacing="10" IsVisible="{Binding Inspector.IsClassNodeSelected}"
                                    DataContext="{Binding Inspector.SelectedClassNode}">
                            <StackPanel Spacing="3">
                                <TextBlock Text="Name" Opacity="0.7" />
                                <TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                            </StackPanel>
                            <CheckBox Content="Abstract" IsChecked="{Binding IsAbstract, Mode=TwoWay}" />

                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <TextBlock Text="Members" FontWeight="SemiBold" VerticalAlignment="Center" />
                                <Button Content="+ Field/Literal"
                                        Command="{Binding $parent[Window].((vm:ShellViewModel)DataContext).Inspector.AddPrimaryMemberCommand}" />
                                <Button Content="+ Operation" IsVisible="{Binding HasOperations}"
                                        Command="{Binding $parent[Window].((vm:ShellViewModel)DataContext).Inspector.AddOperationCommand}" />
                            </StackPanel>

                            <ItemsControl ItemsSource="{Binding PrimaryMembers}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate x:DataType="vm:ClassMemberViewModel">
                                        <StackPanel Spacing="2" Margin="0,0,0,6">
                                            <StackPanel Orientation="Horizontal" Spacing="4">
                                                <ComboBox Width="56" ItemsSource="{x:Static vm:InspectorViewModel.VisibilityOptions}"
                                                          SelectedItem="{Binding Visibility, Mode=TwoWay}" />
                                                <TextBox Width="90" Watermark="name"
                                                         Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                                            </StackPanel>
                                            <StackPanel Orientation="Horizontal" Spacing="4">
                                                <AutoCompleteBox Width="120" Watermark="type"
                                                                 ItemsSource="{Binding TypeSuggestions}"
                                                                 Text="{Binding Type, Mode=TwoWay}" />
                                                <CheckBox Content="stat" IsChecked="{Binding IsStatic, Mode=TwoWay}" />
                                                <Button Content="✕"
                                                        Command="{Binding $parent[Window].((vm:ShellViewModel)DataContext).Inspector.RemoveMemberCommand}"
                                                        CommandParameter="{Binding}" />
                                            </StackPanel>
                                        </StackPanel>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>

                            <TextBlock Text="Operations" Opacity="0.7" IsVisible="{Binding HasOperations}" />
                            <ItemsControl ItemsSource="{Binding Operations}" IsVisible="{Binding HasOperations}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate x:DataType="vm:ClassMemberViewModel">
                                        <StackPanel Spacing="2" Margin="0,0,0,6">
                                            <StackPanel Orientation="Horizontal" Spacing="4">
                                                <ComboBox Width="56" ItemsSource="{x:Static vm:InspectorViewModel.VisibilityOptions}"
                                                          SelectedItem="{Binding Visibility, Mode=TwoWay}" />
                                                <TextBox Width="90" Watermark="name"
                                                         Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                                            </StackPanel>
                                            <StackPanel Orientation="Horizontal" Spacing="4">
                                                <TextBox Width="86" Watermark="params"
                                                         Text="{Binding Parameters, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
                                                <AutoCompleteBox Width="80" Watermark="ret"
                                                                 ItemsSource="{Binding TypeSuggestions}"
                                                                 Text="{Binding Type, Mode=TwoWay}" />
                                                <Button Content="✕"
                                                        Command="{Binding $parent[Window].((vm:ShellViewModel)DataContext).Inspector.RemoveMemberCommand}"
                                                        CommandParameter="{Binding}" />
                                            </StackPanel>
                                        </StackPanel>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </StackPanel>
```

Notes:
- The `$parent[Window].((vm:ShellViewModel)DataContext).Inspector.<Command>` path reaches the command on the inspector while the local DataContext is the class node / member VM. This mirrors the existing pattern used for `OpenRecentCommand` and the tab close button in this same file.
- Move up/down buttons are omitted from the MVP UI to keep rows compact (the `MoveMemberUp/DownCommand` exist on the VM for a later pass — note this as a deliberate, logged omission, not a silent cap).

- [ ] **Step 3: Build**

Run: `dotnet build Draw.slnx`
Expected: succeeds (compiled XAML validates all bindings and `x:DataType`).

- [ ] **Step 4: Manual verification**

Run the app; select a placed class node and confirm: Name edits, Abstract toggles, "+ Field/Literal" and "+ Operation" add rows, visibility/name/type/params edit and persist, "✕" removes a row, and each edit is undoable. Fill/stroke/font apply to the class box.

- [ ] **Step 5: Commit**

```bash
git add src/Draw.App/Views/MainWindow.axaml
git commit -m "Add class-node inspector UI with member editor

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase G — Rendering + inline member editing (`Draw.App`)

## Task 19: Node DataTemplates — shape + class compartment box

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml`

Build + manual verification.

- [ ] **Step 1: Replace the single `ItemTemplate` with two implicit `DataTemplates`**

In the nodes `ItemsControl` (lines 55-102), keep `ItemsPanel` and the `ContentPresenter` `Canvas.Left/Top` style. Replace the `<ItemsControl.ItemTemplate>…</ItemsControl.ItemTemplate>` block with a `<ItemsControl.DataTemplates>` block holding the existing shape template **and** a new class template:
```xml
                <ItemsControl.DataTemplates>
                    <DataTemplate DataType="vm:ShapeNodeViewModel">
                        <Panel Width="{Binding Width}" Height="{Binding Height}">
                            <Path Data="{Binding Geometry}"
                                  Fill="{Binding Fill}"
                                  Stroke="{Binding Stroke}"
                                  StrokeThickness="{Binding StrokeThickness}"
                                  StrokeDashArray="{Binding StrokeDashArray}" />
                            <TextBlock Text="{Binding Text}"
                                       Foreground="{Binding Foreground}"
                                       FontFamily="{Binding FontFamily}"
                                       FontSize="{Binding FontSize}"
                                       FontWeight="{Binding FontWeight}"
                                       FontStyle="{Binding FontStyle}"
                                       TextAlignment="{Binding TextAlignment}"
                                       TextWrapping="Wrap"
                                       VerticalAlignment="Center"
                                       Margin="6"
                                       IsVisible="{Binding !IsEditing}" />
                            <TextBox x:Name="EditBox"
                                     Text="{Binding Text, Mode=TwoWay}"
                                     FontFamily="{Binding FontFamily}"
                                     FontSize="{Binding FontSize}"
                                     AcceptsReturn="True"
                                     TextWrapping="Wrap"
                                     Background="Transparent"
                                     IsVisible="{Binding IsEditing}" />
                            <Rectangle Stroke="#3D7EFF" StrokeThickness="1.5"
                                       StrokeDashArray="3,2"
                                       Fill="Transparent"
                                       IsHitTestVisible="False"
                                       IsVisible="{Binding IsSelected}" />
                        </Panel>
                    </DataTemplate>

                    <DataTemplate DataType="vm:ClassNodeViewModel">
                        <Panel Width="{Binding Width}" Height="{Binding Height}">
                            <Border Background="{Binding Fill}"
                                    BorderBrush="{Binding Stroke}"
                                    BorderThickness="{Binding StrokeThickness}">
                                <Grid RowDefinitions="Auto,Auto,*,Auto,Auto">
                                    <StackPanel Grid.Row="0" Margin="6,4" HorizontalAlignment="Center">
                                        <TextBlock Text="{Binding Stereotype}" FontSize="10" FontStyle="Italic"
                                                   HorizontalAlignment="Center"
                                                   IsVisible="{Binding HasStereotype}" />
                                        <TextBlock Text="{Binding Name}" FontWeight="Bold"
                                                   Foreground="{Binding Foreground}"
                                                   HorizontalAlignment="Center" />
                                    </StackPanel>
                                    <Rectangle Grid.Row="1" Height="1" Fill="{Binding Stroke}" />
                                    <ItemsControl Grid.Row="2" ItemsSource="{Binding PrimaryMembers}" Margin="6,2">
                                        <ItemsControl.ItemTemplate>
                                            <DataTemplate x:DataType="vm:ClassMemberViewModel">
                                                <Panel>
                                                    <TextBlock Text="{Binding DisplayText}" FontSize="11"
                                                               Foreground="{Binding $parent[ItemsControl].((vm:ClassNodeViewModel)DataContext).Foreground}"
                                                               FontStyle="{Binding RowFontStyle}"
                                                               TextDecorations="{Binding RowDecorations}"
                                                               IsVisible="{Binding !IsEditing}"
                                                               DoubleTapped="OnMemberDoubleTapped" />
                                                    <TextBox Text="{Binding RawText, Mode=TwoWay}" FontSize="11"
                                                             Background="Transparent" Padding="0"
                                                             IsVisible="{Binding IsEditing}"
                                                             LostFocus="OnMemberEditCommitted" />
                                                </Panel>
                                            </DataTemplate>
                                        </ItemsControl.ItemTemplate>
                                    </ItemsControl>
                                    <Rectangle Grid.Row="3" Height="1" Fill="{Binding Stroke}"
                                               IsVisible="{Binding HasOperations}" />
                                    <ItemsControl Grid.Row="4" ItemsSource="{Binding Operations}" Margin="6,2"
                                                  IsVisible="{Binding HasOperations}">
                                        <ItemsControl.ItemTemplate>
                                            <DataTemplate x:DataType="vm:ClassMemberViewModel">
                                                <Panel>
                                                    <TextBlock Text="{Binding DisplayText}" FontSize="11"
                                                               FontStyle="{Binding RowFontStyle}"
                                                               TextDecorations="{Binding RowDecorations}"
                                                               IsVisible="{Binding !IsEditing}"
                                                               DoubleTapped="OnMemberDoubleTapped" />
                                                    <TextBox Text="{Binding RawText, Mode=TwoWay}" FontSize="11"
                                                             Background="Transparent" Padding="0"
                                                             IsVisible="{Binding IsEditing}"
                                                             LostFocus="OnMemberEditCommitted" />
                                                </Panel>
                                            </DataTemplate>
                                        </ItemsControl.ItemTemplate>
                                    </ItemsControl>
                                </Grid>
                            </Border>
                            <Rectangle Stroke="#3D7EFF" StrokeThickness="1.5"
                                       StrokeDashArray="3,2"
                                       Fill="Transparent"
                                       IsHitTestVisible="False"
                                       IsVisible="{Binding IsSelected}" />
                        </Panel>
                    </DataTemplate>
                </ItemsControl.DataTemplates>
```

Notes:
- Avalonia renders `ItemsControl` items using its `DataTemplates` (matched by `DataType`) when no `ItemTemplate` is set — so removing `ItemTemplate` and supplying both templates is the mechanism for two node kinds.
- The member-row `Foreground` binding to the parent class node is best-effort styling; if the compiled binding to `$parent[ItemsControl]…` proves awkward, drop the `Foreground` setter on the row (rows then use the default foreground) — it is cosmetic.
- `OnMemberDoubleTapped` / `OnMemberEditCommitted` are wired in Task 20; the build in this task will fail to resolve those handlers, so **do Task 20's code-behind edit in the same cycle**, then build once and commit Tasks 19+20 together (see Task 20).

- [ ] **Step 2: Proceed to Task 20, then build.** (Tasks 19 and 20 are one compile unit because the AXAML references the handlers.)

---

## Task 20: Member-row double-tap + commit handlers; extend `EndEditing`

**Files:**
- Modify: `src/Draw.App/Views/DiagramView.axaml.cs`

- [ ] **Step 1: Add the handlers**

Add to `DiagramView.axaml.cs` (anywhere in the class; they are referenced by the class templates). Ensure `using Avalonia.Interactivity;` is present for `RoutedEventArgs`:
```csharp
    private void OnMemberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        if ((sender as Control)?.DataContext is ClassMemberViewModel member)
        {
            _vm.CaptureUndo();
            member.BeginEdit();
            e.Handled = true;

            // Focus the sibling editor so typing starts immediately.
            if ((sender as Control)?.Parent is Panel panel)
            {
                panel.Children.OfType<TextBox>().FirstOrDefault()?.Focus();
            }
        }
    }

    private void OnMemberEditCommitted(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        if ((sender as Control)?.DataContext is ClassMemberViewModel { IsEditing: true } member)
        {
            member.CommitEdit();
            _vm.MarkModified();
        }
    }
```

- [ ] **Step 2: Extend `EndEditing` to commit class member edits**

Replace `EndEditing`:
```csharp
    private void EndEditing()
    {
        if (_vm is null)
        {
            return;
        }

        foreach (ShapeNodeViewModel node in _vm.Nodes.OfType<ShapeNodeViewModel>().Where(n => n.IsEditing))
        {
            node.IsEditing = false;
        }

        bool committed = false;
        foreach (ClassNodeViewModel klass in _vm.Nodes.OfType<ClassNodeViewModel>())
        {
            committed |= klass.CommitPendingEdits();
        }

        if (committed)
        {
            _vm.MarkModified();
        }
    }
```

- [ ] **Step 3: Build (completes Tasks 19+20 compile unit)**

Run: `dotnet build Draw.slnx`
Expected: succeeds.

- [ ] **Step 4: Run all tests** (ensure no regressions)

Run: `dotnet test --solution Draw.slnx`
Expected: PASS.

- [ ] **Step 5: Manual verification**

Run the app and verify:
- A placed Class renders as a compartment box: name (bold; italic if abstract), attributes compartment, operations compartment with divider lines.
- Interface shows `«interface»`; Enum shows `«enumeration»` and a single literals compartment with no operations.
- Double-tapping a member row opens an inline editor seeded with `+ name: Type`; editing it and clicking away (or Esc) parses it back (e.g. typing `+ pay(amount: decimal): bool` turns a field row into an operation if it moves compartments on reselect — note: kind change relocates the member only after the collections rebuild on reselect; acceptable for MVP).
- Inline edits are undoable.

- [ ] **Step 6: Commit (covers Tasks 19+20)**

```bash
git add src/Draw.App/Views/DiagramView.axaml src/Draw.App/Views/DiagramView.axaml.cs
git commit -m "Render class compartment nodes with inline member editing

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase H — End-to-end verification & docs

## Task 21: Full verification, connectors-to-class check, docs

**Files:**
- Modify: `documentation/roadmap.md`
- Modify: `documentation/architecture.md` (only if it enumerates node/VM types — add `ClassNode`/`NodeViewModelBase`)

- [ ] **Step 1: Full suite green**

Run: `dotnet test --solution Draw.slnx`
Expected: PASS. Record the passing test count.

- [ ] **Step 2: Manual end-to-end run** (Linux/WSL: ensure fontconfig installed)

Run: `dotnet run --project src/Draw.App/Draw.App.csproj`
Checklist (note any failures honestly):
- Place a Class, an Interface, and an Enum; add fields/operations/literals via the inspector.
- Draw a Generalization from one class to another (Phase 2 connector tool) — confirm the line attaches to the rectangular class-box boundary and re-routes when a class node is moved/resized.
- Draw a Realization to an Interface, a Dependency, and an Association.
- Resize a class node — confirm it cannot shrink below its member content (per-node `MinHeight`).
- Save to `.draw`, close, reopen — confirm class nodes, members, flags and connectors round-trip.
- Undo/redo across creation, member edits, and connector creation.
- Toggle theme — confirm class box fill/stroke/text adapt.

- [ ] **Step 3: Update roadmap**

In `documentation/roadmap.md`, mark Phase 3 done and move the "(current)" marker. Replace the Phase 3 section heading line:
```markdown
## Phase 3 — UML class diagrams ✅
```
and add a one-line "current" note to Phase 4 consistent with how Phase 2 was marked.

- [ ] **Step 4: Commit**

```bash
git add documentation/roadmap.md documentation/architecture.md
git commit -m "Mark Phase 3 complete in docs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 5: Finish the branch**

Use the superpowers:finishing-a-development-branch skill to decide merge/PR. Do not merge to `main` without the user's go-ahead (the working tree had 94 pre-existing modified files at branch start — confirm with the user how those should be handled before any merge).

---

## Notes, deviations from the spec, and logged omissions

- **Deviation (better):** No `Draw.Diagramming` change for class-node routing. `NodeViewModelBase.BoundaryKind` returns `ShapeKind.Rectangle` for class nodes, so existing rectangle attachment is reused. (Spec §6 anticipated a `ShapeOutline`/`ShapeBoundary` change — unnecessary.)
- **Deviation (fits codebase):** `ClassMember` is a mutable `sealed class` (like `ShapeStyle`), not a record — enables field-by-field write-through from the inspector.
- **Deviation (simpler):** Parser is `MemberSignature.Parse` (always returns a best-effort member) rather than `TryParse`; there is no "empty means delete" path because add/remove is inspector-driven (per the approved Q4 answer).
- **Consolidation:** undo/dirty/autocomplete coupling for members goes through one `INodeEditContext` implemented by `DiagramDocumentViewModel`.
- **Logged omission:** member up/down reordering commands exist on `ClassNodeViewModel`/`InspectorViewModel` but are not surfaced in the inspector UI in this pass (kept rows compact). Surface them in a follow-up.
- **Logged limitation:** changing a member's kind via inline edit (field↔operation) relocates it to the correct compartment only after the node's collections rebuild (e.g. on reselect/reopen), since `PrimaryMembers`/`Operations` are built at construction. Acceptable for MVP; a live re-bucketing pass is a follow-up.
- **Honesty:** AXAML/pointer tasks (10, 16, 18, 19, 20) have no automated UI test in this repo; they are verified by `dotnet build` (compiled-XAML validation) + the manual checklist in Task 21. They are not unit-tested.
```
