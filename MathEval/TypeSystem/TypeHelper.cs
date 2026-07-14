using MathEval.Exceptions;
using MathEval.Parser;

namespace MathEval.TypeSystem;

public static class TypeHelper {
    public static double ToDouble(object value) {
        return value switch {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            bool bl => bl ? 1.0 : 0.0,
            short s => s,
            sbyte sb => sb,
            ushort us => us,
            uint ui => ui,
            ulong ul => ul,
            byte b => b,
            decimal dec => (double)dec,
            _ => throw new TypeMismatchException("期望数值类型", "number", value?.GetType().Name ?? "null")
        };
    }

    /// <summary>
    /// 判断 double 值是否为真（非零且非 NaN），与 Fast 项目 ConvertToBool 语义一致
    /// </summary>
    public static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);

    public static long ToInteger(object value, string operationName) {
        double d = ToDouble(value);
        // 超出 long 范围的整型 double（如 1e19）不可安全地转换为 long，
        // 必须先做范围检查，否则 (long)d 会产生实现定义的饱和值（数据错误）。
        if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d)
            && d >= long.MinValue && d < 9223372036854775808.0) return (long)d;
        throw new TypeMismatchException($"{operationName} 需要整数操作数", "integer", value?.GetType().Name ?? "null");
    }

    public static object EvaluateBinary(BinaryExpressionType type, object left, object right) {
        // Array operations
        if (left is double[] || right is double[]) return EvaluateBinaryArray(type, left, right);

        // Scalar operations - pure double, zero branching
        var (d1, d2) = (ToDouble(left), ToDouble(right));
        return type switch {
            BinaryExpressionType.Plus => d1 + d2,
            BinaryExpressionType.Minus => d1 - d2,
            BinaryExpressionType.Multiply => d1 * d2,
            BinaryExpressionType.Divide => EvaluateDivide(d1, d2),
            BinaryExpressionType.IntegerDivide => EvaluateIntegerDivide(d1, d2),
            BinaryExpressionType.Remainder => EvaluateRemainder(d1, d2),
            BinaryExpressionType.Modulo => EvaluateModulo(d1, d2),
            BinaryExpressionType.Power => EvaluatePower(d1, d2),
            BinaryExpressionType.BitwiseAnd => (double)(ToInteger(left, "按位与") & ToInteger(right, "按位与")),
            BinaryExpressionType.BitwiseOr => (double)(ToInteger(left, "按位或") | ToInteger(right, "按位或")),
            BinaryExpressionType.BitwiseXor => (double)(ToInteger(left, "按位异或") ^ ToInteger(right, "按位异或")),
            BinaryExpressionType.LeftShift => EvaluateLeftShift(ToInteger(left, "左移"), ToInteger(right, "左移")),
            BinaryExpressionType.RightShift => EvaluateRightShift(ToInteger(left, "右移"), ToInteger(right, "右移")),
            BinaryExpressionType.UnsignedRightShift => (double)((ulong)ToInteger(left, "无符号右移") >> (int)EvaluateShiftAmount(ToInteger(right, "无符号右移"))),
            BinaryExpressionType.Equal => d1 == d2 ? 1.0 : 0.0,
            BinaryExpressionType.NotEqual => d1 != d2 ? 1.0 : 0.0,
            BinaryExpressionType.LessThan => d1 < d2 ? 1.0 : 0.0,
            BinaryExpressionType.LessThanOrEqual => d1 <= d2 ? 1.0 : 0.0,
            BinaryExpressionType.GreaterThan => d1 > d2 ? 1.0 : 0.0,
            BinaryExpressionType.GreaterThanOrEqual => d1 >= d2 ? 1.0 : 0.0,
            BinaryExpressionType.And => (IsTruthy(d1) && IsTruthy(d2)) ? 1.0 : 0.0,
            BinaryExpressionType.Or => (IsTruthy(d1) || IsTruthy(d2)) ? 1.0 : 0.0,
            _ => throw new System.InvalidOperationException($"未知的二元运算符：{type}")
        };
    }

    private static double[] EvaluateBinaryArray(BinaryExpressionType type, object left, object right) {
        if (left is double[] lhs && right is double[] rhs) return ElementWise(lhs, rhs, type);

        double[] arr = (left as double[]) ?? (right as double[])!;
        if (arr == null) {
            throw new TypeMismatchException("数组运算需要数值类型", "number|array",
                $"{left?.GetType().Name ?? "null"}, {right?.GetType().Name ?? "null"}");
        }

        // 非 double 标量（int/long/bool 等）统一转换为 double 后再参与 element-wise 运算
        var scalar = ToDouble(left is double[] ? right : left);
        return left is double[] ? ElementWise(arr, scalar, type) : ElementWise(scalar, arr, type);
    }

    private static double[] ElementWise(double[] a, double[] b, BinaryExpressionType type) {
        if (a.Length != b.Length) throw new EvaluateException($"数组长度不匹配：{a.Length} vs {b.Length}");
        var result = new double[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = ToDouble(EvaluateBinary(type, a[i], b[i]));
        return result;
    }

    private static double[] ElementWise(double[] a, double scalar, BinaryExpressionType type) {
        var result = new double[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = ToDouble(EvaluateBinary(type, a[i], scalar));
        return result;
    }

    private static double[] ElementWise(double scalar, double[] b, BinaryExpressionType type) {
        var result = new double[b.Length];
        for (int i = 0; i < b.Length; i++) result[i] = ToDouble(EvaluateBinary(type, scalar, b[i]));
        return result;
    }

    public static object EvaluateUnary(UnaryExpressionType type, object operand) {
        if (operand is double[] arr) return EvaluateUnaryArray(type, arr);

        var d = ToDouble(operand);
        return type switch {
            UnaryExpressionType.Positive => d,
            UnaryExpressionType.Negate => -d,
            UnaryExpressionType.Not => !IsTruthy(d) ? 1.0 : 0.0,
            UnaryExpressionType.BitwiseNot => (double)(~ToInteger(operand, "按位取反")),
            _ => throw new System.InvalidOperationException($"未知的一元运算符：{type}")
        };
    }

    private static double[] EvaluateUnaryArray(UnaryExpressionType type, double[] arr) {
        var result = new double[arr.Length];
        for (int i = 0; i < arr.Length; i++) result[i] = ToDouble(EvaluateUnary(type, arr[i]));
        return result;
    }

    private static double EvaluateDivide(double d1, double d2) {
        return d1 / d2;
    }

    private static double EvaluateIntegerDivide(double d1, double d2) {
        return Math.Truncate(d1 / d2);
    }

    private static double EvaluateRemainder(double d1, double d2) {
        return d1 % d2;
    }

    private static double EvaluateModulo(double d1, double d2) {
        double r = d1 % d2;
        if ((r < 0 && d2 > 0) || (r > 0 && d2 < 0)) r += d2;
        return r;
    }

    private static double EvaluatePower(double d1, double d2) {
        return Math.Pow(d1, d2);
    }

    private static int EvaluateShiftAmount(long amount) {
        if (amount < 0) throw new EvaluateException("移位量不能为负数");
        return (int)(amount & 0x3F);
    }

    private static double EvaluateLeftShift(long value, long amount) {
        return value << EvaluateShiftAmount(amount);
    }

    private static double EvaluateRightShift(long value, long amount) {
        return value >> EvaluateShiftAmount(amount);
    }
}