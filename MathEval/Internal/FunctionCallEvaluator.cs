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
        double[]? broadcastArray = null;
        foreach (var arg in args) {
            if (arg.Kind == MathKind.NumberArray) {
                broadcastArray = arg.AsNumberArray;
                break;
            }
        }

        if (broadcastArray != null) {
            return MathValue.Array(Broadcast(func, args, broadcastArray));
        }

        return MathValue.FromObject(func(ToObjectArgs(args)));
    }

    /// <summary>
    /// 逐元素广播：数组参数按索引取元素、标量参数保持不变，逐次调用函数
    /// </summary>
    private static double[] Broadcast(ExpressionFunction func, MathValue[] args, double[] broadcastArray) {
        // 校验所有数组参数长度一致
        foreach (var arg in args) {
            if (arg.Kind == MathKind.NumberArray) {
                var da = arg.AsNumberArray;
                if (da.Length != broadcastArray.Length) {
                    throw new EvaluateException(
                        $"数组广播时所有数组参数长度必须一致，但遇到长度 {da.Length} 和 {broadcastArray.Length}");
                }
            }
        }

        var result = new double[broadcastArray.Length];
        for (int i = 0; i < broadcastArray.Length; i++) {
            var scalarArgs = new object[args.Length];
            for (int j = 0; j < args.Length; j++) {
                scalarArgs[j] = args[j].Kind == MathKind.NumberArray
                    ? args[j].AsNumberArray[i]      // 装箱 double（函数边界）
                    : args[j].ToObject();
            }
            result[i] = TypeHelper.ToDouble(func(scalarArgs));
        }
        return result;
    }

    /// <summary>
    /// 将参数展平：数组参数拆分为单个元素，标量参数保持不变（聚合函数用）
    /// </summary>
    private static object[] FlattenArgs(MathValue[] args) {
        var list = new List<object>();
        foreach (var arg in args) {
            if (arg.Kind == MathKind.NumberArray) {
                foreach (var item in arg.AsNumberArray) list.Add(item);
            } else {
                list.Add(arg.ToObject());
            }
        }
        return [.. list];
    }

    private static object[] ToObjectArgs(MathValue[] args) {
        var result = new object[args.Length];
        for (int i = 0; i < args.Length; i++) result[i] = args[i].ToObject();
        return result;
    }
}
