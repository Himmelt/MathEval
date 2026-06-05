using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Functions;

/// <summary>
/// 内置数学函数注册器
/// </summary>
internal static class BuiltInFunctions {
    public static void Register(ExpressionContext context) {

        context.Set("E", Math.E);
        context.Set("π", Math.PI);
        context.Set("PI", Math.PI);

        context.SetFunction("sin", static (double x) => Math.Sin(x));
        context.SetFunction("cos", static (double x) => Math.Cos(x));
        context.SetFunction("tan", static (double x) => Math.Tan(x));
        context.SetFunction("asin", static (double x) => Math.Asin(x));
        context.SetFunction("acos", static (double x) => Math.Acos(x));
        context.SetFunction("atan", static (double x) => Math.Atan(x));
        context.SetFunction("atan2", static (double y, double x) => Math.Atan2(y, x));

        context.SetFunction("exp", static (double x) => Math.Exp(x));
        context.SetFunction("pow", static (double x, double y) => Math.Pow(x, y));

        context.SetFunction("ln", static (double x) => Math.Log(x));
        context.SetFunction("log", static (double x, double b) => Math.Log(x, b));
        context.SetFunction("log2", static (double x) => Math.Log2(x));
        context.SetFunction("log10", static (double x) => Math.Log10(x));

        context.SetFunction("abs", static (double x) => Math.Abs(x));
        context.SetFunction("sqrt", static (double x) => Math.Sqrt(x));
        context.SetFunction("sign", static (double x) => Math.Sign(x));
        context.SetFunction("ceil", static (double x) => Math.Ceiling(x));
        context.SetFunction("floor", static (double x) => Math.Floor(x));
        context.SetFunction("trunc", static (double x) => Math.Truncate(x));
        context.SetFunction("round", static args => {
            if (args.Length == 1) {
                return Math.Round(Convert.ToDouble(args[0]));
            } else if (args.Length == 2) {
                var value = Convert.ToDouble(args[0]);
                var digits = Convert.ToInt32(args[1]);
                return Math.Round(value, digits);
            }
            throw new FunctionTypeMismatchException("round 需要 1 或 2 个参数");
        });

        context.SetFunction("max", static args => args.Max(a => Convert.ToDouble(a)));
        context.SetFunction("min", static args => args.Min(a => Convert.ToDouble(a)));
    }
}