using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// 测试 FastEval.Eval&lt;T&gt; 的各种类型转换
/// </summary>
public class FastEvalTypeTests {
    #region 浮点类型测试

    [Fact]
    public void Eval_Double_ReturnsCorrectValue() {
        var result = FastEval.Eval<double>("3.14");
        Assert.Equal(3.14, result, 5);
    }

    [Fact]
    public void Eval_Float_ReturnsCorrectValue() {
        var result = FastEval.Eval<float>("3.14");
        Assert.Equal(3.14f, result, 5);
    }

    [Fact]
    public void Eval_Decimal_ReturnsCorrectValue() {
        var result = FastEval.Eval<decimal>("3.14");
        Assert.Equal(3.14m, result, 5);
    }

    #endregion

    #region 整数类型正常转换测试

    [Fact]
    public void Eval_Long_ReturnsCorrectValue() {
        var result = FastEval.Eval<long>("123456789");
        Assert.Equal(123456789L, result);
    }

    [Fact]
    public void Eval_Int_ReturnsCorrectValue() {
        var result = FastEval.Eval<int>("123");
        Assert.Equal(123, result);
    }

    [Fact]
    public void Eval_Short_ReturnsCorrectValue() {
        var result = FastEval.Eval<short>("123");
        Assert.Equal((short)123, result);
    }

    [Fact]
    public void Eval_Byte_ReturnsCorrectValue() {
        var result = FastEval.Eval<byte>("255");
        Assert.Equal((byte)255, result);
    }

    [Fact]
    public void Eval_SByte_ReturnsCorrectValue() {
        var result = FastEval.Eval<sbyte>("-100");
        Assert.Equal((sbyte)-100, result);
    }

    [Fact]
    public void Eval_UShort_ReturnsCorrectValue() {
        var result = FastEval.Eval<ushort>("60000");
        Assert.Equal((ushort)60000, result);
    }

    [Fact]
    public void Eval_UInt_ReturnsCorrectValue() {
        var result = FastEval.Eval<uint>("3000000000");
        Assert.Equal(3000000000u, result);
    }

    [Fact]
    public void Eval_ULong_ReturnsCorrectValue() {
        // double 精度限制，使用较小的值测试
        var result = FastEval.Eval<ulong>("9000000000000000");
        Assert.Equal(9000000000000000ul, result);
    }

    #endregion

    #region 非整数转整数异常测试

