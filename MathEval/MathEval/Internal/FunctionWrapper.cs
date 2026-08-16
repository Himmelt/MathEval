using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Internal;

/// <summary>
/// 函数包装器，将强类型 Func 转换为 ExpressionFunction
/// </summary>
internal static class FunctionWrapper {
    /// <summary>
    /// 公共包装逻辑：参数数量校验 + 类型转换异常统一处理，
    /// 由各强类型重载提供具体的参数转换与调用委托，消除重复代码（OPT-10）
    /// </summary>
    private static ExpressionFunction WrapCore(string name, int argCount, Func<object[], object?> invoke) {
        return args => {
            if (args.Length != argCount)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");
            try {
                return invoke(args)!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, TResult>(string name, Func<T1, TResult> func)
        => WrapCore(name, 1, args => func((T1)Convert.ChangeType(args[0], typeof(T1))));

    public static ExpressionFunction Wrap<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
        => WrapCore(name, 2, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2))));

    public static ExpressionFunction Wrap<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
        => WrapCore(name, 3, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3))));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
        => WrapCore(name, 4, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3)),
            (T4)Convert.ChangeType(args[3], typeof(T4))));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
        => WrapCore(name, 5, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3)),
            (T4)Convert.ChangeType(args[3], typeof(T4)),
            (T5)Convert.ChangeType(args[4], typeof(T5))));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
        => WrapCore(name, 6, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3)),
            (T4)Convert.ChangeType(args[3], typeof(T4)),
            (T5)Convert.ChangeType(args[4], typeof(T5)),
            (T6)Convert.ChangeType(args[5], typeof(T6))));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
        => WrapCore(name, 7, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3)),
            (T4)Convert.ChangeType(args[3], typeof(T4)),
            (T5)Convert.ChangeType(args[4], typeof(T5)),
            (T6)Convert.ChangeType(args[5], typeof(T6)),
            (T7)Convert.ChangeType(args[6], typeof(T7))));

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
        => WrapCore(name, 8, args => func(
            (T1)Convert.ChangeType(args[0], typeof(T1)),
            (T2)Convert.ChangeType(args[1], typeof(T2)),
            (T3)Convert.ChangeType(args[2], typeof(T3)),
            (T4)Convert.ChangeType(args[3], typeof(T4)),
            (T5)Convert.ChangeType(args[4], typeof(T5)),
            (T6)Convert.ChangeType(args[5], typeof(T6)),
            (T7)Convert.ChangeType(args[6], typeof(T7)),
            (T8)Convert.ChangeType(args[7], typeof(T8))));
}
