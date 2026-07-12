using MathEval.AST;
using MathEval.Internal;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Optimization;

/// <summary>
/// 优化的表达式缓存：缓存解析后的 AST 和编译后的委托
/// </summary>
public static class OptimizedExpressionCache {
    // 默认缓存容量：最多缓存 512 条 AST+编译结果
    private const int DefaultCapacity = 512;
    // 缓存键包含表达式文本与选项，避免不同 ExpressionOptions 共享同一条 AST/编译结果
    private static readonly LruCache<(string Expression, int Options), CacheEntry> _cache = new(DefaultCapacity);

    private class CacheEntry {
        public LogicalExpression? Ast { get; set; }
        public CompiledExpression? Compiled { get; set; }
    }

    public static bool TryGetAst(string expression, int options, [MaybeNullWhen(false)] out LogicalExpression ast) {
        if (_cache.TryGet((expression, options), out var entry)) {
            ast = entry.Ast;
            return ast != null;
        }
        ast = null;
        return false;
    }

    public static bool TryGetCompiled(string expression, int options, [MaybeNullWhen(false)] out CompiledExpression compiled) {
        if (_cache.TryGet((expression, options), out var entry)) {
            compiled = entry.Compiled;
            return compiled != null;
        }
        compiled = null;
        return false;
    }

    public static void Set(string expression, int options, LogicalExpression ast) {
        _cache.Set((expression, options), new CacheEntry { Ast = ast, Compiled = null });
    }

    public static void SetCompiled(string expression, int options, CompiledExpression compiled) {
        _cache.Set((expression, options), new CacheEntry { Compiled = compiled });
    }

    public static void Clear() {
        _cache.Clear();
    }

    public static LogicalExpression GetOrAdd(string expression, int options, Func<string, LogicalExpression> factory) {
        return _cache.GetOrAdd(
            (expression, options),
            expr => new CacheEntry { Ast = factory(expr.Expression) }
        ).Ast!;
    }

    public static CompiledExpression GetOrAddCompiled(string expression, int options, Func<string, LogicalExpression> astFactory, Func<LogicalExpression, CompiledExpression> compileFactory) {
        // 先检查是否已有编译结果
        if (_cache.TryGet((expression, options), out var entry) && entry.Compiled != null) {
            return entry.Compiled;
        }

        // 原子地获取或创建完整条目
        entry = _cache.GetOrAdd((expression, options), expr => {
            var ast = astFactory(expr.Expression);
            return new CacheEntry { Ast = ast, Compiled = compileFactory(ast) };
        });

        // 处理边界情况：条目来自 Set() 但 Compiled 为 null，需要编译
        if (entry.Compiled == null) {
            lock (entry) {
                if (entry.Compiled == null) {
                    entry.Compiled = compileFactory(entry.Ast!);
                }
            }
        }

        return entry.Compiled!;
    }
}
