using MathEval.Fast.Exceptions;

namespace MathEval.Fast.BuiltIn;

/// <summary>
/// 内置运算符实现
/// <br/>
/// 统一使用 double 进行运算，位操作要求操作数为整数
/// </summary>
internal static class BuiltInOperators {

    #region 类型转换辅助方法

    /// <summary>
    /// 将 double 值转换为布尔值
    /// </summary>
    public static bool ConvertToBool(double value) {
        return value != 0 && !double.IsNaN(value);
    }

    /// <summary>
    /// 将布尔值转换为 double（true=1.0, false=0.0）
    /// </summary>
    public static double BoolToDouble(bool value) {
        return value ? 1.0 : 0.0;
    }

    /// <summary>
    /// 检查 double 值是否为数学整数（不含小数部分，且在 long 范围内）
    /// </summary>
    public static bool IsInteger(double value) {
        return value == Math.Truncate(value) && !double.IsInfinity(value) && !double.IsNaN(value) && value >= long.MinValue && value < 9223372036854775808.0;
    }

    /// <summary>
    /// 将 double 转换为 long，如果非整数则抛出异常
    /// </summary>
    public static long ToInt64(double value, string operationName) {
        if (!IsInteger(value)) throw new FastEvalException($"{operationName} 运算需要整数操作数");
        return (long)value;
    }

    #endregion

    #region 算术运算

    /// <summary>
    /// 加法运算 +
    /// </summary>
    public static double Add(double left, double right) => left + right;

    /// <summary>
    /// 减法运算 -
    /// </summary>
    public static double Subtract(double left, double right) => left - right;

    /// <summary>
    /// 乘法运算 *
    /// </summary>
    public static double Multiply(double left, double right) => left * right;

    /// <summary>
    /// 除法运算 /
    /// </summary>
    public static double Divide(double left, double right) {
        return left / right;
    }

    /// <summary>
    /// 整除运算 //
    /// </summary>
    public static double IntegerDivide(double left, double right) {
        // TODO 实现泛型版本，支持整数和浮点数的整除
        return Math.Truncate(left / right);
    }

    /// <summary>
    /// 取余运算 %
    /// <br/>
    /// 结果符号与被除数（左操作数）相同，直接使用 C# 的 % 运算符
    /// </summary>
    public static double Remainder(double left, double right) {
        return left % right;
    }

    /// <summary>
    /// 取模运算 mod
    /// <br/>
    /// 结果符号与除数（右操作数）相同，计算时向负无穷取整
    /// </summary>
    public static double Modulo(double left, double right) {
        double r = left % right;
        if ((r < 0 && right > 0) || (r > 0 && right < 0))
            r += right;
        return r;
    }

    /// <summary>
    /// 乘方运算 ^ 或 **
    /// </summary>
    public static double Power(double left, double right) {
        return Math.Pow(left, right);
    }

    #endregion

    #region 一元运算

    /// <summary>
    /// 一元负号 -
    /// </summary>
    public static double Negate(double operand) => -operand;

    /// <summary>
    /// 逻辑非运算 ! 或 not
    /// </summary>
    public static double Not(double operand) => ConvertToBool(operand) ? 0.0 : 1.0;

    /// <summary>
    /// 按位取反运算 ~
    /// </summary>
    public static double BitwiseNot(double operand) {
        return ~ToInt64(operand, "按位取反");
    }

    #endregion

    #region 位运算

    /// <summary>
    /// 按位或运算 |
    /// </summary>
    public static double BitwiseOr(double left, double right) {
        return ToInt64(left, "按位或") | ToInt64(right, "按位或");
    }

    /// <summary>
    /// 按位与运算 &amp;
    /// </summary>
    public static double BitwiseAnd(double left, double right) {
        return ToInt64(left, "按位与") & ToInt64(right, "按位与");
    }

    /// <summary>
    /// 按位异或运算 xor
    /// </summary>
    public static double BitwiseXor(double left, double right) {
        return ToInt64(left, "按位异或") ^ ToInt64(right, "按位异或");
    }

    /// <summary>
    /// 左移运算 &lt;&lt;
    /// </summary>
    public static double LeftShift(double left, double right) {
        var l1 = ToInt64(left, "左移");
        var l2 = ToInt64(right, "左移");
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 << (int)l2;
    }

    /// <summary>
    /// 算术右移运算 &gt;&gt;
    /// </summary>
    public static double RightShift(double left, double right) {
        var l1 = ToInt64(left, "右移");
        var l2 = ToInt64(right, "右移");
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 >> (int)l2;
    }

    #endregion

    #region 比较运算

    /// <summary>
    /// 相等运算 ==
    /// </summary>
    public static double Equal(double left, double right) {
        if (double.IsNaN(left) || double.IsNaN(right)) return BoolToDouble(false);
        return BoolToDouble(left == right);
    }

    /// <summary>
    /// 不等运算 !=
    /// </summary>
    public static double NotEqual(double left, double right) {
        if (double.IsNaN(left) || double.IsNaN(right)) return BoolToDouble(true);
        return BoolToDouble(left != right);
    }

    /// <summary>
    /// 小于运算 &lt;
    /// </summary>
    public static double LessThan(double left, double right) => BoolToDouble(left < right);

    /// <summary>
    /// 小于等于运算 &lt;=
    /// </summary>
    public static double LessThanOrEqual(double left, double right) => BoolToDouble(left <= right);

    /// <summary>
    /// 大于运算 &gt;
    /// </summary>
    public static double GreaterThan(double left, double right) => BoolToDouble(left > right);

    /// <summary>
    /// 大于等于运算 &gt;=
    /// </summary>
    public static double GreaterThanOrEqual(double left, double right) => BoolToDouble(left >= right);

    #endregion
}