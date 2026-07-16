using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Enums;

public class RemainingEnumsTests
{
    [Fact]
    public void ZVecLogLevel_HasCorrectValues()
    {
        ((int)ZVecLogLevel.Debug).Should().Be(0);
        ((int)ZVecLogLevel.Info).Should().Be(1);
        ((int)ZVecLogLevel.Warn).Should().Be(2);
        ((int)ZVecLogLevel.Error).Should().Be(3);
        ((int)ZVecLogLevel.Fatal).Should().Be(4);
    }

    [Fact]
    public void ZVecLogType_HasCorrectValues()
    {
        ((int)ZVecLogType.Console).Should().Be(0);
        ((int)ZVecLogType.File).Should().Be(1);
    }

    [Fact]
    public void ZVecOperator_HasCorrectValues()
    {
        ((int)ZVecOperator.Insert).Should().Be(0);
        ((int)ZVecOperator.Upsert).Should().Be(1);
        ((int)ZVecOperator.Update).Should().Be(2);
        ((int)ZVecOperator.Delete).Should().Be(3);
    }

    [Fact]
    public void ZVecCompareOp_HasCorrectValues()
    {
        ((int)ZVecCompareOp.None).Should().Be(0);
        ((int)ZVecCompareOp.Eq).Should().Be(1);
        ((int)ZVecCompareOp.Ne).Should().Be(2);
        ((int)ZVecCompareOp.Lt).Should().Be(3);
        ((int)ZVecCompareOp.Le).Should().Be(4);
        ((int)ZVecCompareOp.Gt).Should().Be(5);
        ((int)ZVecCompareOp.Ge).Should().Be(6);
        ((int)ZVecCompareOp.Like).Should().Be(7);
        ((int)ZVecCompareOp.ContainAll).Should().Be(8);
        ((int)ZVecCompareOp.ContainAny).Should().Be(9);
        ((int)ZVecCompareOp.NotContainAll).Should().Be(10);
        ((int)ZVecCompareOp.NotContainAny).Should().Be(11);
        ((int)ZVecCompareOp.IsNull).Should().Be(12);
        ((int)ZVecCompareOp.IsNotNull).Should().Be(13);
        ((int)ZVecCompareOp.HasPrefix).Should().Be(14);
        ((int)ZVecCompareOp.HasSuffix).Should().Be(15);
    }

    [Fact]
    public void ZVecRelationOp_HasCorrectValues()
    {
        ((int)ZVecRelationOp.None).Should().Be(0);
        ((int)ZVecRelationOp.And).Should().Be(1);
        ((int)ZVecRelationOp.Or).Should().Be(2);
    }

    [Fact]
    public void ZVecColumnOp_HasCorrectValues()
    {
        ((int)ZVecColumnOp.Undefined).Should().Be(0);
        ((int)ZVecColumnOp.Add).Should().Be(1);
        ((int)ZVecColumnOp.Alter).Should().Be(2);
        ((int)ZVecColumnOp.Drop).Should().Be(3);
    }

    [Fact]
    public void ZVecBlockType_HasCorrectValues()
    {
        ((int)ZVecBlockType.Undefined).Should().Be(0);
        ((int)ZVecBlockType.Scalar).Should().Be(1);
        ((int)ZVecBlockType.ScalarIndex).Should().Be(2);
        ((int)ZVecBlockType.VectorIndex).Should().Be(3);
        ((int)ZVecBlockType.VectorIndexQuantize).Should().Be(4);
        ((int)ZVecBlockType.FtsIndex).Should().Be(5);
    }

    [Fact]
    public void ZVecFileFormat_HasCorrectValues()
    {
        ((int)ZVecFileFormat.Unknown).Should().Be(0);
        ((int)ZVecFileFormat.Ipc).Should().Be(1);
        ((int)ZVecFileFormat.Parquet).Should().Be(2);
    }

    [Fact]
    public void ZVecFtsDefaultOperator_HasCorrectValues()
    {
        ((int)ZVecFtsDefaultOperator.Or).Should().Be(0);
        ((int)ZVecFtsDefaultOperator.And).Should().Be(1);
        default(ZVecFtsDefaultOperator).Should().Be(ZVecFtsDefaultOperator.Or);
    }
}
