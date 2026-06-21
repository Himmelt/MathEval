using MathEval.AST;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Internal;

internal static class ExpressionCache {
    // 默认缓存容量：最多缓存 512 条 AST
    private const int DefaultCapacity = 512;
    private static readonly LruCache<string, LogicalExpression> _cache = new(DefaultCapacity);

    public static bool TryGet(string expression, [MaybeNullWhen(false)] out LogicalExpression ast) {
        return _cache.TryGet(expression, out ast);
    }

    public static void Set(string expression, LogicalExpression ast) {
        _cache.Set(expression, ast);
    }

    public static void Clear() {
        _cache.Clear();
    }
}
