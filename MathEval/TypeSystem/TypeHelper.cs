using MathEval.Exceptions;
using MathEval.Parser;
using System.Globalization;

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

        // 文本数组广播：TextArray 与 TextArray/Text（拼接与比较）
        if (left.Kind == MathKind.TextArray || right.Kind == MathKind.TextArray)
            return EvaluateTextArrayOp(type, left, right);

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

        if (array.Kind == MathKind.TextArray) {
            var arr = array.AsTextArray;
            if (intIndex < 0 || intIndex >= arr.Length)
                throw new EvaluateException($"索引 {intIndex} 超出数组范围 [0, {arr.Length})");
            return MathValue.Text(arr[intIndex]);
        }

        if (isSynthetic && (array.Kind == MathKind.Number || array.Kind == MathKind.Text)) return array;

        throw new TypeMismatchException("索引操作需要数组类型", "array", array.KindName);
    }

    /// <summary>
    /// 数组字面量构造（解释模式与编译模式共享）：按首个元素的 Kind 推断数组类型，
    /// 全 number → number[]，全 text → text[]，空数组 → number[]，元素类型不一致抛 TypeMismatch。
    /// </summary>
    public static MathValue BuildArrayLiteral(MathValue[] elements) {
        if (elements.Length == 0) return MathValue.Array(System.Array.Empty<double>());

        var kind = elements[0].Kind;
        switch (kind) {
            case MathKind.Number: {
                    var result = new double[elements.Length];
                    for (int i = 0; i < elements.Length; i++) {
                        if (elements[i].Kind != MathKind.Number)
                            throw new TypeMismatchException("数组字面量元素类型必须一致", "number[]",
                                $"{KindNameOf(elements[i].Kind)}（第 {i} 个元素）");
                        result[i] = elements[i].AsNumber;
                    }
                    return MathValue.Array(result);
                }
            case MathKind.Text: {
                    var result = new string[elements.Length];
                    for (int i = 0; i < elements.Length; i++) {
                        if (elements[i].Kind != MathKind.Text)
                            throw new TypeMismatchException("数组字面量元素类型必须一致", "text[]",
                                $"{KindNameOf(elements[i].Kind)}（第 {i} 个元素）");
                        result[i] = elements[i].AsText;
                    }
                    return MathValue.Array(result);
                }
            default:
                throw new TypeMismatchException("数组字面量元素必须是 number 或 text", "number|text",
                    elements[0].KindName);
        }
    }

    private static string KindNameOf(MathKind kind) => kind switch {
        MathKind.Number => "number",
        MathKind.Text => "text",
        MathKind.NumberArray => "number[]",
        MathKind.TextArray => "text[]",
        _ => kind.ToString(),
    };

    // ============ 插值显示与格式化 ============

    /// <summary>
    /// 插值段的默认显示：Number 用 G 格式（NaN/INF 特殊显示，InvariantCulture），
    /// Text 原样，数组显示 Kind 与长度
    /// </summary>
    public static string ToDisplayString(MathValue value) => value.Kind switch {
        MathKind.Number => FormatNumberG(value.AsNumber),
        MathKind.Text => value.AsText,
        _ => value.ToString(),
    };

    /// <summary>
    /// 插值段格式化：仅数值支持 d/e/f/g/x 说明符；d/x 要求整数值。
    /// Text/数组使用格式说明符抛 EvaluateException。
    /// </summary>
    public static string Format(MathValue value, string formatSpec) {
        if (value.Kind != MathKind.Number)
            throw new EvaluateException($"格式说明符 '{formatSpec}' 只能用于数值类型");

        double d = value.AsNumber;
        var firstChar = char.ToLowerInvariant(formatSpec[0]);
        var supportedFormats = new[] { 'd', 'e', 'f', 'g', 'x' };
        if (!supportedFormats.Contains(firstChar))
            throw new ParseException($"不支持的格式说明符：{formatSpec}", 1, 1);

        // D/X 格式只适用于整数：double 为数学整数时转换后格式化
        if (firstChar == 'd' || firstChar == 'x') {
            if (double.IsNaN(d) || double.IsInfinity(d) || d != Math.Truncate(d))
                throw new EvaluateException($"格式说明符 '{formatSpec}' 只能用于整数，但值为 {FormatNumberG(d)}");
            return string.Format(CultureInfo.InvariantCulture, $"{{0:{formatSpec}}}", (long)d);
        }

        return string.Format(CultureInfo.InvariantCulture, $"{{0:{formatSpec}}}", d);
    }

    private static string FormatNumberG(double d) =>
        double.IsNaN(d) ? "NaN"
        : double.IsPositiveInfinity(d) ? "INF"
        : double.IsNegativeInfinity(d) ? "-INF"
        : d.ToString("G", CultureInfo.InvariantCulture);

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

    /// <summary>
    /// 文本数组二元运算广播：TextArray×TextArray（等长）/ TextArray×Text / Text×TextArray。
    /// + 为逐元素拼接（结果 text[]），比较为逐元素序数比较（结果 number[]），其余运算符不支持。
    /// </summary>
    private static MathValue EvaluateTextArrayOp(BinaryExpressionType type, MathValue left, MathValue right) {
        if (type is not (BinaryExpressionType.Plus or BinaryExpressionType.Equal or BinaryExpressionType.NotEqual
            or BinaryExpressionType.LessThan or BinaryExpressionType.LessThanOrEqual
            or BinaryExpressionType.GreaterThan or BinaryExpressionType.GreaterThanOrEqual))
            throw new TypeMismatchException($"运算符 {type} 不支持 {left.KindName} 与 {right.KindName}",
                "text[](+|==|!=|<|<=|>|>=)", $"{left.KindName}, {right.KindName}");

        // 对齐为等长的两侧（数组×数组校验等长，标量侧按数组长度展开）
        string[] a, b;
        if (left.Kind == MathKind.TextArray && right.Kind == MathKind.TextArray) {
            a = left.AsTextArray;
            b = right.AsTextArray;
            if (a.Length != b.Length)
                throw new EvaluateException($"数组长度不匹配：{a.Length} vs {b.Length}");
        } else if (left.Kind == MathKind.TextArray) {
            a = left.AsTextArray;
            b = new string[a.Length];
            System.Array.Fill(b, right.AsText);
        } else {
            b = right.AsTextArray;
            a = new string[b.Length];
            System.Array.Fill(a, left.AsText);
        }

        if (type == BinaryExpressionType.Plus) {
            var result = new string[a.Length];
            for (int i = 0; i < a.Length; i++) result[i] = string.Concat(a[i], b[i]);
            return MathValue.Array(result);
        }

        var cmpResult = new double[a.Length];
        for (int i = 0; i < a.Length; i++) {
            int cmp = string.CompareOrdinal(a[i], b[i]);
            cmpResult[i] = type switch {
                BinaryExpressionType.Equal => a[i] == b[i] ? 1.0 : 0.0,
                BinaryExpressionType.NotEqual => a[i] != b[i] ? 1.0 : 0.0,
                BinaryExpressionType.LessThan => cmp < 0 ? 1.0 : 0.0,
                BinaryExpressionType.LessThanOrEqual => cmp <= 0 ? 1.0 : 0.0,
                BinaryExpressionType.GreaterThan => cmp > 0 ? 1.0 : 0.0,
                _ => cmp >= 0 ? 1.0 : 0.0,
            };
        }
        return MathValue.Array(cmpResult);
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
