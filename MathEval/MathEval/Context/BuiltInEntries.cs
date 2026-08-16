using MathEval.Exceptions;
using MathEval.TypeSystem;

namespace MathEval.Context;

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
/// <br/>
/// 所有内置条目均携带 Kind 签名（参数 Number 约束 + 返回 Number），
/// 供 StrictTypes 静态推断使用：如 <c>sin("abc")</c> 可在求值前报出
/// </summary>
internal static class BuiltInEntries {
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
        functions["sin"] = Num1(FunctionWrapper.Wrap("sin", (Func<double, double>)Math.Sin));
        functions["cos"] = Num1(FunctionWrapper.Wrap("cos", (Func<double, double>)Math.Cos));
        functions["tan"] = Num1(FunctionWrapper.Wrap("tan", (Func<double, double>)Math.Tan));
        functions["asin"] = Num1(FunctionWrapper.Wrap("asin", (Func<double, double>)Math.Asin));
        functions["acos"] = Num1(FunctionWrapper.Wrap("acos", (Func<double, double>)Math.Acos));
        functions["atan"] = Num1(FunctionWrapper.Wrap("atan", (Func<double, double>)Math.Atan));
        functions["atan2"] = Num2(FunctionWrapper.Wrap("atan2", (Func<double, double, double>)Math.Atan2));

        // 指数幂函数
        functions["exp"] = Num1(FunctionWrapper.Wrap("exp", (Func<double, double>)Math.Exp));
        functions["pow"] = Num2(FunctionWrapper.Wrap("pow", (Func<double, double, double>)Math.Pow));

        // 对数函数
        functions["ln"] = Num1(FunctionWrapper.Wrap("ln", (Func<double, double>)Math.Log));
        functions["lg"] = Num1(FunctionWrapper.Wrap("lg", (Func<double, double>)Math.Log10));
        // 审核修复：变参函数经 TypeHelper.ToDouble 归一化，非法类型抛 MathEval 异常
        // （Convert.ToDouble 对字符串抛 FormatException，会泄漏库外异常类型）
        functions["log"] = NumResult(Func("log", 1, 2, args => args.Length == 1
            ? Math.Log(TypeHelper.ToDouble(args[0]!))
            : Math.Log(TypeHelper.ToDouble(args[0]!), TypeHelper.ToDouble(args[1]!))));
        functions["log2"] = Num1(FunctionWrapper.Wrap("log2", (Func<double, double>)Math.Log2));
        functions["log10"] = Num1(FunctionWrapper.Wrap("log10", (Func<double, double>)Math.Log10));

        // 数值处理函数
        functions["abs"] = Num1(FunctionWrapper.Wrap("abs", (Func<double, double>)Math.Abs));
        functions["sqrt"] = Num1(FunctionWrapper.Wrap("sqrt", (Func<double, double>)Math.Sqrt));
        functions["sign"] = Num1(FunctionWrapper.Wrap("sign", (Func<double, int>)Math.Sign));

        // 取整函数
        functions["ceil"] = Num1(FunctionWrapper.Wrap("ceil", (Func<double, double>)Math.Ceiling));
        functions["floor"] = Num1(FunctionWrapper.Wrap("floor", (Func<double, double>)Math.Floor));
        functions["trunc"] = Num1(FunctionWrapper.Wrap("trunc", (Func<double, double>)Math.Truncate));
        functions["round"] = NumResult(Func("round", 1, 2, args => args.Length == 1
            ? Math.Round(TypeHelper.ToDouble(args[0]!))
            : Math.Round(TypeHelper.ToDouble(args[0]!), (int)TypeHelper.ToDouble(args[1]!))));

        // 聚合函数（参数可为标量或数组，展平后归约为 Number）
        // 经 TypeHelper.ToDouble 归一化：非法类型抛 TypeMismatchException 而非泄漏 FormatException
        functions["max"] = new(Func("max", 1, int.MaxValue, args => args.Max(a => TypeHelper.ToDouble(a!))),
            FunctionFlags.Aggregate, ParamKinds: null, ResultKind: MathKind.Number);
        functions["min"] = new(Func("min", 1, int.MaxValue, args => args.Min(a => TypeHelper.ToDouble(a!))),
            FunctionFlags.Aggregate, ParamKinds: null, ResultKind: MathKind.Number);
    }

    /// <summary>单参数 double→double 函数条目：(Number)→Number</summary>
    private static ExpressionContext.FunctionEntry Num1(ExpressionFunction func)
        => new(func, FunctionFlags.ElementWise, [MathKind.Number], MathKind.Number);

    /// <summary>双参数 double 函数条目：(Number, Number)→Number</summary>
    private static ExpressionContext.FunctionEntry Num2(ExpressionFunction func)
        => new(func, FunctionFlags.ElementWise, [MathKind.Number, MathKind.Number], MathKind.Number);

    /// <summary>变参函数条目：参数不约束（个数可变），返回 Number</summary>
    private static ExpressionContext.FunctionEntry NumResult(ExpressionFunction func)
        => new(func, FunctionFlags.ElementWise, ParamKinds: null, ResultKind: MathKind.Number);

    private static ExpressionFunction Func(string name, int argCount, Func<object?[], object?> fn) => args => args.Length == argCount ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

    private static ExpressionFunction Func(string name, int minArgs, int maxArgs, Func<object?[], object?> fn) => args => args.Length >= minArgs && args.Length <= maxArgs ? fn(args)! : throw new FunctionTypeMismatchException($"函数 {name} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "∞" : maxArgs.ToString())} 个参数，但提供了 {args.Length} 个");
}