    [Fact]
    public void Eval_Long_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<long>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("Int64", ex.Message);
    }

    [Fact]
    public void Eval_Int_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<int>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("Int32", ex.Message);
    }

    [Fact]
    public void Eval_Byte_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<byte>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("Byte", ex.Message);
    }

    [Fact]
    public void Eval_SByte_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<sbyte>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("SByte", ex.Message);
    }

    [Fact]
    public void Eval_Short_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<short>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("Int16", ex.Message);
    }

    [Fact]
    public void Eval_UShort_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<ushort>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("UInt16", ex.Message);
    }

    [Fact]
    public void Eval_UInt_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<uint>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("UInt32", ex.Message);
    }

    [Fact]
    public void Eval_ULong_NonInteger_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<ulong>("3.5"));
        Assert.Contains("不是整数", ex.Message);
        Assert.Contains("UInt64", ex.Message);
    }

    #endregion

    #region 溢出异常测试

    [Fact]
    public void Eval_Int_Overflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<int>("2147483648"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("Int32", ex.Message);
    }

    [Fact]
    public void Eval_Int_NegativeOverflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<int>("-2147483649"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("Int32", ex.Message);
    }

    [Fact]
    public void Eval_Short_Overflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<short>("32768"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("Int16", ex.Message);
    }

    [Fact]
    public void Eval_SByte_Overflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<sbyte>("128"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("SByte", ex.Message);
    }

    [Fact]
    public void Eval_Byte_Overflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<byte>("256"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("Byte", ex.Message);
    }

    [Fact]
    public void Eval_Byte_Negative_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<byte>("-1"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("Byte", ex.Message);
    }

    [Fact]
    public void Eval_UShort_Overflow_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<ushort>("65536"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("UInt16", ex.Message);
    }

    [Fact]
    public void Eval_UShort_Negative_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<ushort>("-1"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("UInt16", ex.Message);
    }

    [Fact]
    public void Eval_UInt_Negative_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<uint>("-1"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("UInt32", ex.Message);
    }

    [Fact]
    public void Eval_ULong_Negative_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<ulong>("-1"));
        Assert.Contains("超出", ex.Message);
        Assert.Contains("UInt64", ex.Message);
    }

    #endregion

    #region 边界值测试

    [Fact]
    public void Eval_Int_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<int>("2147483647");
        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void Eval_Int_MinValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<int>("-2147483648");
        Assert.Equal(int.MinValue, result);
    }

    [Fact]
    public void Eval_Byte_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<byte>("255");
        Assert.Equal(byte.MaxValue, result);
    }

    [Fact]
    public void Eval_Byte_MinValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<byte>("0");
        Assert.Equal(byte.MinValue, result);
    }

    [Fact]
    public void Eval_SByte_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<sbyte>("127");
        Assert.Equal(sbyte.MaxValue, result);
    }

    [Fact]
    public void Eval_SByte_MinValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<sbyte>("-128");
        Assert.Equal(sbyte.MinValue, result);
    }

    [Fact]
    public void Eval_Short_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<short>("32767");
        Assert.Equal(short.MaxValue, result);
    }

    [Fact]
    public void Eval_Short_MinValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<short>("-32768");
        Assert.Equal(short.MinValue, result);
    }

    [Fact]
    public void Eval_UShort_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<ushort>("65535");
        Assert.Equal(ushort.MaxValue, result);
    }

    [Fact]
    public void Eval_UShort_MinValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<ushort>("0");
        Assert.Equal(ushort.MinValue, result);
    }

    [Fact]
    public void Eval_Long_MaxValue_ReturnsCorrectValue() {
        // double 精度限制（约 9×10^15），使用一个 double 能精确表示的大值
        var result = FastEval.Eval<long>("9000000000000000");
        Assert.Equal(9000000000000000L, result);
    }

    [Fact]
    public void Eval_Long_MinValue_ReturnsCorrectValue() {
        // double 精度限制，使用一个 double 能精确表示的值
        var result = FastEval.Eval<long>("-9000000000000000");
        Assert.Equal(-9000000000000000L, result);
    }

    [Fact]
    public void Eval_UInt_MaxValue_ReturnsCorrectValue() {
        var result = FastEval.Eval<uint>("4294967295");
        Assert.Equal(uint.MaxValue, result);
    }

    [Fact]
    public void Eval_ULong_MaxValue_ReturnsCorrectValue() {
        // double 精度限制（约 9×10^15），使用一个 double 能精确表示的大值
        var result = FastEval.Eval<ulong>("9000000000000000");
        Assert.Equal(9000000000000000ul, result);
    }

    #endregion

    #region bool 类型测试

    [Fact]
    public void Eval_Bool_True_ReturnsCorrectValue() {
        var result = FastEval.Eval<bool>("1");
        Assert.True(result);
    }

    [Fact]
    public void Eval_Bool_False_ReturnsCorrectValue() {
        var result = FastEval.Eval<bool>("0");
        Assert.False(result);
    }

    [Fact]
    public void Eval_Bool_Comparison_ReturnsCorrectValue() {
        var result = FastEval.Eval<bool>("3 > 2");
        Assert.True(result);
    }

    #endregion

    #region 不支持的类型测试

    [Fact]
    public void Eval_Char_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<char>("65"));
        Assert.Contains("不支持的类型", ex.Message);
        Assert.Contains("Char", ex.Message);
    }

    [Fact]
    public void Eval_DateTime_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<DateTime>("123"));
        Assert.Contains("不支持的类型", ex.Message);
        Assert.Contains("DateTime", ex.Message);
    }

    [Fact]
    public void Eval_Guid_ThrowsException() {
        var ex = Assert.Throws<FastEvalException>(() => FastEval.Eval<Guid>("123"));
        Assert.Contains("不支持的类型", ex.Message);
        Assert.Contains("Guid", ex.Message);
    }

    #endregion
}