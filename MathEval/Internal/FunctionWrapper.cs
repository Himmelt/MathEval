using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Internal;

/// <summary>
/// 函数包装器，将强类型 Func 转换为 ExpressionFunction
/// </summary>
internal static class FunctionWrapper {
    public static ExpressionFunction Wrap<T1, TResult>(string name, Func<T1, TResult> func) {
        return args => {
            if (args.Length != 1)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 1 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var result = func(arg1);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, TResult>(string name, Func<T1, T2, TResult> func) {
        return args => {
            if (args.Length != 2)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 2 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var result = func(arg1, arg2);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func) {
        return args => {
            if (args.Length != 3)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 3 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var result = func(arg1, arg2, arg3);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func) {
        return args => {
            if (args.Length != 4)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 4 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var result = func(arg1, arg2, arg3, arg4);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func) {
        return args => {
            if (args.Length != 5)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 5 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var result = func(arg1, arg2, arg3, arg4, arg5);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func) {
        return args => {
            if (args.Length != 6)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 6 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var arg6 = (T6)Convert.ChangeType(args[5], typeof(T6));
                var result = func(arg1, arg2, arg3, arg4, arg5, arg6);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func) {
        return args => {
            if (args.Length != 7)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 7 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var arg6 = (T6)Convert.ChangeType(args[5], typeof(T6));
                var arg7 = (T7)Convert.ChangeType(args[6], typeof(T7));
                var result = func(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func) {
        return args => {
            if (args.Length != 8)
                throw new FunctionTypeMismatchException($"函数 {name} 需要 8 个参数，但提供了 {args.Length} 个");
            try {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var arg6 = (T6)Convert.ChangeType(args[5], typeof(T6));
                var arg7 = (T7)Convert.ChangeType(args[6], typeof(T7));
                var arg8 = (T8)Convert.ChangeType(args[7], typeof(T8));
                var result = func(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 {name} 参数类型不匹配");
            }
        };
    }
}