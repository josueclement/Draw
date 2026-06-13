using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Styling;
using Xunit;

namespace Draw.Model.Tests;

/// <summary>
/// A reflection-based safety net for the hand-written <c>Clone()</c> methods across the model.
/// Every model type lists its fields by hand in <c>Clone()</c>; adding a property and forgetting
/// to clone it silently drops it on copy/paste (paste uses structural <c>Clone()</c>). This test
/// populates every public property of each cloneable type with a non-default value, clones it,
/// and asserts the copy preserves every value <em>and</em> is reference-independent (no shared
/// mutable sub-objects). A new <see cref="NodeBase"/> subtype is covered automatically.
/// </summary>
public class CloneCompletenessTests
{
    public static IEnumerable<object[]> CloneableModelTypes()
    {
        // Auto-discover node kinds: a new NodeBase subtype is covered the moment it is added.
        foreach (Type node in typeof(NodeBase).Assembly.GetTypes()
            .Where(t => typeof(NodeBase).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            yield return new object[] { node };
        }

        // Non-node cloneable types (no shared base) must be listed explicitly. Adding a model type
        // with a hand-written Clone() and a parameterless ctor here keeps it under the net.
        foreach (Type other in new[]
        {
            typeof(ClassMember), typeof(EntityColumn), typeof(Connector),
            typeof(ShapeStyle), typeof(FontSpec), typeof(StrokeStyle),
            typeof(ConnectorStyle), typeof(DocumentMetadata),
        })
        {
            yield return new object[] { other };
        }
    }

    [Theory]
    [MemberData(nameof(CloneableModelTypes))]
    public void Clone_PreservesEveryPublicProperty_AndIsReferenceIndependent(Type type)
    {
        object original = Activator.CreateInstance(type)!;
        Populate(original);

        object copy = InvokeClone(original);

        Assert.Equal(type, copy.GetType()); // Clone returns the same concrete type
        AssertDeepEqual(original, copy, type.Name);
        AssertIndependent(original, copy, type.Name);
    }

    // --- Cloning ---

    private static object InvokeClone(object original)
    {
        MethodInfo? clone = original.GetType().GetMethod("Clone", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        Assert.True(clone is not null, $"{original.GetType().Name} must expose a public parameterless Clone().");
        object? copy = clone!.Invoke(original, null);
        Assert.True(copy is not null, $"{original.GetType().Name}.Clone() returned null.");
        return copy!;
    }

    // --- Population with non-default values ---

    private static void Populate(object instance)
    {
        foreach (PropertyInfo p in WritableProps(instance.GetType()))
        {
            object current = p.GetValue(instance)!;
            p.SetValue(instance, MakeValue(p.PropertyType, current, p.Name));
        }
    }

    private static object MakeValue(Type type, object? current, string name)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(string))
        {
            return "x_" + name;
        }

        if (t == typeof(bool))
        {
            return !(bool)(current ?? false); // flip the (possibly non-false) default so a dropped flag is detectable
        }

        if (t == typeof(int))
        {
            return 7;
        }

        if (t == typeof(long))
        {
            return 7L;
        }

        if (t == typeof(double))
        {
            return 13.5d;
        }

        if (t == typeof(Guid))
        {
            return Guid.NewGuid(); // distinct per property so an Id/Source/Target swap is caught
        }

        if (t == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2021, 6, 11, 8, 30, 0, TimeSpan.Zero);
        }

        if (t == typeof(byte[]))
        {
            return new byte[] { 1, 2, 3 };
        }

        if (t.IsEnum)
        {
            return PickDifferentEnum(t, current);
        }

        if (t == typeof(ArgbColor))
        {
            return new ArgbColor(1, 2, 3, 4);
        }

        if (t == typeof(Point2D))
        {
            return new Point2D(11, 22);
        }

        if (t == typeof(Rect2D))
        {
            return new Rect2D(1, 2, 3, 4);
        }

        if (t == typeof(Size2D))
        {
            return new Size2D(5, 6);
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            return MakeList(t);
        }

        if (IsModelReference(t))
        {
            object nested = Activator.CreateInstance(t)!;
            Populate(nested);
            return nested;
        }

