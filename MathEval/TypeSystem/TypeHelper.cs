using MathEval.Exceptions;
using MathEval.Parser;

namespace MathEval.TypeSystem;

public static class TypeHelper {
    public static object BoolToNumber(bool value, bool preferDouble = false) {
        long longValue = value ? 1L : 0L;
        return preferDouble ? (double)longValue : longValue;
    }

    public static (object, object) Promote(object left, object right) {
        if (left is bool _left) left = BoolToNumber(_left, right is double);
        if (right is bool _right) right = BoolToNumber(_right, left is double);

        if (left is long _left2 && right is double) left = (double)_left2;
        if (left is double && right is long _right2) right = (double)_right2;

        return (left, right);
    }

    public static long CheckedAdd(long a, long b) {
        try {
            return checked(a + b);
        } catch (System.OverflowException) {
            throw new Exceptions.OverflowException("加法运算整数溢出");
        }
    }

    public static long CheckedSubtract(long a, long b) {
        try {
            return checked(a - b);
        } catch (System.OverflowException) {
            throw new Exceptions.OverflowException("减法运算整数溢出");
        }
    }

    public static long CheckedMultiply(long a, long b) {
        try {
            return checked(a * b);
        } catch (System.OverflowException) {
            throw new Exceptions.OverflowException("乘法运算整数溢出");
        }
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
    private static bool IsINF(object value) => value is double d && double.IsPositiveInfinity(d);

    public static object EvaluateBinary(BinaryExpressionType type, object left, object right) {
        if (type == BinaryExpressionType.Plus) {
            if (left is string || right is string) return ToString(left) + ToString(right);
        }

        var (promotedLeft, promotedRight) = Promote(left, right);

        return type switch {
            BinaryExpressionType.Plus => EvaluatePlus(promotedLeft, promotedRight),
            BinaryExpressionType.Minus => EvaluateMinus(promotedLeft, promotedRight),
            BinaryExpressionType.Multiply => EvaluateMultiply(promotedLeft, promotedRight),
            BinaryExpressionType.Divide => EvaluateDivide(promotedLeft, promotedRight),
            BinaryExpressionType.IntegerDivide => EvaluateIntegerDivide(promotedLeft, promotedRight),
            BinaryExpressionType.Modulo => EvaluateModulo(promotedLeft, promotedRight),
            BinaryExpressionType.Power => EvaluatePower(promotedLeft, promotedRight),
            BinaryExpressionType.BitwiseAnd => EvaluateBitwiseAnd(left, right),
            BinaryExpressionType.BitwiseOr => EvaluateBitwiseOr(left, right),
            BinaryExpressionType.BitwiseXor => EvaluateBitwiseXor(left, right),
            BinaryExpressionType.LeftShift => EvaluateLeftShift(left, right),
            BinaryExpressionType.RightShift => EvaluateRightShift(left, right),
            BinaryExpressionType.Equal => EvaluateEqual(left, right),
            BinaryExpressionType.NotEqual => EvaluateNotEqual(left, right),
            BinaryExpressionType.LessThan => EvaluateLessThan(promotedLeft, promotedRight),
            BinaryExpressionType.LessThanOrEqual => EvaluateLessThanOrEqual(promotedLeft, promotedRight),
            BinaryExpressionType.GreaterThan => EvaluateGreaterThan(promotedLeft, promotedRight),
            BinaryExpressionType.GreaterThanOrEqual => EvaluateGreaterThanOrEqual(promotedLeft, promotedRight),
            BinaryExpressionType.And => EvaluateAnd(left, right),
            BinaryExpressionType.Or => EvaluateOr(left, right),
            _ => throw new Exceptions.InvalidOperationException($"未知的二元运算符：{type}")
        };
    }

    private static object EvaluatePlus(object left, object right) {
        if (IsNaN(left) || IsNaN(right))
            return double.NaN;
        if (IsINF(left) || IsINF(right))
            return double.PositiveInfinity;

        if (left is long l1 && right is long l2)
            return CheckedAdd(l1, l2);
        if (left is double d1 && right is double d2)
            return d1 + d2;
        throw new TypeMismatchException("加法运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static object EvaluateMinus(object left, object right) {
        if (IsNaN(left) || IsNaN(right))
            return double.NaN;
        if (IsINF(left) && IsINF(right))
            return double.NaN;
        if (IsINF(left))
            return double.PositiveInfinity;
        if (IsINF(right))
            return double.NegativeInfinity;

        if (left is long l1 && right is long l2)
            return CheckedSubtract(l1, l2);
        if (left is double d1 && right is double d2)
            return d1 - d2;
        throw new TypeMismatchException("减法运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static object EvaluateMultiply(object left, object right) {
        if (IsNaN(left) || IsNaN(right))
            return double.NaN;
        if (IsINF(left) || IsINF(right)) {
            var other = IsINF(left) ? right : left;
            if (other is long l && l == 0)
                return double.NaN;
            if (other is double d && d == 0)
                return double.NaN;
            return double.PositiveInfinity;
        }

        if (left is long l1 && right is long l2)
            return CheckedMultiply(l1, l2);
        if (left is double d1 && right is double d2)
            return d1 * d2;
        throw new TypeMismatchException("乘法运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static double EvaluateDivide(object left, object right) {
        if (IsNaN(left) || IsNaN(right)) return double.NaN;

        if (IsINF(left)) return IsINF(right) ? double.NaN : double.PositiveInfinity;

        if (IsINF(right)) return 0.0;

        if (left is long l1 && right is long l2) {
            if (l2 == 0) throw new DivisionByZeroException();
            return (double)l1 / l2;
        }
        if (left is double d1 && right is double d2) {
            if (d2 == 0) throw new DivisionByZeroException();
            return d1 / d2;
        }
        throw new TypeMismatchException("除法运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static object EvaluateIntegerDivide(object left, object right) {
        if (IsNaN(left) || IsNaN(right) || IsINF(left) || IsINF(right)) throw new EvaluateException("整除运算不支持 NaN 或 INF");

        long l1, l2;

        if (left is double d1) l1 = (long)d1;
        else if (left is long l) l1 = l;
        else throw new TypeMismatchException("整除运算需要数值类型", "number", GetTypeName(left, right));

        if (right is double d2) l2 = (long)d2;
        else if (right is long l) l2 = l;
        else throw new TypeMismatchException("整除运算需要数值类型", "number", GetTypeName(left, right));

        if (l2 == 0) throw new DivisionByZeroException();

        return l1 / l2;
    }

    private static object EvaluateModulo(object left, object right) {
        if (IsNaN(left) || IsNaN(right)) return double.NaN;

        if (IsINF(left)) return double.NaN;

        if (IsINF(right)) {
            if (left is long l) return (double)l;
            return left;
        }

        if (left is long l1 && right is long l2) {
            if (l2 == 0) throw new DivisionByZeroException();
            return l1 % l2;
        }
        if (left is double d1 && right is double d2) {
            if (d2 == 0) throw new DivisionByZeroException();
            return d1 % d2;
        }
        throw new TypeMismatchException("取模运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static double EvaluatePower(object left, object right) {
        if (IsNaN(left) || IsNaN(right)) return double.NaN;

        if (left is long l1 && right is long l2) {
            if (l1 < 0 && l2 < 0) throw new EvaluateException("不能对负数求负数次幂");
            if (l1 < 0 && l2 != (long)Math.Floor((double)l2)) throw new EvaluateException("不能对负数求非整数次幂");

            var result = Math.Pow(l1, l2);

            if (l1 == 0 && l2 < 0) throw new EvaluateException("零不能求负数次幂");

            if (l2 >= 0 && result == Math.Floor(result) && result >= long.MinValue && result <= long.MaxValue) return (long)result;

            return result;
        }
        if (left is double d1 && right is double d2) {
            if (d1 < 0 && d2 != Math.Floor(d2)) throw new EvaluateException("不能对负数求非整数次幂");
            return Math.Pow(d1, d2);
        }
        throw new TypeMismatchException("幂运算需要数值类型", "number", GetTypeName(left, right));
    }

    private static long ToLong(object value) {
        if (value is bool b) return b ? 1L : 0L;
        if (value is long l) return l;
        if (value is double d) return (long)d;
        throw new TypeMismatchException("期望数值类型", "number", GetTypeName(value, null));
    }

    private static long EvaluateBitwiseAnd(object left, object right) {
        return ToLong(left) & ToLong(right);
    }

    private static long EvaluateBitwiseOr(object left, object right) {
        return ToLong(left) | ToLong(right);
    }

    private static long EvaluateBitwiseXor(object left, object right) {
        return ToLong(left) ^ ToLong(right);
    }

    private static long EvaluateLeftShift(object left, object right) {
        long l1 = ToLong(left);
        long l2 = ToLong(right);
        if (l2 < 0) throw new EvaluateException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 << (int)l2;
    }

    private static long EvaluateRightShift(object left, object right) {
        long l1 = ToLong(left);
        long l2 = ToLong(right);
        if (l2 < 0) throw new EvaluateException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return l1 >> (int)l2;
    }

    private static bool EvaluateEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            if (IsNaN(left) || IsNaN(right)) return false;
            var (pl, pr) = Promote(left, right);
            if (pl is double dl && pr is double dr) return dl == dr;
            return Equals(pl, pr);
        }

        if (left.GetType() != right.GetType()) return false;
        return Equals(left, right);
    }

    private static bool EvaluateNotEqual(object left, object right) {
        var leftIsNumber = left is long || left is double;
        var rightIsNumber = right is long || right is double;

        if (leftIsNumber && rightIsNumber) {
            if (IsNaN(left) || IsNaN(right)) return true;
            var (pl, pr) = Promote(left, right);
            if (pl is double dl && pr is double dr) return dl != dr;
            return !Equals(pl, pr);
        }

        if (left.GetType() != right.GetType()) return true;
        return !Equals(left, right);
    }

    private static bool EvaluateLessThan(object left, object right) {
        if (left is long l1 && right is long l2) return l1 < l2;
        if (left is double d1 && right is double d2) return d1 < d2;
        if (left is string s1 && right is string s2) return string.Compare(s1, s2, StringComparison.Ordinal) < 0;
        throw new TypeMismatchException("比较运算需要兼容类型", "number ? string", GetTypeName(left, right));
    }

    private static bool EvaluateLessThanOrEqual(object left, object right) {
        if (left is long l1 && right is long l2) return l1 <= l2;
        if (left is double d1 && right is double d2) return d1 <= d2;
        if (left is string s1 && right is string s2) return string.Compare(s1, s2, StringComparison.Ordinal) <= 0;
        throw new TypeMismatchException("比较运算需要兼容类型", "number ? string", GetTypeName(left, right));
    }

    private static bool EvaluateGreaterThan(object left, object right) {
        if (left is long l1 && right is long l2) return l1 > l2;
        if (left is double d1 && right is double d2) return d1 > d2;
        if (left is string s1 && right is string s2) return string.Compare(s1, s2, StringComparison.Ordinal) > 0;
        throw new TypeMismatchException("比较运算需要兼容类型", "number ? string", GetTypeName(left, right));
    }

    private static bool EvaluateGreaterThanOrEqual(object left, object right) {
        if (left is long l1 && right is long l2) return l1 >= l2;
        if (left is double d1 && right is double d2) return d1 >= d2;
        if (left is string s1 && right is string s2) return string.Compare(s1, s2, StringComparison.Ordinal) >= 0;
        throw new TypeMismatchException("比较运算需要兼容类型", "number ? string", GetTypeName(left, right));
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

    private static object EvaluatePositive(object operand) {
        if (operand is long l) return l;
        if (operand is double d) return d;
        throw new TypeMismatchException("正号运算符需要数值类型", "number", GetTypeName(operand, null));
    }

    private static object EvaluateNegate(object operand) {
        if (operand is long l) {
            try {
                return checked(-l);
            } catch (System.OverflowException) {
                throw new Exceptions.OverflowException("取负运算整数溢出");
            }
        }
        if (operand is double d) return -d;
        throw new TypeMismatchException("取负运算符需要数值类型", "number", GetTypeName(operand, null));
    }

    private static object EvaluateNot(object operand) {
        RequireBool(operand);
        return !(bool)operand;
    }

    private static object EvaluateBitwiseNot(object operand) {
        long l = ToLong(operand);
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