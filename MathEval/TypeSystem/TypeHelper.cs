using MathEval.Exceptions;
using MathEval.Parser;

namespace MathEval.TypeSystem;

public static class TypeHelper {
    public static double BoolToNumber(bool value) {
        return value ? 1.0 : 0.0;
    }

    public static (double, double) PromoteToDouble(object left, object right) {
        double leftDouble = left switch {
            bool b => BoolToNumber(b),
            long l => l,
            double d => d,
            _ => throw new TypeMismatchException("期望数值类型", "number", left?.GetType().Name ?? "null")
        };

        double rightDouble = right switch {
            bool b => BoolToNumber(b),
            long l => l,
            double d => d,
            _ => throw new TypeMismatchException("期望数值类型", "number", right?.GetType().Name ?? "null")
        };

        return (leftDouble, rightDouble);
    }

    public static double ToDouble(object value) {
        return value switch {
            bool b => BoolToNumber(b),
            long l => l,
            double d => d,
            _ => throw new TypeMismatchException("期望数值类型", "number", value?.GetType().Name ?? "null")
        };
    }

    public static void RequireInteger(object value, string operationName) {
        if (value is long) return;
        if (value is double d) {
            if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d) && d >= long.MinValue && d <= long.MaxValue)
                return;
        }
        throw new TypeMismatchException($"{operationName} 运算需要整数操作数", "integer", value?.GetType().Name ?? "null");
    }

    public static long ToInteger(object value, string operationName) {
        RequireInteger(value, operationName);
        if (value is long l) return l;
        if (value is double d) return (long)d;
        throw new TypeMismatchException($"{operationName} 运算需要整数操作数", "integer", value?.GetType().Name ?? "null");
    }

    public static string ToString(object value) {
        return value switch {
            bool b => b.ToString(),
            long l => l.ToString(),
            double d => double.IsNaN(d) ? "NaN" : double.IsPositiveInfinity(d) ? "INF" : d.ToString("G"),
            string s => s,
            null => "",
            _ => value.ToString() ?? ""
        };
    }

    public static string Format(object value, string formatSpec) {
        if (value is not long && value is not double)
            throw new EvaluateException($"格式说明符 '{formatSpec}' 只能用于数值类型");

        var firstChar = char.ToLowerInvariant(formatSpec[0]);
        var supportedFormats = new[] { 'd', 'e', 'f', 'g', 'x' };
        if (!supportedFormats.Contains(firstChar))
            throw new ParseException($"不支持的格式说明符：{formatSpec}", 1, 1);

        // D和X格式说明符只适用于整数类型
        // 如果value是double且为数学整数，转换为long后格式化
        if ((firstChar == 'd' || firstChar == 'x') && value is double d) {
            if (double.IsNaN(d) || double.IsInfinity(d) || d != Math.Truncate(d))
                throw new EvaluateException($"格式说明符 '{formatSpec}' 只能用于整数，但值为 {d}");
            value = (long)d;
        }

        return string.Format($"{{0:{formatSpec}}}", value);
    }

    public static bool IsTruthy(object value) {
        if (value is bool b)
            return b;
        return false;
    }

    public static void RequireBool(object value) {
        if (value is not bool)
            throw new TypeMismatchException("期望布尔类型", "bool", value?.GetType().Name ?? "null");
    }

    private static bool IsNaN(object value) => value is double d && double.IsNaN(d);
    private static bool IsINF(object value) => value is double d && double.IsInfinity(d);

    public static object EvaluateBinary(BinaryExpressionType type, object left, object right) {
        if (type == BinaryExpressionType.Plus) {
            if (left is string || right is string) return ToString(left) + ToString(right);
        }

        return type switch {
            BinaryExpressionType.Plus => EvaluatePlus(left, right),
            BinaryExpressionType.Minus => EvaluateMinus(left, right),
            BinaryExpressionType.Multiply => EvaluateMultiply(left, right),
            BinaryExpressionType.Divide => EvaluateDivide(left, right),
            BinaryExpressionType.IntegerDivide => EvaluateIntegerDivide(left, right),
            BinaryExpressionType.Remainder => EvaluateRemainder(left, right),
            BinaryExpressionType.Modulo => EvaluateModulo(left, right),
            BinaryExpressionType.Power => EvaluatePower(left, right),
            BinaryExpressionType.BitwiseAnd => EvaluateBitwiseAnd(left, right),
            BinaryExpressionType.BitwiseOr => EvaluateBitwiseOr(left, right),
            BinaryExpressionType.BitwiseXor => EvaluateBitwiseXor(left, right),
            BinaryExpressionType.LeftShift => EvaluateLeftShift(left, right),
            BinaryExpressionType.RightShift => EvaluateRightShift(left, right),
            BinaryExpressionType.UnsignedRightShift => EvaluateUnsignedRightShift(left, right),
            BinaryExpressionType.Equal => EvaluateEqual(left, right),
            BinaryExpressionType.NotEqual => EvaluateNotEqual(left, right),
            BinaryExpressionType.LessThan => EvaluateLessThan(left, right),
            BinaryExpressionType.LessThanOrEqual => EvaluateLessThanOrEqual(left, right),
            BinaryExpressionType.GreaterThan => EvaluateGreaterThan(left, right),
            BinaryExpressionType.GreaterThanOrEqual => EvaluateGreaterThanOrEqual(left, right),
            BinaryExpressionType.And => EvaluateAnd(left, right),
            BinaryExpressionType.Or => EvaluateOr(left, right),
            _ => throw new Exceptions.InvalidOperationException($"未知的二元运算符：{type}")
        };
    }

    private static double EvaluatePlus(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);
        return d1 + d2;
    }

    private static double EvaluateMinus(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);
        return d1 - d2;
    }

    private static double EvaluateMultiply(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);
        return d1 * d2;
    }

    private static double EvaluateDivide(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);

        if (double.IsNaN(d1) || double.IsNaN(d2)) return double.NaN;
        if (double.IsInfinity(d1) && double.IsInfinity(d2)) return double.NaN;
        if (double.IsInfinity(d1)) return d1 > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        if (double.IsInfinity(d2)) return 0.0;
        if (d2 == 0) throw new DivisionByZeroException();

        return d1 / d2;
    }

    private static long EvaluateIntegerDivide(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);

        if (double.IsNaN(d1) || double.IsNaN(d2) || double.IsInfinity(d1) || double.IsInfinity(d2))
            throw new EvaluateException("整除运算不支持 NaN 或 INF");
        if (d2 == 0) throw new DivisionByZeroException();

        return (long)(d1 / d2);
    }

    private static double EvaluateRemainder(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);

        if (double.IsNaN(d1) || double.IsNaN(d2)) return double.NaN;
        if (double.IsInfinity(d1)) return double.NaN;
        if (double.IsInfinity(d2)) return d1;
        if (d2 == 0) throw new DivisionByZeroException();

        return d1 % d2;
    }

    private static double EvaluateModulo(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);

        if (double.IsNaN(d1) || double.IsNaN(d2)) return double.NaN;

        if (double.IsInfinity(d1)) return double.NaN;

        if (double.IsInfinity(d2)) return d1;

        if (d2 == 0) throw new DivisionByZeroException();

        double r = d1 % d2;
        if ((r < 0 && d2 > 0) || (r > 0 && d2 < 0))
            r += d2;
        return r;
    }

    private static double EvaluatePower(object left, object right) {
        var (d1, d2) = PromoteToDouble(left, right);

        if (double.IsNaN(d1) || double.IsNaN(d2)) return double.NaN;

        if (d1 < 0 && d2 != Math.Floor(d2)) throw new EvaluateException("不能对负数求非整数次幂");
        if (d1 == 0 && d2 < 0) throw new EvaluateException("零不能求负数次幂");

        return Math.Pow(d1, d2);
    }

    private static long EvaluateBitwiseAnd(object left, object right) {
        long l1 = ToInteger(left, "按位与");
        long l2 = ToInteger(right, "按位与");
        return l1 & l2;
    }

    private static long EvaluateBitwiseOr(object left, object right) {
        long l1 = ToInteger(left, "按位或");
        long l2 = ToInteger(right, "按位或");
        return l1 | l2;
    }

    private static long EvaluateBitwiseXor(object left, object right) {
        long l1 = ToInteger(left, "按位异或");
        long l2 = ToInteger(right, "按位异或");
        return l1 ^ l2;
    }

    private static long EvaluateLeftShift(object left, object right) {
        long l1 = ToInteger(left, "左移");
        long l2 = ToInteger(right, "左移");
        if (l2 < 0) throw new EvaluateException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 << (int)l2;
    }

    private static long EvaluateRightShift(object left, object right) {
        long l1 = ToInteger(left, "右移");
        long l2 = ToInteger(right, "右移");
        if (l2 < 0) throw new EvaluateException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 >> (int)l2;
    }

    private static long EvaluateUnsignedRightShift(object left, object right) {
        long l1 = ToInteger(left, "无符号右移");
        long l2 = ToInteger(right, "无符号右移");
        if (l2 < 0) throw new EvaluateException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return (long)((ulong)l1 >> (int)l2);
    }

    private static bool EvaluateEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            if (IsNaN(left) || IsNaN(right)) return false;
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 == d2;
        }

        if (left.GetType() != right.GetType()) return false;
        return Equals(left, right);
    }

    private static bool EvaluateNotEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            if (IsNaN(left) || IsNaN(right)) return true;
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 != d2;
        }

        if (left.GetType() != right.GetType()) return true;
        return !Equals(left, right);
    }

    private static bool EvaluateLessThan(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 < d2;
        }

        if (left is string s1 && right is string s2)
            return string.Compare(s1, s2, StringComparison.Ordinal) < 0;

        throw new TypeMismatchException("比较运算需要兼容类型", "number | string", GetTypeName(left, right));
    }

    private static bool EvaluateLessThanOrEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 <= d2;
        }

        if (left is string s1 && right is string s2)
            return string.Compare(s1, s2, StringComparison.Ordinal) <= 0;

        throw new TypeMismatchException("比较运算需要兼容类型", "number | string", GetTypeName(left, right));
    }

    private static bool EvaluateGreaterThan(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 > d2;
        }

        if (left is string s1 && right is string s2)
            return string.Compare(s1, s2, StringComparison.Ordinal) > 0;

        throw new TypeMismatchException("比较运算需要兼容类型", "number | string", GetTypeName(left, right));
    }

    private static bool EvaluateGreaterThanOrEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            var (d1, d2) = PromoteToDouble(left, right);
            return d1 >= d2;
        }

        if (left is string s1 && right is string s2)
            return string.Compare(s1, s2, StringComparison.Ordinal) >= 0;

        throw new TypeMismatchException("比较运算需要兼容类型", "number | string", GetTypeName(left, right));
    }

    private static object EvaluateAnd(object left, object right) {
        RequireBool(left);
        RequireBool(right);
        return (bool)left && (bool)right;
    }

    private static object EvaluateOr(object left, object right) {
        RequireBool(left);
        RequireBool(right);
        return (bool)left || (bool)right;
    }

    public static object EvaluateUnary(UnaryExpressionType type, object operand) {
        return type switch {
            UnaryExpressionType.Positive => EvaluatePositive(operand),
            UnaryExpressionType.Negate => EvaluateNegate(operand),
            UnaryExpressionType.Not => EvaluateNot(operand),
            UnaryExpressionType.BitwiseNot => EvaluateBitwiseNot(operand),
            _ => throw new Exceptions.InvalidOperationException($"未知的一元运算符：{type}")
        };
    }

    private static double EvaluatePositive(object operand) {
        return ToDouble(operand);
    }

    private static double EvaluateNegate(object operand) {
        return -ToDouble(operand);
    }

    private static object EvaluateNot(object operand) {
        RequireBool(operand);
        return !(bool)operand;
    }

    private static long EvaluateBitwiseNot(object operand) {
        long l = ToInteger(operand, "按位取反");
        return ~l;
    }

    private static string GetTypeName(object? a, object? b) {
        if (a is bool || (b != null && b is bool)) return "bool";
        if (a is long || (b != null && b is long)) return "long";
        if (a is double || (b != null && b is double)) return "double";
        if (a is string || (b != null && b is string)) return "string";
        return "unknown";
    }
}
