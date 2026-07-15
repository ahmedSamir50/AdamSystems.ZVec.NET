using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Enums;

public class ZVecDataTypeTests
{
    [Fact]
    public void ZVecDataType_HasCorrectValues()
    {
        ((int)ZVecDataType.Undefined).Should().Be(0);
        ((int)ZVecDataType.Binary).Should().Be(1);
        ((int)ZVecDataType.String).Should().Be(2);
        ((int)ZVecDataType.Bool).Should().Be(3);
        ((int)ZVecDataType.Int32).Should().Be(4);
        ((int)ZVecDataType.Int64).Should().Be(5);
        ((int)ZVecDataType.UInt32).Should().Be(6);
        ((int)ZVecDataType.UInt64).Should().Be(7);
        ((int)ZVecDataType.Float).Should().Be(8);
        ((int)ZVecDataType.Double).Should().Be(9);
        ((int)ZVecDataType.VectorBinary32).Should().Be(20);
        ((int)ZVecDataType.VectorBinary64).Should().Be(21);
        ((int)ZVecDataType.VectorFp16).Should().Be(22);
        ((int)ZVecDataType.VectorFp32).Should().Be(23);
        ((int)ZVecDataType.VectorFp64).Should().Be(24);
        ((int)ZVecDataType.VectorInt4).Should().Be(25);
        ((int)ZVecDataType.VectorInt8).Should().Be(26);
        ((int)ZVecDataType.VectorInt16).Should().Be(27);
        ((int)ZVecDataType.SparseVectorFp16).Should().Be(30);
        ((int)ZVecDataType.SparseVectorFp32).Should().Be(31);
        ((int)ZVecDataType.ArrayBinary).Should().Be(40);
        ((int)ZVecDataType.ArrayString).Should().Be(41);
        ((int)ZVecDataType.ArrayBool).Should().Be(42);
        ((int)ZVecDataType.ArrayInt32).Should().Be(43);
        ((int)ZVecDataType.ArrayInt64).Should().Be(44);
        ((int)ZVecDataType.ArrayUInt32).Should().Be(45);
        ((int)ZVecDataType.ArrayUInt64).Should().Be(46);
        ((int)ZVecDataType.ArrayFloat).Should().Be(47);
        ((int)ZVecDataType.ArrayDouble).Should().Be(48);
    }
}
