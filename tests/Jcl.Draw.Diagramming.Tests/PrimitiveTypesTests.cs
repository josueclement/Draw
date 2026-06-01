using System.Linq;
using Jcl.Draw.Diagramming.Uml;
using Xunit;

namespace Jcl.Draw.Diagramming.Tests;

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
