using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Internal;

/// <summary>
/// 函数包装器，将强类型 Func 转换为 ExpressionFunction
/// </summary>
internal static class FunctionWrapper
{
    public static ExpressionFunction Wrap<T1, TResult>(Func<T1, TResult> func)
    {
        return args =>
        {
            if (args.Length != 1)
                throw new FunctionTypeMismatchException($"Function expects 1 argument, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var result = func(arg1);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, TResult>(Func<T1, T2, TResult> func)
    {
        return args =>
        {
            if (args.Length != 2)
                throw new FunctionTypeMismatchException($"Function expects 2 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var result = func(arg1, arg2);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func)
    {
        return args =>
        {
            if (args.Length != 3)
                throw new FunctionTypeMismatchException($"Function expects 3 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var result = func(arg1, arg2, arg3);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> func)
    {
        return args =>
        {
            if (args.Length != 4)
                throw new FunctionTypeMismatchException($"Function expects 4 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var result = func(arg1, arg2, arg3, arg4);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, TResult>(Func<T1, T2, T3, T4, T5, TResult> func)
    {
        return args =>
        {
            if (args.Length != 5)
                throw new FunctionTypeMismatchException($"Function expects 5 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var result = func(arg1, arg2, arg3, arg4, arg5);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, TResult>(Func<T1, T2, T3, T4, T5, T6, TResult> func)
    {
        return args =>
        {
            if (args.Length != 6)
                throw new FunctionTypeMismatchException($"Function expects 6 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var arg6 = (T6)Convert.ChangeType(args[5], typeof(T6));
                var result = func(arg1, arg2, arg3, arg4, arg5, arg6);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        return args =>
        {
            if (args.Length != 7)
                throw new FunctionTypeMismatchException($"Function expects 7 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var arg3 = (T3)Convert.ChangeType(args[2], typeof(T3));
                var arg4 = (T4)Convert.ChangeType(args[3], typeof(T4));
                var arg5 = (T5)Convert.ChangeType(args[4], typeof(T5));
                var arg6 = (T6)Convert.ChangeType(args[5], typeof(T6));
                var arg7 = (T7)Convert.ChangeType(args[6], typeof(T7));
                var result = func(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        return args =>
        {
            if (args.Length != 8)
                throw new FunctionTypeMismatchException($"Function expects 8 arguments, got {args.Length}");
            try
            {
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
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException("Argument type mismatch for function");
            }
        };
    }
}
