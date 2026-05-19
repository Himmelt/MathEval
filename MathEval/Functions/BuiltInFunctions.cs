using MathEval.Context;
using MathEval.Exceptions;

namespace MathEval.Functions;

/// <summary>
/// 内置数学函数注册器
/// </summary>
internal static class BuiltInFunctions
{
    public static void Register(ExpressionContext context)
    {
        context.Set("PI", 3.14159265358979);
        context.Set("E", 2.71828182845905);

        context.SetFunction("abs", (ExpressionFunction)(args =>
        {
            if (args[0] is long l)
                return Math.Abs(l);
            if (args[0] is double d)
                return Math.Abs(d);
            throw new FunctionTypeMismatchException("abs expects a numeric argument");
        }));

        context.SetFunction("sqrt", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < 0)
                throw new EvaluateException("Square root of negative number is not allowed");
            return Math.Sqrt(value);
        }));

        context.SetFunction("sin", (ExpressionFunction)(args => Math.Sin(Convert.ToDouble(args[0]))));
        context.SetFunction("cos", (ExpressionFunction)(args => Math.Cos(Convert.ToDouble(args[0]))));
        context.SetFunction("tan", (ExpressionFunction)(args => Math.Tan(Convert.ToDouble(args[0]))));

        context.SetFunction("asin", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("asin expects argument in range [-1, 1]");
            return Math.Asin(value);
        }));

        context.SetFunction("acos", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("acos expects argument in range [-1, 1]");
            return Math.Acos(value);
        }));

        context.SetFunction("atan", (ExpressionFunction)(args => Math.Atan(Convert.ToDouble(args[0]))));
        context.SetFunction("atan2", (ExpressionFunction)(args => Math.Atan2(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]))));
        context.SetFunction("exp", (ExpressionFunction)(args => Math.Exp(Convert.ToDouble(args[0]))));

        context.SetFunction("ln", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log(value);
        }));

        context.SetFunction("log", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log(value);
        }));

        context.SetFunction("log10", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log10(value);
        }));

        context.SetFunction("log2", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log2(value);
        }));

        context.SetFunction("ceil", (ExpressionFunction)(args => (long)Math.Ceiling(Convert.ToDouble(args[0]))));
        context.SetFunction("floor", (ExpressionFunction)(args => (long)Math.Floor(Convert.ToDouble(args[0]))));

        context.SetFunction("round", (ExpressionFunction)(args =>
        {
            if (args.Length == 1)
            {
                return (long)Math.Round(Convert.ToDouble(args[0]));
            }
            else if (args.Length == 2)
            {
                var value = Convert.ToDouble(args[0]);
                var digits = Convert.ToInt32(args[1]);
                if (digits < 0)
                    throw new EvaluateException("round decimal digits must be non-negative");
                return Math.Round(value, digits);
            }
            throw new FunctionTypeMismatchException("round expects 1 or 2 arguments");
        }));

        context.SetFunction("truncate", (ExpressionFunction)(args => (long)Math.Truncate(Convert.ToDouble(args[0]))));
        context.SetFunction("sign", (ExpressionFunction)(args => (long)Math.Sign(Convert.ToDouble(args[0]))));

        context.SetFunction("max", (ExpressionFunction)(args =>
        {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Max(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("max does not accept NaN arguments");
            return Math.Max(d1, d2);
        }));

        context.SetFunction("min", (ExpressionFunction)(args =>
        {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Min(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("min does not accept NaN arguments");
            return Math.Min(d1, d2);
        }));

        context.SetFunction("pow", (ExpressionFunction)(args =>
        {
            var x = Convert.ToDouble(args[0]);
            var y = Convert.ToDouble(args[1]);
            if (x < 0 && y != Math.Floor(y))
                throw new EvaluateException("Cannot raise negative number to non-integer power");
            return Math.Pow(x, y);
        }));
    }
}
