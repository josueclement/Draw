using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Model.Tests;

/// <summary>
/// Enforces the node-kind contract that the compiler can't: every concrete <see cref="NodeBase"/>
/// subtype must have a unique <c>[JsonDerivedType]</c> discriminator and survive a serialization
/// round-trip. Adding a node kind without wiring its discriminator fails here (a test) instead of
/// silently at runtime / on export — the headless-enforceable half of the node-type registry.
/// </summary>
public class NodeKindContractTests
{
    private static readonly Type[] NodeKinds = typeof(NodeBase).Assembly.GetTypes()
        .Where(t => typeof(NodeBase).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

    private static IReadOnlyList<JsonDerivedTypeAttribute> Discriminators
        => typeof(NodeBase).GetCustomAttributes<JsonDerivedTypeAttribute>().ToList();

    [Fact]
    public void EveryConcreteNodeSubtype_HasAJsonDerivedTypeDiscriminator()
    {
        HashSet<Type> declared = Discriminators.Select(a => a.DerivedType).ToHashSet();
        HashSet<Type> concrete = NodeKinds.ToHashSet();

        Assert.True(
            concrete.SetEquals(declared),
            $"NodeBase [JsonDerivedType] set is out of sync with its concrete subclasses. "
            + $"Missing a discriminator: {Names(concrete.Except(declared))}; "
            + $"discriminator for a non-existent/abstract type: {Names(declared.Except(concrete))}.");
    }

    [Fact]
    public void Discriminators_AreUnique()
    {
        List<string> tokens = Discriminators.Select(a => a.TypeDiscriminator?.ToString() ?? string.Empty).ToList();
        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryNodeSubtype_SurvivesSerializationRoundTrip_WithConcreteTypeIntact()
    {
        DiagramDocument document = new();
        foreach (Type kind in NodeKinds)
        {
            document.Nodes.Add((NodeBase)Activator.CreateInstance(kind)!);
        }

        JsonDocumentSerializer serializer = new();
        DiagramDocument restored = serializer.Deserialize(serializer.Serialize(document));

        HashSet<Type> restoredKinds = restored.Nodes.Select(n => n.GetType()).ToHashSet();
        Assert.True(
            restoredKinds.SetEquals(NodeKinds.ToHashSet()),
            $"A node type lost its concrete type through serialization. Round-tripped: {Names(restoredKinds)}.");
    }

    private static string Names(IEnumerable<Type> types)
        => string.Join(", ", types.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
}
