using MathEval.Context;
using MathEval.Exceptions;
using MathEval.TypeSystem;

namespace MathEval.Internal;

/// <summary>
/// 共享的函数调用求值逻辑，供解释模式（EvaluationVisitor）和编译模式（CompiledExpression）复用。
/// 内核值流为 MathValue；函数委托边界（object[]）经 ToObject/FromObject 转换，
/// 数值参数的装箱只发生在函数调用边界（非运算热路径，见设计文档 ADR-D9）。
/// </summary>
internal static class FunctionCallEvaluator {
    /// <summary>
    /// 调用函数，处理数组广播或聚合展平
    /// </summary>
    /// <param name="func">函数委托</param>
    /// <param name="args">参数数组</param>
    /// <param name="isAggregate">是否为聚合函数</param>
    public static MathValue Evaluate(ExpressionFunction func, MathValue[] args, bool isAggregate) {
        if (isAggregate) {
            // 聚合函数：展平数组参数后全局归约
            return MathValue.FromObject(func(FlattenArgs(args)));
        }

        // 非聚合函数：检测数组参数做 element-wise 广播
        int broadcastLength = -1;
        foreach (var arg in args) {
            if (arg.Kind is MathKind.NumberArray or MathKind.TextArray) {
                var len = GetArrayLength(arg);
                if (broadcastLength == -1) broadcastLength = len;
                else if (broadcastLength != len)
                    throw new EvaluateException(
                        $"数组广播时所有数组参数长度必须一致，但遇到长度 {broadcastLength} 和 {len}");
            }
        }

        if (broadcastLength != -1)
            return Broadcast(func, args, broadcastLength);

        return MathValue.FromObject(func(ToObjectArgs(args)));
    }

    /// <summary>
    /// 逐元素广播：数组参数按索引取元素、标量参数保持不变，逐次调用函数。
    /// 结果 Kind 按各次返回值统一推断：全 double → number[]，全 string → text[]，不一致抛 TypeMismatch。
    /// </summary>
    private static MathValue Broadcast(ExpressionFunction func, MathValue[] args, int length) {
        var resultKind = MathKind.Number;
        var numbers = new double[length];
        var texts = new string[length];

        for (int i = 0; i < length; i++) {
            var scalarArgs = new object[args.Length];
            for (int j = 0; j < args.Length; j++)
                scalarArgs[j] = GetElementAt(args[j], i);     // 数组取元素（边界装箱），标量原样

            var result = func(scalarArgs);
            if (result is string s) {
                if (i > 0 && resultKind != MathKind.Text)
                    throw new TypeMismatchException("函数广播结果的元素类型必须一致", "number[]|text[]", "mixed");
                resultKind = MathKind.Text;
                texts[i] = s;
            } else {
                if (i > 0 && resultKind != MathKind.Number)
                    throw new TypeMismatchException("函数广播结果的元素类型必须一致", "number[]|text[]", "mixed");
                resultKind = MathKind.Number;
                numbers[i] = TypeHelper.ToDouble(result);
            }
        }

        return resultKind == MathKind.Text ? MathValue.Array(texts) : MathValue.Array(numbers);
    }

    /// <summary>
    /// 将参数展平：数组参数拆分为单个元素，标量参数保持不变（聚合函数用）
    /// </summary>
    private static object[] FlattenArgs(MathValue[] args) {
        var list = new List<object>();
        foreach (var arg in args) {
            switch (arg.Kind) {
                case MathKind.NumberArray:
                    foreach (var item in arg.AsNumberArray) list.Add(item);
                    break;
                case MathKind.TextArray:
                    foreach (var item in arg.AsTextArray) list.Add(item);
                    break;
                default:
                    list.Add(arg.ToObject());
                    break;
            }
        }
        return [.. list];
    }

    private static int GetArrayLength(MathValue value) => value.Kind switch {
        MathKind.NumberArray => value.AsNumberArray.Length,
        MathKind.TextArray => value.AsTextArray.Length,
        _ => throw new TypeMismatchException("期望数组类型", "array", value.KindName),
    };

    private static object GetElementAt(MathValue value, int index) => value.Kind switch {
        MathKind.NumberArray => value.AsNumberArray[index],   // 装箱 double（函数边界）
        MathKind.TextArray => value.AsTextArray[index],
        _ => value.ToObject(),
    };

    private static object[] ToObjectArgs(MathValue[] args) {
        var result = new object[args.Length];
        for (int i = 0; i < args.Length; i++) result[i] = args[i].ToObject();
        return result;
    }
}
