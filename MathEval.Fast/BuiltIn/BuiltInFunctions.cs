using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using MathEval.Fast.Exceptions;

namespace MathEval.Fast.BuiltIn;

/// <summary>
/// FastEval 内置函数表，硬编码常用数学函数
/// <br/>
/// 统一定义源：函数名、参数数量、求值委托、JIT MethodInfo
/// BytecodeCompiler 和 JitCompiler 均从此处获取函数信息
/// </summary>
internal static class BuiltInFunctions {

    /// <summary>
    /// 函数定义记录，作为所有函数相关信息的唯一数据源
    /// </summary>
    internal readonly record struct FunctionDef(
        string Name,
        int MinArgs,
        int MaxArgs,
        Func<double[], double> Evaluate,
        MethodInfo? JitMethod1 = null,
        MethodInfo? JitMethod2 = null
    );

    #region 唯一数据源

    private static readonly FunctionDef[] _defs = [
        // 三角函数
        new("sin",   1, 1, args => Math.Sin(args[0]),  M1(Math.Sin)),
        new("cos",   1, 1, args => Math.Cos(args[0]),  M1(Math.Cos)),
        new("tan",   1, 1, args => Math.Tan(args[0]),  M1(Math.Tan)),
        new("asin",  1, 1, args => Math.Asin(args[0]), M1(Math.Asin)),
        new("acos",  1, 1, args => Math.Acos(args[0]), M1(Math.Acos)),
        new("atan",  1, 1, args => Math.Atan(args[0]), M1(Math.Atan)),
        new("atan2", 2, 2, args => Math.Atan2(args[0], args[1]), JitMethod2: M2(Math.Atan2)),
        // 指数幂函数
        new("exp",   1, 1, args => Math.Exp(args[0]),  M1(Math.Exp)),
        new("pow",   2, 2, args => Math.Pow(args[0], args[1]), JitMethod2: M2(Math.Pow)),
        // 对数函数
        new("ln",    1, 1, args => Math.Log(args[0]),  M1((Func<double, double>)Math.Log)),
        new("lg",    1, 1, args => Math.Log10(args[0]), M1(Math.Log10)),
        new("log",   1, 2, args => args.Length == 1 ? Math.Log(args[0]) : Math.Log(args[0], args[1]),
            M1((Func<double, double>)Math.Log), M2((Func<double, double, double>)Math.Log)),
        new("log2",  1, 1, args => Math.Log2(args[0]), M1(Math.Log2)),
        new("log10", 1, 1, args => Math.Log10(args[0]), M1(Math.Log10)),
        // 数值处理函数
        new("abs",   1, 1, args => Math.Abs(args[0]),  M1(Math.Abs)),
        new("sqrt",  1, 1, args => Math.Sqrt(args[0]), M1(Math.Sqrt)),
        new("sign",  1, 1, args => Math.Sign(args[0]), M1(SignDouble)),
        // 取整函数
        new("ceil",  1, 1, args => Math.Ceiling(args[0]), M1(Math.Ceiling)),
        new("floor", 1, 1, args => Math.Floor(args[0]),  M1(Math.Floor)),
        new("trunc", 1, 1, args => Math.Truncate(args[0]), M1(Math.Truncate)),
        new("round", 1, 2, args => args.Length == 1 ? Math.Round(args[0]) : Math.Round(args[0], (int)args[1]),
            M1((Func<double, double>)Math.Round), M2(RoundWithDigits)),
        // 聚合函数
        new("max",   1, int.MaxValue, args => args.Max(), JitMethod2: M2(Math.Max)),
        new("min",   1, int.MaxValue, args => args.Min(), JitMethod2: M2(Math.Min)),
    ];

    #endregion

    #region 索引视图

    // 按名称查找（含参数校验）
    private static readonly FrozenDictionary<string, Func<double[], double>> _functions;
    private static readonly FrozenDictionary<string, Func<double[], double>>.AlternateLookup<ReadOnlySpan<char>> _spanLookup;

    // 按名称查 ID
    private static readonly FrozenDictionary<string, byte> _nameToId;
    private static readonly FrozenDictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>> _nameToIdSpan;

    static BuiltInFunctions() {
        // 构建带参数校验的函数字典
        var dict = new Dictionary<string, Func<double[], double>>(_defs.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var def in _defs) {
            dict[def.Name] = CreateValidated(def);
        }
        _functions = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _spanLookup = _functions.GetAlternateLookup<ReadOnlySpan<char>>();

        // 构建名称 → ID 映射
        var idDict = new Dictionary<string, byte>(_defs.Length, StringComparer.OrdinalIgnoreCase);
        for (byte i = 0; i < _defs.Length; i++) {
            idDict[_defs[i].Name] = i;
        }
        _nameToId = idDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _nameToIdSpan = _nameToId.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    #endregion

    #region 公共 API

    /// <summary>
    /// 按名称查找函数（含参数校验），供 FastEvaluator 使用
    /// </summary>
    public static bool TryGetFunction(string name, [NotNullWhen(true)] out Func<double[], double>? func)
        => _functions.TryGetValue(name, out func);

    /// <summary>
    /// Span 版本函数查找，零字符串分配
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetFunction(ReadOnlySpan<char> name, [NotNullWhen(true)] out Func<double[], double>? func)
        => _spanLookup.TryGetValue(name, out func);

    /// <summary>
    /// 按名称查找函数 ID，供 BytecodeCompiler 使用
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetId(ReadOnlySpan<char> name, out byte id)
        => _nameToIdSpan.TryGetValue(name, out id);

    /// <summary>
    /// 按名称查找函数 ID（string 版本）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetId(string name, out byte id)
        => _nameToId.TryGetValue(name, out id);

    /// <summary>
    /// 按 ID 获取函数定义，供 BytecodeVM 和 JitCompiler 使用
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FunctionDef GetById(byte id) => _defs[id];

    /// <summary>
    /// 按 ID 获取求值委托（无参数校验），供 BytecodeVM 使用
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<double[], double> GetEvaluateById(byte id) => _defs[id].Evaluate;

    #endregion

    #region 辅助方法

    /// <summary>
    /// 从单参数方法组提取 MethodInfo（编译期类型安全）
    /// </summary>
    private static MethodInfo M1(Func<double, double> f) => f.Method;

    /// <summary>
    /// 从双参数方法组提取 MethodInfo（编译期类型安全）
    /// </summary>
    private static MethodInfo M2(Func<double, double, double> f) => f.Method;

    /// <summary>
    /// Math.Sign 的 double 返回值包装，供 JIT 直接调用
    /// </summary>
    private static double SignDouble(double v) => Math.Sign(v);

    /// <summary>
    /// Math.Round 的双参数包装，避免 JIT 中需要 Conv_I4
    /// </summary>
    private static double RoundWithDigits(double v, double d) => Math.Round(v, (int)d);

    /// <summary>
    /// 创建带参数数量校验的函数委托
    /// </summary>
    private static Func<double[], double> CreateValidated(FunctionDef def) {
        var maxLabel = def.MaxArgs == int.MaxValue ? "N" : def.MaxArgs.ToString();
        return args => (args.Length >= def.MinArgs && args.Length <= def.MaxArgs)
            ? def.Evaluate(args)
            : throw new FastEvalException($"函数 {def.Name} 需要 {def.MinArgs}-{maxLabel} 个参数，但提供了 {args.Length} 个");
    }

    #endregion
}
