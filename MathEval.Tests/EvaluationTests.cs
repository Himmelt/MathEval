using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

public class EvaluationTests {
    [Fact]
    public void Arithmetic_AddMulPriority_Returns11() {
        Assert.Equal(11L, Expression.Eval<long>("3 + 4 * 2"));
    }

    [Fact]
    public void Arithmetic_AddMulPowPriority_Returns50() {
        Assert.Equal(50L, Expression.Eval<long>("2 + 3 * 4 ^ 2"));
    }

    [Fact]
    public void Arithmetic_SubLeftAssociative_Returns5() {
        Assert.Equal(5L, Expression.Eval<long>("10 - 3 - 2"));
    }

    [Fact]
    public void Arithmetic_DivLeftAssociative_Returns5() {
        Assert.Equal(5.0, Expression.Eval<double>("100 / 10 / 2"));
    }

    [Fact]
    public void Power_RightAssociative_Returns512() {
        Assert.Equal(512L, Expression.Eval<long>("2 ^ 3 ^ 2"));
    }

    [Fact]
    public void Power_FractionalExponent_Returns3() {
        Assert.Equal(3.0, Expression.Eval<double>("9 ^ 0.5"));
    }

    [Fact]
    public void Power_NegativeExponent_Returns0_5() {
        Assert.Equal(0.5, Expression.Eval<double>("2 ^ -1"));
    }

    [Fact]
    public void Power_ZeroToZero_Returns1() {
        Assert.Equal(1L, Expression.Eval<long>("0 ^ 0"));
    }

    [Fact]
    public void IntegerDivision_Basic_Returns3() {
        Assert.Equal(3L, Expression.Eval<long>("7 // 2"));
    }

    [Fact]
    public void IntegerDivision_Negative_TruncatesTowardZero() {
        Assert.Equal(-3L, Expression.Eval<long>("-7 // 2"));
    }

    [Fact]
    public void IntegerDivision_DoubleOperand_Returns3() {
        Assert.Equal(3L, Expression.Eval<long>("7.5 // 2"));
    }

    [Fact]
    public void Modulo_Basic_Returns1() {
        Assert.Equal(1L, Expression.Eval<long>("7 % 3"));
    }

    [Fact]
    public void Modulo_DoubleOperand_Returns1_5() {
        Assert.Equal(1.5, Expression.Eval<double>("7.5 % 2"));
    }

    [Fact]
    public void Modulo_Negative_ReturnsMinus1() {
        Assert.Equal(-1L, Expression.Eval<long>("-7 % 3"));
    }

    [Fact]
    public void Division_ReturnsDouble() {
        Assert.Equal(3.5, Expression.Eval<double>("7 / 2"));
    }

    [Fact]
    public void Division_ByZero_ThrowsDivisionByZeroException() {
        Assert.Throws<DivisionByZeroException>(() => Expression.Eval("1 / 0"));
    }

    [Fact]
    public void IntegerDivision_ByZero_ThrowsDivisionByZeroException() {
        Assert.Throws<DivisionByZeroException>(() => Expression.Eval("1 // 0"));
    }

    [Fact]
    public void Modulo_ByZero_ThrowsDivisionByZeroException() {
        Assert.Throws<DivisionByZeroException>(() => Expression.Eval("5 % 0"));
    }

    [Fact]
    public void StringConcat_TwoStrings_ReturnsHelloWorld() {
        Assert.Equal("hello world", Expression.Eval<string>("'hello' + ' ' + 'world'"));
    }

    [Fact]
    public void StringConcat_StringAndInt_ReturnsValue42() {
        Assert.Equal("value: 42", Expression.Eval<string>("'value: ' + 42"));
    }

    [Fact]
    public void StringConcat_DoubleAndString_Returns3_14IsPi() {
        Assert.Equal("3.14 is pi", Expression.Eval<string>("3.14 + ' is pi'"));
    }

    [Fact]
    public void StringConcat_StringAndBool_ReturnsFlagTrue() {
        Assert.Equal("flag: True", Expression.Eval<string>("'flag: ' + true"));
    }

    [Fact]
    public void StringConcat_BoolAndString_ReturnsTrueBang() {
        Assert.Equal("True!", Expression.Eval<string>("true + '!'"));
    }

    [Fact]
    public void StringConcat_IntAndBool_Returns6() {
        Assert.Equal(6L, Expression.Eval<long>("5 + true"));
    }

