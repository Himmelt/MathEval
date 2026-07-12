using MathEval.Context;
using MathEval.Exceptions;
using MathEval.TypeSystem;

namespace MathEval.Functions;

/// <summary>
/// 内置数学函数及常量注册器
/// </summary>
internal static class BuiltInFunctions {
    public static void Register(ExpressionContext context) {

        // 常量
        context.Set("E", Math.E);
        context.Set("π", Math.PI);
        context.Set("PI", Math.PI);

        // 三角函数
        context.SetFunction("sin", static (double x) => Math.Sin(x));
        context.SetFunction("cos", static (double x) => Math.Cos(x));
        context.SetFunction("tan", static (double x) => Math.Tan(x));
        context.SetFunction("asin", static (double x) => Math.Asin(x));
        context.SetFunction("acos", static (double x) => Math.Acos(x));
        context.SetFunction("atan", static (double x) => Math.Atan(x));
        context.SetFunction("atan2", static (double y, double x) => Math.Atan2(y, x));

        // 指数幂函数
        context.SetFunction("exp", static (double x) => Math.Exp(x));
        context.SetFunction("pow", static (double x, double y) => Math.Pow(x, y));

        // 对数函数
        context.SetFunction("ln", static (double x) => Math.Log(x));
        context.SetFunction("lg", static (double x) => Math.Log10(x));
        context.SetFunction("log", Func("log", 1, 2, args => args.Length == 1 ? Math.Log(Convert.ToDouble(args[0])) : Math.Log(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]))));
        context.SetFunction("log2", static (double x) => Math.Log2(x));
        context.SetFunction("log10", static (double x) => Math.Log10(x));

        // 数值处理函数
        context.SetFunction("abs", static (double x) => Math.Abs(x));
        context.SetFunction("sqrt", static (double x) => Math.Sqrt(x));
        context.SetFunction("sign", static (double x) => (double)Math.Sign(x));

        // 取整函数
        context.SetFunction("ceil", static (double x) => Math.Ceiling(x));
        context.SetFunction("floor", static (double x) => Math.Floor(x));
        context.SetFunction("trunc", static (double x) => Math.Truncate(x));
        context.SetFunction("round", Func("round", 1, 2, args => {
            if (args.Length == 1) return Math.Round(Convert.ToDouble(args[0]));
            // 用 ToInteger 约束位数范围，避免 Convert.ToInt32 抛出非 MathEval 异常
            var digits = TypeHelper.ToInteger(args[1], "round");
            if (digits < 0 || digits > 15)
                throw new EvaluateException("round 的小数位数必须在 0 到 15 之间");
            return Math.Round(Convert.ToDouble(args[0]), (int)digits);
        }));

        // 聚合函数
        context.SetFunction("max", Func("max", 0, int.MaxValue, args => {
            if (args.Length == 0) throw new EvaluateException("max 的参数不能为空");
            return args.Max(a => Convert.ToDouble(a));
        }));
        context.SetFunction("min", Func("min", 0, int.MaxValue, args => {
            if (args.Length == 0) throw new EvaluateException("min 的参数不能为空");
            return args.Min(a => Convert.ToDouble(a));
        }));
    }

    private static ExpressionFunction Func(string name, int argCount, Func<object?[], object?> fn) => args => args.Length == argCount ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

    private static ExpressionFunction Func(string name, int minArgs, int maxArgs, Func<object?[], object?> fn) => args => args.Length >= minArgs && args.Length <= maxArgs ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "∞" : maxArgs.ToString())} 个参数，但提供了 {args.Length} 个");
}