using MathEval.Context;
using MathEval.Exceptions;
using System.Reflection;

namespace MathEval.Internal;

/// <summary>
/// 函数包装器，将强类型 Func 转换为 ExpressionFunction
/// </summary>
internal static class FunctionWrapper {
    public static ExpressionFunction Wrap<T1, TResult>(string name, Func<T1, TResult> func)
        => WrapCore(name, func, typeof(T1));

    public static ExpressionFunction Wrap<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2));

    public static ExpressionFunction Wrap<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
        => WrapCore(name, func, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));

    /// <summary>
    /// 统一包装逻辑：先安全地转换参数（捕获 FormatException/OverflowException/InvalidCastException），
    /// 再调用用户函数并解包其异常，全部包装为 MathEval 异常以维护异常契约。
    /// </summary>
    private static ExpressionFunction WrapCore(string name, Delegate func, params Type[] paramTypes) {
        return args => {
            if (args.Length != paramTypes.Length)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 {paramTypes.Length} 个参数，但提供了 {args.Length} 个");

            try {
                var converted = new object[paramTypes.Length];
                for (int i = 0; i < paramTypes.Length; i++) {
                    try {
                        converted[i] = Convert.ChangeType(args[i], paramTypes[i]);
                    } catch (Exception ex) when (ex is not MathEvalException) {
                        // Convert.ChangeType 可抛 InvalidCastException/FormatException/OverflowException
                        throw new FunctionTypeMismatchException($"函数 {name} 第 {i + 1} 个参数类型不匹配：{ex.Message}");
                    }
                }

                try {
                    var result = func.DynamicInvoke(converted);
                    return result!;
                } catch (TargetInvocationException ex) {
                    // 解包用户函数体内抛出的异常，重新包装为 MathEval 异常并保留内部异常
                    var inner = ex.InnerException ?? ex;
                    throw new EvaluateException($"调用函数 {name} 时出错：{inner.Message}", inner);
                } catch (Exception ex) when (ex is not MathEvalException) {
                    throw new EvaluateException($"调用函数 {name} 时出错：{ex.Message}", ex);
                }
            } catch (MathEvalException) {
                // 已为 MathEval 异常，直接透传，避免重复包装
                throw;
            }
        };
    }
}
