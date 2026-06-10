using System;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.Model.Tests;

public class ImageNodeTests
{
    [Fact]
    public void Clone_CopiesScalarFieldsAndPreservesId()
    {
        ImageNode original = new()
        {
            Data = new byte[] { 1, 2, 3 },
            Format = "jpeg",
            PixelWidth = 640,
            PixelHeight = 480,
        };

        ImageNode copy = Assert.IsType<ImageNode>(original.Clone());

        Assert.Equal(original.Id, copy.Id); // deep clone preserves identity
        Assert.Equal("jpeg", copy.Format);
        Assert.Equal(640, copy.PixelWidth);
        Assert.Equal(480, copy.PixelHeight);
        Assert.Equal(original.Data, copy.Data);
    }

    [Fact]
    public void Clone_DeepCopiesData_SoMutationDoesNotLeakBetweenCopies()
    {
        ImageNode original = new() { Data = new byte[] { 10, 20, 30 } };

        ImageNode copy = (ImageNode)original.Clone();

        Assert.NotSame(original.Data, copy.Data); // distinct array instances
        copy.Data[0] = 99;
        Assert.Equal(10, original.Data[0]);        // original buffer is untouched
    }
}
