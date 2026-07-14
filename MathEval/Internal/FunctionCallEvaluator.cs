using MathEval.Context;
using MathEval.Exceptions;
using MathEval.TypeSystem;

namespace MathEval.Internal;

/// <summary>
/// 共享的函数调用求值逻辑，供解释模式（EvaluationVisitor）和编译模式（CompiledExpression）复用
/// </summary>
internal static class FunctionCallEvaluator {
    /// <summary>
    /// 调用函数，处理数组广播或聚合展平
    /// </summary>
    /// <param name="func">函数委托</param>
    /// <param name="args">参数数组</param>
    /// <param name="isAggregate">是否为聚合函数</param>
    public static object Evaluate(ExpressionFunction func, object[] args, bool isAggregate) {
        if (isAggregate) {
            // 聚合函数：展平数组参数后全局归约
            return func(FlattenArgs(args))!;
        }

        // 非聚合函数：检测数组参数做 element-wise 广播
        var arrayArg = args.FirstOrDefault(a => a is double[]);
        if (arrayArg is double[] arr) {
            // 校验所有数组参数长度一致
            foreach (var arg in args) {
                if (arg is double[] da && da.Length != arr.Length) {
                    throw new EvaluateException(
                        $"数组广播时所有数组参数长度必须一致，但遇到长度 {da.Length} 和 {arr.Length}");
                }
            }

            var result = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++) {
                var scalarArgs = new object[args.Length];
                for (int j = 0; j < args.Length; j++) {
                    scalarArgs[j] = args[j] is double[] da ? da[i] : args[j];
                }
                result[i] = TypeHelper.ToDouble(func(scalarArgs));
            }
            return result;
        }

        return func(args)!;
    }

    /// <summary>
    /// 将参数展平：数组参数拆分为单个元素，标量参数保持不变
    /// </summary>
    private static object[] FlattenArgs(object[] args) {
        var list = new List<object>();
        foreach (var arg in args) {
            if (arg is double[] arr) {
                foreach (var item in arr) list.Add(item);
            } else {
                list.Add(arg);
            }
        }
        return [.. list];
    }
}