    [Fact]
    public void StringConcat_BoolAndBool_Returns2() {
        Assert.Equal(2L, Expression.Eval<long>("true + true"));
    }

    [Fact]
    public void StringConcat_StringMinusInt_ThrowsTypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'hello' - 1"));
    }

    [Fact]
    public void Comparison_GreaterThan_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("3 > 2"));
    }

    [Fact]
    public void Comparison_StringLessThan_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("'abc' < 'abd'"));
    }

    [Fact]
    public void Comparison_StringCaseSensitive_NotEqual() {
        Assert.False(Expression.Eval<bool>("'A' == 'a'"));
    }

    [Fact]
    public void Comparison_CrossTypeNumeric_Equal() {
        Assert.True(Expression.Eval<bool>("1.0 == 1"));
    }

    [Fact]
    public void Comparison_StringEqual_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("'hello' == 'hello'"));
    }

    [Fact]
    public void Comparison_BoolEqual_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("true == true"));
    }

    [Fact]
    public void Comparison_BoolNotEqual_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("true != false"));
    }

    [Fact]
    public void Comparison_IntAndString_NotEqual() {
        Assert.False(Expression.Eval<bool>("1 == '1'"));
    }

    [Fact]
    public void Comparison_IntAndString_NotEqualOp() {
        Assert.True(Expression.Eval<bool>("1 != '1'"));
    }

    [Fact]
    public void Comparison_IntGreaterThanString_ThrowsTypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("1 > '1'"));
    }

    [Fact]
    public void Logical_And_ReturnsFalse() {
        Assert.False(Expression.Eval<bool>("true and false"));
    }

    [Fact]
    public void Logical_AndShortCircuit_DoesNotEvaluateRight() {
        Assert.False(Expression.Eval<bool>("false and (1/0 == 0)"));
    }

    [Fact]
    public void Logical_OrShortCircuit_DoesNotEvaluateRight() {
        Assert.True(Expression.Eval<bool>("true or (1/0 == 0)"));
    }

    [Fact]
    public void Logical_DoubleAmpersand_ReturnsFalse() {
        Assert.False(Expression.Eval<bool>("true && false"));
    }

    [Fact]
    public void Logical_DoublePipe_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("false || true"));
    }

    [Fact]
    public void Logical_Not_ReturnsFalse() {
        Assert.False(Expression.Eval<bool>("not true"));
    }

    [Fact]
    public void Logical_BangNot_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("!false"));
    }

    [Fact]
    public void Logical_IntAnd_ThrowsTypeMismatch() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("1 and 2"));
    }

    [Fact]
    public void Unary_Plus5_Returns5() {
        Assert.Equal(5L, Expression.Eval<long>("+5"));
    }

    [Fact]
    public void Unary_Minus5_ReturnsMinus5() {
        Assert.Equal(-5L, Expression.Eval<long>("-5"));
    }

    [Fact]
    public void Unary_MinusString_ThrowsTypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("-'hello'"));
    }

    [Fact]
    public void Unary_NotInt_ThrowsTypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("not 1"));
    }

    [Fact]
    public void Bitwise_And_Returns1() {
        Assert.Equal(1L, Expression.Eval<long>("5 & 3"));
    }

    [Fact]
    public void Bitwise_Or_Returns7() {
        Assert.Equal(7L, Expression.Eval<long>("5 | 3"));
    }

    [Fact]
    public void Bitwise_Xor_Returns6() {
        Assert.Equal(6L, Expression.Eval<long>("5 xor 3"));
    }

    [Fact]
    public void Bitwise_BoolAndInt_Returns0() {
        Assert.Equal(0L, Expression.Eval<long>("true & 6"));
    }

    [Fact]
    public void Bitwise_Not5_ReturnsMinus6() {
        Assert.Equal(-6L, Expression.Eval<long>("~5"));
    }

    [Fact]
    public void Bitwise_LeftShift_Returns16() {
        Assert.Equal(16L, Expression.Eval<long>("1 << 4"));
    }

    [Fact]
    public void Bitwise_RightShift_Returns4() {
        Assert.Equal(4L, Expression.Eval<long>("16 >> 2"));
    }

    [Fact]
    public void Bitwise_LeftShift64_Returns1() {
        Assert.Equal(1L, Expression.Eval<long>("1 << 64"));
    }

    [Fact]
    public void Bitwise_LeftShiftNegative_ThrowsEvaluateException() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("1 << -1"));
    }

    [Fact]
    public void SpecialValues_NaNPlus1_ReturnsNaN() {
        Assert.True(double.IsNaN(Expression.Eval<double>("NaN + 1")));
    }

    [Fact]
    public void SpecialValues_NaNEqualsNaN_ReturnsFalse() {
        Assert.False(Expression.Eval<bool>("NaN == NaN"));
    }

    [Fact]
    public void SpecialValues_NaNNotEqualNaN_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("NaN != NaN"));
    }

    [Fact]
    public void SpecialValues_INFPlus1_ReturnsINF() {
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("INF + 1")));
    }

    [Fact]
    public void SpecialValues_INFMinusINF_ReturnsNaN() {
        Assert.True(double.IsNaN(Expression.Eval<double>("INF - INF")));
    }

    [Fact]
    public void SpecialValues_INFGreaterThan1_ReturnsTrue() {
        Assert.True(Expression.Eval<bool>("INF > 1"));
    }

    [Fact]
    public void SpecialValues_ZeroTimesINF_ReturnsNaN() {
        Assert.True(double.IsNaN(Expression.Eval<double>("0 * INF")));
    }

    [Fact]
    public void Ternary_TrueBranch_Returns1() {
        Assert.Equal(1L, Expression.Eval<long>("true ? 1 : 2"));
    }

    [Fact]
    public void Ternary_FalseBranch_Returns2() {
        Assert.Equal(2L, Expression.Eval<long>("false ? 1 : 2"));
    }

    [Fact]
    public void Ternary_NestedTrueFalse_Returns2() {
        Assert.Equal(2L, Expression.Eval<long>("true ? false ? 1 : 2 : 3"));
    }

    [Fact]
    public void Ternary_NestedFalseTrue_Returns2() {
        Assert.Equal(2L, Expression.Eval<long>("false ? 1 : true ? 2 : 3"));
    }

    [Fact]
    public void Ternary_IntCondition_ThrowsTypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("1 ? 2 : 3"));
    }

    [Fact]
    public void TypeInference_42IsLong() {
        Assert.IsType<long>(Expression.Eval("42"));
    }

    [Fact]
    public void TypeInference_3_14IsDouble() {
        Assert.IsType<double>(Expression.Eval("3.14"));
    }

    [Fact]
    public void TypeInference_BoolPlusIntIsLong() {
        var result = Expression.Eval("true + 1");
        Assert.IsType<double>(result);
        Assert.Equal(2.0, result);
    }

    [Fact]
    public void TypeInference_BoolPlusDoubleIsDouble() {
        var result = Expression.Eval("true + 1.5");
        Assert.IsType<double>(result);
        Assert.Equal(2.5, result);
    }

    [Fact]
    public void TypeInference_HexOctalBinarySum_Returns273() {
        Assert.Equal(273L, Expression.Eval<long>("0xFF + 0o10 + 0b1010"));
    }

    [Fact]
    public void TypeInference_HexBitwiseAnd_Returns15() {
        Assert.Equal(15L, Expression.Eval<long>("0xFF & 0x0F"));
    }

    [Fact]
    public void TypeInference_NegativeHex_ReturnsMinus255() {
        Assert.Equal(-255L, Expression.Eval<long>("-0xFF"));
    }

    [Fact]
    public void Overflow_LongMaxPlus1_ThrowsOverflowException() {
        Assert.Throws<Exceptions.OverflowException>(() => Expression.Eval("9223372036854775807 + 1"));
    }

    [Fact]
    public void Context_VariableAbs_Returns5() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5L);
        Assert.Equal(5L, Expression.Eval<long>("x > 0 ? x : -x", ctx));
    }

    [Fact]
    public void Context_VariableRangeCheck_ReturnsValid() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5L);
        Assert.Equal("valid", Expression.Eval<string>("x > 0 and x < 10 ? 'valid' : 'invalid'", ctx));
    }

    [Fact]
    public void Error_EmptyExpression_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval(""));
    }

    [Fact]
    public void Error_InvalidSyntax_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("2 + * 3"));
    }

    [Fact]
    public void Error_UndefinedVariable_ThrowsSymbolNotFoundException() {
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("undefinedVar"));
    }
}