using MathEval.AST;
using MathEval.Context;
using MathEval.Internal;
using MathEval.TypeSystem;

namespace MathEval.Optimization;

/// <summary>
/// StrictTypes 推断缓存：按 (表达式, ContextId, 是否折叠) 缓存 Kind 推断结果与纯 Number 特化委托，
/// 以 <see cref="ExpressionContext.SymbolVersion"/> 判定失效——上下文任何符号/函数变更后重推断。
/// 折叠位参与键：折叠与未折叠的 AST 形态不同（折叠可预算纯函数、改写常量），
/// 混用会互相污染推断结论与特化委托。
/// 特化委托在重推断后 Kind 仍为 Number 时直接复用（纯 Number 特化对任意 Number 值成立，
/// 值更新无需重编译；只有符号 Kind 变化才会使推断结论改变）。
/// 推断抛出的异常不缓存：每次求值重新检查（StrictTypes 的求值前快照语义）。
/// </summary>
internal static class StrictTypeCache {
    private const int DefaultCapacity = 512;

    private sealed record Entry(long Version, MathKind? Kind, bool PureNumber,
        Func<ExpressionContext, double>? Specialized);

    private static readonly LruCache<(string Expression, long ContextId, bool Folded), Entry> _cache = new(DefaultCapacity);

    /// <summary>
    /// 推断（或复用）表达式的根 Kind 与纯 Number 判定。版本不匹配或未缓存时重新推断；
    /// 无效类型组合在此抛出（含死分支），实现 StrictTypes 的求值前检查
    /// </summary>
    public static (MathKind? Kind, bool PureNumber) InferKind(string expression, ExpressionContext context, LogicalExpression ast, bool folded) {
        var key = (expression, context.ContextId, folded);
        var version = context.SymbolVersion;

        if (_cache.TryGet(key, out var entry) && entry.Version == version)
            return (entry.Kind, entry.PureNumber);

        var (kind, pureNumber) = KindInferencePass.Infer(ast, context);

        // 重推断后复用旧特化委托（仅当结论仍为纯 Number）
        var specialized = pureNumber ? entry?.Specialized : null;
        _cache.Set(key, new Entry(version, kind, pureNumber, specialized));
        return (kind, pureNumber);
    }

    /// <summary>
    /// 获取或编译纯 Number 特化委托：整棵树纯 Number 时生效；
    /// 树含不可特化节点时返回 null（调用方走通用路径）
    /// </summary>
    public static Func<ExpressionContext, double>? GetOrCompileSpecialized(
        string expression, ExpressionContext context, LogicalExpression ast, bool pureNumber, bool folded) {
        if (!pureNumber) return null;

        var key = (expression, context.ContextId, folded);
        var version = context.SymbolVersion;

        if (_cache.TryGet(key, out var entry) && entry.Specialized != null) return entry.Specialized;

        Func<ExpressionContext, double>? specialized;
        try {
            specialized = NumberSpecializedCompiler.Compile(ast);
        } catch (NotSupportedException) {
            // 推断结论为 Number 但树含不可特化节点（防御）：记录并回退通用路径
            specialized = null;
        }

        _cache.Set(key, new Entry(version, MathKind.Number, pureNumber, specialized));
        return specialized;
    }

    public static void Clear() => _cache.Clear();
}
