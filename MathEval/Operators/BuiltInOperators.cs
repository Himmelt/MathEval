using MathEval.Exceptions;

namespace MathEval.Operators;

/// <summary>
/// 内置运算符实现
/// <br/>
/// 集中管理所有运算符的计算逻辑，便于查阅和维护
/// </summary>
internal static class BuiltInOperators {

    #region 类型转换辅助方法

    public static bool ConvertToBool<T>(T value) where T : struct {
        if (typeof(T) == typeof(bool)) return (bool)(object)value;
        if (typeof(T) == typeof(long)) return (long)(object)value != 0;
        if (typeof(T) == typeof(double)) return (double)(object)value != 0 && !double.IsNaN((double)(object)value);
        throw new FastEvalException("无法转换为布尔类型");
    }

    public static T BoolToT<T>(bool value) where T : struct {
        if (typeof(T) == typeof(bool)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(value ? 1L : 0L);
        if (typeof(T) == typeof(double)) return (T)(object)(value ? 1.0 : 0.0);
        throw new FastEvalException("无法从布尔类型转换");
    }

    public static T DoubleToT<T>(double value) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(long)value;
        throw new FastEvalException("无法从 double 类型转换");
    }

    public static T LongToT<T>(long value) where T : struct {
        if (typeof(T) == typeof(long)) return (T)(object)value;
        if (typeof(T) == typeof(double)) return (T)(object)(double)value;
        throw new FastEvalException("无法从 long 类型转换");
    }

    public static double ToDouble<T>(T value) where T : struct {
        if (typeof(T) == typeof(double)) return (double)(object)value;
        if (typeof(T) == typeof(long)) return (long)(object)value;
        if (typeof(T) == typeof(bool)) return (bool)(object)value ? 1.0 : 0.0;
        return Convert.ToDouble(value);
    }

    public static long ToLong<T>(T value) where T : struct {
        if (typeof(T) == typeof(long)) return (long)(object)value;
        if (typeof(T) == typeof(double)) return (long)(double)(object)value;
        if (typeof(T) == typeof(bool)) return (bool)(object)value ? 1L : 0L;
        return Convert.ToInt64(value);
    }

