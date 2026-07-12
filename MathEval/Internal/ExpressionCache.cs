using MathEval.AST;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Internal;

internal static class ExpressionCache {
    // 默认缓存容量：最多缓存 512 条 AST
    private const int DefaultCapacity = 512;
    // 缓存键包含表达式文本与选项，避免不同 ExpressionOptions 共享同一条 AST
    private static readonly LruCache<(string Expression, int Options), LogicalExpression> _cache = new(DefaultCapacity);

    public static bool TryGet(string expression, int options, [MaybeNullWhen(false)] out LogicalExpression ast) {
        return _cache.TryGet((expression, options), out ast);
    }

    public static void Set(string expression, int options, LogicalExpression ast) {
        _cache.Set((expression, options), ast);
    }

    public static void Clear() {
        _cache.Clear();
    }
}
