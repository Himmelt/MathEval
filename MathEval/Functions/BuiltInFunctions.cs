using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Functions;

/// <summary>
/// 内置数学函数注册器
/// </summary>
internal static class BuiltInFunctions {
    public static void Register(ExpressionContext context) {
        context.Set("PI", Math.PI);
        context.Set("E", Math.E);
        context.Set("π", Math.PI);

        context.SetFunction("abs", args => {
            if (args[0] is long l) return Math.Abs(l);
            if (args[0] is double d) return Math.Abs(d);
            throw new FunctionTypeMismatchException("abs 需要数值参数");
        });

        context.SetFunction("sqrt", args => {
            var value = Convert.ToDouble(args[0]);
            if (value < 0)
                throw new EvaluateException("不允许对负数求平方根");
            return Math.Sqrt(value);
        });

        context.SetFunction("sin", args => Math.Sin(Convert.ToDouble(args[0])));
        context.SetFunction("cos", args => Math.Cos(Convert.ToDouble(args[0])));
        context.SetFunction("tan", args => Math.Tan(Convert.ToDouble(args[0])));

        context.SetFunction("asin", args => {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("asin 的参数范围应为 [-1, 1]");
            return Math.Asin(value);
        });

        context.SetFunction("acos", args => {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("acos 的参数范围应为 [-1, 1]");
            return Math.Acos(value);
        });

        context.SetFunction("atan", args => Math.Atan(Convert.ToDouble(args[0])));
        context.SetFunction("atan2", args => Math.Atan2(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])));
        context.SetFunction("exp", args => Math.Exp(Convert.ToDouble(args[0])));

        context.SetFunction("ln", args => {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("不允许对非正数求对数");
            return Math.Log(value);
        });

        context.SetFunction("log", args => {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("不允许对非正数求对数");
            return Math.Log(value);
        });

        context.SetFunction("log10", args => {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("不允许对非正数求对数");
            return Math.Log10(value);
        });

        context.SetFunction("log2", args => {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("不允许对非正数求对数");
            return Math.Log2(value);
        });

        context.SetFunction("ceil", args => (long)Math.Ceiling(Convert.ToDouble(args[0])));
        context.SetFunction("floor", args => (long)Math.Floor(Convert.ToDouble(args[0])));

        context.SetFunction("round", args => {
            if (args.Length == 1) {
                return (long)Math.Round(Convert.ToDouble(args[0]));
            } else if (args.Length == 2) {
                var value = Convert.ToDouble(args[0]);
                var digits = Convert.ToInt32(args[1]);
                if (digits < 0)
                    throw new EvaluateException("round 的小数位数必须为非负数");
                return Math.Round(value, digits);
            }
            throw new FunctionTypeMismatchException("round 需要 1 或 2 个参数");
        });

        context.SetFunction("truncate", args => (long)Math.Truncate(Convert.ToDouble(args[0])));
        context.SetFunction("sign", args => (long)Math.Sign(Convert.ToDouble(args[0])));

        context.SetFunction("max", args => {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Max(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("max does not accept NaN arguments");
            return Math.Max(d1, d2);
        });

        context.SetFunction("min", args => {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Min(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("min 不接受 NaN 参数");
            return Math.Min(d1, d2);
        });

        context.SetFunction("pow", args => {
            var x = Convert.ToDouble(args[0]);
            var y = Convert.ToDouble(args[1]);
            if (x < 0 && y != Math.Floor(y))
                throw new EvaluateException("不能对负数求非整数次幂");
            return Math.Pow(x, y);
        });
    }
}