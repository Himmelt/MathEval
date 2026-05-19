using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using MathEval.AST;

namespace MathEval.Internal;

internal static class ExpressionCache
{
    private static readonly ConcurrentDictionary<string, LogicalExpression> _cache = new();

    public static bool TryGet(string expression, [MaybeNullWhen(false)] out LogicalExpression ast)
    {
        return _cache.TryGetValue(expression, out ast);
    }

    public static void Set(string expression, LogicalExpression ast)
    {
        _cache.TryAdd(expression, ast);
    }

    public static void Clear()
    {
        _cache.Clear();
    }
}