    public static void RequireInteger<T>(T value, string operationName) where T : struct {
        if (typeof(T) == typeof(long)) return;
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)value;
            if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d) && d >= long.MinValue && d <= long.MaxValue)
                return;
            throw new FastEvalException($"{operationName} 运算需要整数操作数");
        }
        throw new FastEvalException($"{operationName} 运算需要整数操作数");
    }

    public static long ToInteger<T>(T value, string operationName) where T : struct {
        RequireInteger<T>(value, operationName);
        return ToLong<T>(value);
    }

    #endregion

    #region 算术运算

    /// <summary>
    /// 加法运算 +
    /// </summary>
    public static T Add<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left + (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left + (long)(object)right);
        throw new FastEvalException("加法运算需要数值类型");
    }

    /// <summary>
    /// 减法运算 -
    /// </summary>
    public static T Subtract<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left - (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left - (long)(object)right);
        throw new FastEvalException("减法运算需要数值类型");
    }

    /// <summary>
    /// 乘法运算 *
    /// </summary>
    public static T Multiply<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left * (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left * (long)(object)right);
        throw new FastEvalException("乘法运算需要数值类型");
    }

    /// <summary>
    /// 除法运算 /
    /// </summary>
    public static T Divide<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)((double)(object)left / d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)(long)((double)(long)(object)left / r);
        }
        throw new FastEvalException("除法运算需要数值类型");
    }

    /// <summary>
    /// 整除运算 //
    /// </summary>
    public static T IntegerDivide<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)Math.Truncate((double)(object)left / d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)((long)(object)left / r);
        }
        throw new FastEvalException("整除运算需要数值类型");
    }

    /// <summary>
    /// 取模运算 mod
    /// <br/>
    /// 结果符号与除数（右操作数）相同，计算时向负无穷取整
    /// </summary>
    public static T Modulo<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)((double)(object)left % d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)((long)(object)left % r);
        }
        throw new FastEvalException("取模运算需要数值类型");
    }

    /// <summary>
    /// 乘方运算 ^ 或 **
    /// </summary>
    public static double Power<T>(T left, T right) where T : struct {
        double d1, d2;
        var isLong = false;
        if (typeof(T) == typeof(double)) {
            d1 = (double)(object)left;
            d2 = (double)(object)right;
        } else {
            d1 = (long)(object)left;
            d2 = (long)(object)right;
            isLong = true;
        }
        if (d1 < 0 && d2 != Math.Floor(d2))
            throw new FastEvalException("不能对负数求非整数次幂");
        if (isLong && d1 == 0 && d2 < 0)
            throw new FastEvalException("零不能求负数次幂");
        return Math.Pow(d1, d2);
    }

    /// <summary>
    /// 乘方结果类型转换
    /// </summary>
    public static T CastPowerResult<T>(double value) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(long)value;
        throw new FastEvalException("幂运算结果类型不匹配");
    }

    #endregion

    #region 一元运算

    /// <summary>
    /// 一元负号 -
    /// </summary>
    public static T Negate<T>(T operand) where T : struct {
        if (typeof(T) == typeof(double)) return (T)(object)(-(double)(object)operand);
        if (typeof(T) == typeof(long)) return (T)(object)checked(-(long)(object)operand);
        throw new FastEvalException("取负运算需要数值类型");
    }

    /// <summary>
    /// 逻辑非运算 ! 或 not
    /// </summary>
    public static T Not<T>(T operand) where T : struct {
        if (typeof(T) == typeof(bool)) return (T)(object)(!(bool)(object)operand);
        if (typeof(T) == typeof(double)) return (T)(object)(ConvertToBool<T>(operand) ? 0.0 : 1.0);
        if (typeof(T) == typeof(long)) return (T)(object)(ConvertToBool<T>(operand) ? 0L : 1L);
        throw new FastEvalException("逻辑非运算需要布尔或数值类型");
    }

    /// <summary>
    /// 按位取反运算 ~
    /// </summary>
    public static T BitwiseNot<T>(T operand) where T : struct {
        return LongToT<T>(~ToInteger<T>(operand, "按位取反"));
    }

    #endregion

    #region 位运算

    /// <summary>
    /// 按位或运算 |
    /// </summary>
    public static T BitwiseOr<T>(T left, T right) where T : struct {
        return LongToT<T>(ToInteger<T>(left, "按位或") | ToInteger<T>(right, "按位或"));
    }

    /// <summary>
    /// 按位与运算 &amp;
    /// </summary>
    public static T BitwiseAnd<T>(T left, T right) where T : struct {
        return LongToT<T>(ToInteger<T>(left, "按位与") & ToInteger<T>(right, "按位与"));
    }

    /// <summary>
    /// 按位异或运算 xor
    /// </summary>
    public static T BitwiseXor<T>(T left, T right) where T : struct {
        return LongToT<T>(ToInteger<T>(left, "按位异或") ^ ToInteger<T>(right, "按位异或"));
    }

    /// <summary>
    /// 左移运算 &lt;&lt;
    /// </summary>
    public static T LeftShift<T>(T left, T right) where T : struct {
        var l1 = ToInteger<T>(left, "左移");
        var l2 = ToInteger<T>(right, "左移");
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return LongToT<T>(l1 << (int)l2);
    }

    /// <summary>
    /// 算术右移运算 &gt;&gt;
    /// </summary>
    public static T RightShift<T>(T left, T right) where T : struct {
        var l1 = ToInteger<T>(left, "右移");
        var l2 = ToInteger<T>(right, "右移");
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return LongToT<T>(l1 >> (int)l2);
    }

    #endregion

    #region 比较运算

    /// <summary>
    /// 相等运算 ==
    /// </summary>
    public static T Equal<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) {
            var d1 = (double)(object)left;
            var d2 = (double)(object)right;
            if (double.IsNaN(d1) || double.IsNaN(d2)) return BoolToT<T>(false);
            return BoolToT<T>(d1 == d2);
        }
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left == (long)(object)right);
        return BoolToT<T>(Equals(left, right));
    }

    /// <summary>
    /// 不等运算 !=
    /// </summary>
    public static T NotEqual<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) {
            var d1 = (double)(object)left;
            var d2 = (double)(object)right;
            if (double.IsNaN(d1) || double.IsNaN(d2)) return BoolToT<T>(true);
            return BoolToT<T>(d1 != d2);
        }
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left != (long)(object)right);
        return BoolToT<T>(!Equals(left, right));
    }

    /// <summary>
    /// 小于运算 &lt;
    /// </summary>
    public static T LessThan<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return BoolToT<T>((double)(object)left < (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left < (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    /// <summary>
    /// 小于等于运算 &lt;=
    /// </summary>
    public static T LessThanOrEqual<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return BoolToT<T>((double)(object)left <= (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left <= (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    /// <summary>
    /// 大于运算 &gt;
    /// </summary>
    public static T GreaterThan<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return BoolToT<T>((double)(object)left > (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left > (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    /// <summary>
    /// 大于等于运算 &gt;=
    /// </summary>
    public static T GreaterThanOrEqual<T>(T left, T right) where T : struct {
        if (typeof(T) == typeof(double)) return BoolToT<T>((double)(object)left >= (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT<T>((long)(object)left >= (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    #endregion

    #region 逻辑运算

    /// <summary>
    /// 条件逻辑与运算 &amp;&amp; 或 and
    /// <br/>
    /// 短路求值：仅当左操作数为 true 时才计算右操作数
    /// </summary>
    public static T LogicalAnd<T>(T left, Func<T> rightEval) where T : struct {
        if (!ConvertToBool<T>(left)) return BoolToT<T>(false);
        return BoolToT<T>(ConvertToBool<T>(rightEval()));
    }

    /// <summary>
    /// 条件逻辑或运算 || 或 or
    /// <br/>
    /// 短路求值：仅当左操作数为 false 时才计算右操作数
    /// </summary>
    public static T LogicalOr<T>(T left, Func<T> rightEval) where T : struct {
        if (ConvertToBool<T>(left)) return BoolToT<T>(true);
        return BoolToT<T>(ConvertToBool<T>(rightEval()));
    }

    #endregion
}