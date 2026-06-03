using System.Linq;
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
