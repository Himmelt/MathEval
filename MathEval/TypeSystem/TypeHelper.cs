using MathEval.Exceptions;
using MathEval.Parser;

namespace MathEval.TypeSystem;

public static class TypeHelper {
    // ============ 边界转换（object 世界，保留给 API 边界与函数委托） ============

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

    public static long ToInteger(object value, string operationName) {
        double d = ToDouble(value);
        return ToLongChecked(d, operationName, value?.GetType().Name ?? "null");
    }

    // ============ MathValue 内核 ============

    /// <summary>
    /// 真值判断（bool 数值语义的内核形态）：非零且非 NaN，与 Fast 项目 ConvertToBool 语义一致
    /// </summary>
    public static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>MathValue 真值判断：仅 Number 有真值语义，Text/数组参与逻辑运算即类型错误</summary>
    public static bool IsTruthy(MathValue value) => value.Kind == MathKind.Number
        ? IsTruthy(value.AsNumber)
        : throw new TypeMismatchException($"逻辑运算需要数值类型", "number", value.KindName);

    /// <summary>MathValue 整数化（数组索引/位运算），超范围或非整数抛 TypeMismatch</summary>
    public static long ToInteger(MathValue value, string operationName) => value.Kind == MathKind.Number
        ? ToLongChecked(value.AsNumber, operationName, "number")
        : throw new TypeMismatchException($"{operationName} 需要整数操作数", "integer", value.KindName);

    public static MathValue EvaluateBinary(BinaryExpressionType type, MathValue left, MathValue right) {
        // 快路径前置：Number×Number 纯 double 运算，零装箱（PGO 分支预测收敛点）
        if (left.Kind == MathKind.Number && right.Kind == MathKind.Number)
            return MathValue.Number(EvaluateNumberOp(type, left.AsNumber, right.AsNumber));

        // 文本运算：Text×Text 拼接（+）与序数比较（==/!=/</<=/>/>=）
        if (left.Kind == MathKind.Text && right.Kind == MathKind.Text)
            return EvaluateTextOp(type, left.AsText, right.AsText);

        // 数组广播：任一操作数为 NumberArray
        if (left.Kind == MathKind.NumberArray || right.Kind == MathKind.NumberArray)
            return MathValue.Array(EvaluateBinaryArray(type, left, right));

        throw new TypeMismatchException($"运算符 {type} 不支持 {left.KindName} 与 {right.KindName}",
            "number|text|array", $"{left.KindName}, {right.KindName}");
    }

    public static MathValue EvaluateUnary(UnaryExpressionType type, MathValue operand) {
        if (operand.Kind == MathKind.NumberArray) {
            var arr = operand.AsNumberArray;
            var result = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++) result[i] = EvaluateNumberUnary(type, arr[i]);
            return MathValue.Array(result);
        }

        if (operand.Kind != MathKind.Number)
            throw new TypeMismatchException($"一元运算符 {type} 不支持 {operand.KindName}", "number", operand.KindName);