        Assert.Fail($"CloneCompletenessTests has no value generator for property '{name}' of type {t}. Extend MakeValue.");
        return null!; // unreachable
    }

    private static object MakeList(Type listType)
    {
        Type element = listType.GetGenericArguments()[0];
        IList list = (IList)Activator.CreateInstance(listType)!;
        for (int i = 0; i < 2; i++) // two elements so order/count and per-element independence are observable
        {
            if (IsModelReference(element))
            {
                object item = Activator.CreateInstance(element)!;
                Populate(item);
                list.Add(item);
            }
            else if (element == typeof(Point2D))
            {
                list.Add(new Point2D(10 + i, 20 + i)); // distinct per index
            }
            else
            {
                list.Add(MakeValue(element, null, "elem"));
            }
        }

        return list;
    }

    private static object PickDifferentEnum(Type enumType, object? current)
    {
        foreach (object value in Enum.GetValues(enumType))
        {
            if (!Equals(value, current))
            {
                return value;
            }
        }

        return current!; // single-value enum: nothing else to pick
    }

    // --- Deep value comparison ---

    private static void AssertDeepEqual(object? a, object? b, string path)
    {
        if (a is null || b is null)
        {
            Assert.True(a is null && b is null, $"{path}: one side is null, the other is not.");
            return;
        }

        Type t = a.GetType();

        if (IsLeaf(t))
        {
            Assert.True(Equals(a, b), $"{path}: expected {a}, got {b}.");
            return;
        }

        if (a is byte[] ab && b is byte[] bb)
        {
            Assert.Equal(ab, bb);
            return;
        }

        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            List<object?> la = ea.Cast<object?>().ToList();
            List<object?> lb = eb.Cast<object?>().ToList();
            Assert.True(la.Count == lb.Count, $"{path}: list length {la.Count} != {lb.Count}.");
            for (int i = 0; i < la.Count; i++)
            {
                AssertDeepEqual(la[i], lb[i], $"{path}[{i}]");
            }

            return;
        }

        if (IsModelReference(t))
        {
            foreach (PropertyInfo p in ReadableProps(t))
            {
                AssertDeepEqual(p.GetValue(a), p.GetValue(b), $"{path}.{p.Name}");
            }

            return;
        }

        Assert.Fail($"AssertDeepEqual has no comparer for {t} at {path}.");
    }

    // --- Reference independence (the clone must not share mutable sub-objects) ---

    private static void AssertIndependent(object? a, object? b, string path)
    {
        if (a is null || b is null)
        {
            return;
        }

        Type t = a.GetType();
        if (t.IsValueType || t == typeof(string))
        {
            return; // value types and immutable strings need no independent instance
        }

        Assert.False(ReferenceEquals(a, b), $"{path}: clone shares the same instance as the original.");

        if (a is byte[])
        {
            return; // distinctness already asserted above
        }

        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            List<object?> la = ea.Cast<object?>().ToList();
            List<object?> lb = eb.Cast<object?>().ToList();
            for (int i = 0; i < Math.Min(la.Count, lb.Count); i++)
            {
                AssertIndependent(la[i], lb[i], $"{path}[{i}]");
            }

            return;
        }

        if (IsModelReference(t))
        {
            foreach (PropertyInfo p in ReadableProps(t))
            {
                AssertIndependent(p.GetValue(a), p.GetValue(b), $"{path}.{p.Name}");
            }
        }
    }

    // --- Helpers ---

    private static bool IsLeaf(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid)
           || t == typeof(DateTimeOffset) || t == typeof(ArgbColor)
           || t == typeof(Point2D) || t == typeof(Rect2D) || t == typeof(Size2D);

    private static bool IsModelReference(Type t)
        => t.IsClass && t != typeof(string) && !t.IsArray
           && t.Namespace is { } ns && ns.StartsWith("Draw.Model", StringComparison.Ordinal);

    private static IEnumerable<PropertyInfo> WritableProps(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                        && p.SetMethod is { IsPublic: true });

    private static IEnumerable<PropertyInfo> ReadableProps(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
}
