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
    private static readonly LruCache<string, CacheEntry> _cache = new(DefaultCapacity);

    private class CacheEntry {
        public LogicalExpression? Ast { get; set; }
        public CompiledExpression? Compiled { get; set; }
    }

    public static bool TryGetAst(string expression, [MaybeNullWhen(false)] out LogicalExpression ast) {
        if (_cache.TryGet(expression, out var entry)) {
            ast = entry.Ast;
            return ast != null;
        }
        ast = null;
        return false;
    }

    public static bool TryGetCompiled(string expression, [MaybeNullWhen(false)] out CompiledExpression compiled) {
        if (_cache.TryGet(expression, out var entry)) {
            compiled = entry.Compiled;
            return compiled != null;
        }
        compiled = null;
        return false;
    }

    public static void Set(string expression, LogicalExpression ast) {
        // 保留已有条目中的 Compiled，仅更新 Ast 字段（与 SetCompiled 对称，避免覆盖丢失）
        if (_cache.TryGet(expression, out var existing) && existing.Compiled != null) {
            _cache.Set(expression, new CacheEntry { Ast = ast, Compiled = existing.Compiled });
        } else {
            _cache.Set(expression, new CacheEntry { Ast = ast });
        }
    }

    public static void SetCompiled(string expression, CompiledExpression compiled) {
        // 保留已有条目中的 AST，仅更新 Compiled 字段（BUG-6：避免覆盖时丢失 AST）
        if (_cache.TryGet(expression, out var existing) && existing.Ast != null) {
            _cache.Set(expression, new CacheEntry { Ast = existing.Ast, Compiled = compiled });
        } else {
            _cache.Set(expression, new CacheEntry { Compiled = compiled });
        }
    }

    public static void Clear() {
        _cache.Clear();
    }

    public static LogicalExpression GetOrAdd(string expression, Func<string, LogicalExpression> factory) {
        return _cache.GetOrAdd(
            expression,
            expr => new CacheEntry { Ast = factory(expr) }
        ).Ast!;
    }

    public static CompiledExpression GetOrAddCompiled(string expression, Func<string, LogicalExpression> astFactory, Func<LogicalExpression, CompiledExpression> compileFactory) {
        // 先检查是否已有编译结果
        if (_cache.TryGet(expression, out var entry) && entry.Compiled != null) {
            return entry.Compiled;
        }

        // 原子地获取或创建完整条目
        entry = _cache.GetOrAdd(expression, expr => {
            var ast = astFactory(expr);
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