        return MathValue.Number(EvaluateNumberUnary(type, operand.AsNumber));
    }

    /// <summary>
    /// 数组索引求值（解释模式与编译模式共享，保证行为一致）。
    /// 合成索引（IndexPushdownOptimizer 生成）对标量值静默返回标量本身；
    /// 用户原始编写的标量索引抛类型错误。
    /// </summary>
    public static MathValue ArrayIndex(MathValue array, MathValue index, bool isSynthetic) {
        var intIndex = ToInteger(index, "数组索引");

        if (array.Kind == MathKind.NumberArray) {
            var arr = array.AsNumberArray;
            if (intIndex < 0 || intIndex >= arr.Length)
                throw new EvaluateException($"索引 {intIndex} 超出数组范围 [0, {arr.Length})");
            return MathValue.Number(arr[intIndex]);
        }

        if (isSynthetic && array.Kind == MathKind.Number) return array;

        throw new TypeMismatchException("索引操作需要数组类型", "array", array.KindName);
    }

    // ============ 内部计算（纯 double，无类型判断） ============

    private static double EvaluateNumberOp(BinaryExpressionType type, double d1, double d2) {
        return type switch {
            BinaryExpressionType.Plus => d1 + d2,
            BinaryExpressionType.Minus => d1 - d2,
            BinaryExpressionType.Multiply => d1 * d2,
            BinaryExpressionType.Divide => d1 / d2,
            BinaryExpressionType.IntegerDivide => Math.Truncate(d1 / d2),
            BinaryExpressionType.Remainder => d1 % d2,
            BinaryExpressionType.Modulo => EvaluateModulo(d1, d2),
            BinaryExpressionType.Power => Math.Pow(d1, d2),
            BinaryExpressionType.BitwiseAnd => ToLongChecked(d1, "按位与", "number") & ToLongChecked(d2, "按位与", "number"),
            BinaryExpressionType.BitwiseOr => ToLongChecked(d1, "按位或", "number") | ToLongChecked(d2, "按位或", "number"),
            BinaryExpressionType.BitwiseXor => ToLongChecked(d1, "按位异或", "number") ^ ToLongChecked(d2, "按位异或", "number"),
            BinaryExpressionType.LeftShift => EvaluateLeftShift(ToLongChecked(d1, "左移", "number"), ToLongChecked(d2, "左移", "number")),
            BinaryExpressionType.RightShift => EvaluateRightShift(ToLongChecked(d1, "右移", "number"), ToLongChecked(d2, "右移", "number")),
            BinaryExpressionType.UnsignedRightShift => (double)((ulong)ToLongChecked(d1, "无符号右移", "number") >> (int)EvaluateShiftAmount(ToLongChecked(d2, "无符号右移", "number"))),
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

    private static double EvaluateNumberUnary(UnaryExpressionType type, double d) {
        return type switch {
            UnaryExpressionType.Positive => d,
            UnaryExpressionType.Negate => -d,
            UnaryExpressionType.Not => !IsTruthy(d) ? 1.0 : 0.0,
            UnaryExpressionType.BitwiseNot => ~ToLongChecked(d, "按位取反", "number"),
            _ => throw new System.InvalidOperationException($"未知的一元运算符：{type}")
        };
    }

    /// <summary>
    /// 文本二元运算：+ 为拼接，比较运算采用序数（Ordinal）比较以保证结果与区域设置无关，
    /// 与数值比较一致返回 1.0/0.0；其余运算符不支持文本。
    /// </summary>
    private static MathValue EvaluateTextOp(BinaryExpressionType type, string s1, string s2) {
        switch (type) {
            case BinaryExpressionType.Plus:
                return MathValue.Text(string.Concat(s1, s2));
            case BinaryExpressionType.Equal:
                return MathValue.Number(string.Equals(s1, s2, StringComparison.Ordinal) ? 1.0 : 0.0);
            case BinaryExpressionType.NotEqual:
                return MathValue.Number(!string.Equals(s1, s2, StringComparison.Ordinal) ? 1.0 : 0.0);
            case BinaryExpressionType.LessThan:
            case BinaryExpressionType.LessThanOrEqual:
            case BinaryExpressionType.GreaterThan:
            case BinaryExpressionType.GreaterThanOrEqual: {
                    int cmp = string.CompareOrdinal(s1, s2);
                    bool result = type switch {
                        BinaryExpressionType.LessThan => cmp < 0,
                        BinaryExpressionType.LessThanOrEqual => cmp <= 0,
                        BinaryExpressionType.GreaterThan => cmp > 0,
                        _ => cmp >= 0,
                    };
                    return MathValue.Number(result ? 1.0 : 0.0);
                }
            default:
                throw new TypeMismatchException($"运算符 {type} 不支持 text 与 text", "text(+|==|!=|<|<=|>|>=)", "text");
        }
    }

    private static double[] EvaluateBinaryArray(BinaryExpressionType type, MathValue left, MathValue right) {
        // 数组 × 数组
        if (left.Kind == MathKind.NumberArray && right.Kind == MathKind.NumberArray) {
            var a = left.AsNumberArray;
            var b = right.AsNumberArray;
            if (a.Length != b.Length) throw new EvaluateException($"数组长度不匹配：{a.Length} vs {b.Length}");
            var result = new double[a.Length];
            for (int i = 0; i < a.Length; i++) result[i] = EvaluateNumberOp(type, a[i], b[i]);
            return result;
        }

        // 数组 × 标量（广播）
        if (left.Kind == MathKind.NumberArray && right.Kind == MathKind.Number) {
            var a = left.AsNumberArray;
            var s = right.AsNumber;
            var result = new double[a.Length];
            for (int i = 0; i < a.Length; i++) result[i] = EvaluateNumberOp(type, a[i], s);
            return result;
        }
        if (left.Kind == MathKind.Number && right.Kind == MathKind.NumberArray) {
            var b = right.AsNumberArray;
            var s = left.AsNumber;
            var result = new double[b.Length];
            for (int i = 0; i < b.Length; i++) result[i] = EvaluateNumberOp(type, s, b[i]);
            return result;
        }

        // NumberArray 与 Text/TextArray 组合
        throw new TypeMismatchException("数组运算需要数值类型", "number|array",
            $"{left.KindName}, {right.KindName}");
    }

    /// <summary>
    /// double → long 的安全整数转换：超范围/非整数/NaN/Inf 均抛 TypeMismatch，
    /// 否则 (long)d 会产生实现定义的饱和值（数据错误）。
    /// </summary>
    private static long ToLongChecked(double d, string operationName, string actualTypeName) {
        if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d)
            && d >= long.MinValue && d < 9223372036854775808.0) return (long)d;
        throw new TypeMismatchException($"{operationName} 需要整数操作数", "integer", actualTypeName);
    }

    private static double EvaluateModulo(double d1, double d2) {
        double r = d1 % d2;
        if ((r < 0 && d2 > 0) || (r > 0 && d2 < 0)) r += d2;
        return r;
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
