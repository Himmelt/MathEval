using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;

namespace MathEval.Functions;

/// <summary>
/// 内置数学函数及常量注册器
/// <br/>
/// 通过 <see cref="Populate"/> 将内置项填入字典，由 <see cref="ExpressionContext"/> 冻结为静态共享表（ARCH-8），
/// 避免每次 <c>new ExpressionContext()</c> 重复注册 ~30 项
/// <br/>
/// ARCH-10: 注册方式设计——
/// 固定参数数量的函数使用 <see cref="FunctionWrapper.Wrap{T1,TResult}"/> 等强类型重载（编译期类型安全）；
/// 可变参数数量的函数（log/round/max/min）使用私有 <c>Func</c> 辅助方法手动注册。
/// 两种方式各司其职，不强制统一。
/// </summary>
internal static class BuiltInFunctions {
    /// <summary>
    /// 将内置常量与函数填入给定字典
    /// </summary>
    /// <param name="symbols">常量表（名称 → 数值）</param>
    /// <param name="functions">函数表（名称 → 函数条目）</param>
    internal static void Populate(IDictionary<string, double> symbols, IDictionary<string, ExpressionContext.FunctionEntry> functions) {
        // 常量
        symbols["E"] = Math.E;
        symbols["π"] = Math.PI;
        symbols["PI"] = Math.PI;

        // 三角函数
        functions["sin"] = new(FunctionWrapper.Wrap("sin", (Func<double, double>)Math.Sin), FunctionFlags.ElementWise);
        functions["cos"] = new(FunctionWrapper.Wrap("cos", (Func<double, double>)Math.Cos), FunctionFlags.ElementWise);
        functions["tan"] = new(FunctionWrapper.Wrap("tan", (Func<double, double>)Math.Tan), FunctionFlags.ElementWise);
        functions["asin"] = new(FunctionWrapper.Wrap("asin", (Func<double, double>)Math.Asin), FunctionFlags.ElementWise);
        functions["acos"] = new(FunctionWrapper.Wrap("acos", (Func<double, double>)Math.Acos), FunctionFlags.ElementWise);
        functions["atan"] = new(FunctionWrapper.Wrap("atan", (Func<double, double>)Math.Atan), FunctionFlags.ElementWise);
        functions["atan2"] = new(FunctionWrapper.Wrap("atan2", (Func<double, double, double>)Math.Atan2), FunctionFlags.ElementWise);

        // 指数幂函数
        functions["exp"] = new(FunctionWrapper.Wrap("exp", (Func<double, double>)Math.Exp), FunctionFlags.ElementWise);
        functions["pow"] = new(FunctionWrapper.Wrap("pow", (Func<double, double, double>)Math.Pow), FunctionFlags.ElementWise);

        // 对数函数
        functions["ln"] = new(FunctionWrapper.Wrap("ln", (Func<double, double>)Math.Log), FunctionFlags.ElementWise);
        functions["lg"] = new(FunctionWrapper.Wrap("lg", (Func<double, double>)Math.Log10), FunctionFlags.ElementWise);
        functions["log"] = new(Func("log", 1, 2, args => args.Length == 1 ? Math.Log(Convert.ToDouble(args[0])) : Math.Log(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]))), FunctionFlags.ElementWise);
        functions["log2"] = new(FunctionWrapper.Wrap("log2", (Func<double, double>)Math.Log2), FunctionFlags.ElementWise);
        functions["log10"] = new(FunctionWrapper.Wrap("log10", (Func<double, double>)Math.Log10), FunctionFlags.ElementWise);

        // 数值处理函数
        functions["abs"] = new(FunctionWrapper.Wrap("abs", (Func<double, double>)Math.Abs), FunctionFlags.ElementWise);
        functions["sqrt"] = new(FunctionWrapper.Wrap("sqrt", (Func<double, double>)Math.Sqrt), FunctionFlags.ElementWise);
        functions["sign"] = new(FunctionWrapper.Wrap("sign", (Func<double, int>)Math.Sign), FunctionFlags.ElementWise);

        // 取整函数
        functions["ceil"] = new(FunctionWrapper.Wrap("ceil", (Func<double, double>)Math.Ceiling), FunctionFlags.ElementWise);
        functions["floor"] = new(FunctionWrapper.Wrap("floor", (Func<double, double>)Math.Floor), FunctionFlags.ElementWise);
        functions["trunc"] = new(FunctionWrapper.Wrap("trunc", (Func<double, double>)Math.Truncate), FunctionFlags.ElementWise);
        functions["round"] = new(Func("round", 1, 2, args => args.Length == 1 ? Math.Round(Convert.ToDouble(args[0])) : Math.Round(Convert.ToDouble(args[0]), Convert.ToInt32(args[1]))), FunctionFlags.ElementWise);

        // 聚合函数
        functions["max"] = new(Func("max", 1, int.MaxValue, args => args.Max(a => Convert.ToDouble(a))), FunctionFlags.Aggregate);
        functions["min"] = new(Func("min", 1, int.MaxValue, args => args.Min(a => Convert.ToDouble(a))), FunctionFlags.Aggregate);
    }

    private static ExpressionFunction Func(string name, int argCount, Func<object?[], object?> fn) => args => args.Length == argCount ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

    private static ExpressionFunction Func(string name, int minArgs, int maxArgs, Func<object?[], object?> fn) => args => args.Length >= minArgs && args.Length <= maxArgs ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "∞" : maxArgs.ToString())} 个参数，但提供了 {args.Length} 个");
}
