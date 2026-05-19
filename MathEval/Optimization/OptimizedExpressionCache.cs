using MathEval.AST;
using MathEval.Internal;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Optimization;

/// <summary>
/// 优化的表达式缓存：缓存解析后的 AST 和编译后的委托
/// </summary>
public static class OptimizedExpressionCache {
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    
    private class CacheEntry {
        public LogicalExpression? Ast { get; set; }
        public CompiledExpression? Compiled { get; set; }
    }
    
    public static bool TryGetAst(string expression, [MaybeNullWhen(false)] out LogicalExpression ast) {
        if (_cache.TryGetValue(expression, out var entry)) {
            ast = entry.Ast;
            return ast != null;
        }
        ast = null;
        return false;
    }
    
    public static bool TryGetCompiled(string expression, [MaybeNullWhen(false)] out CompiledExpression compiled) {
        if (_cache.TryGetValue(expression, out var entry)) {
            compiled = entry.Compiled;
            return compiled != null;
        }
        compiled = null;
        return false;
    }
    
    public static void Set(string expression, LogicalExpression ast) {
        _cache.AddOrUpdate(
            expression,
            new CacheEntry { Ast = ast },
            (_, existing) => {
                existing.Ast = ast;
                existing.Compiled = null; // AST 变更后清除编译缓存
                return existing;
            }
        );
    }
    
    public static void SetCompiled(string expression, CompiledExpression compiled) {
        _cache.AddOrUpdate(
            expression,
            new CacheEntry { Compiled = compiled },
            (_, existing) => {
                existing.Compiled = compiled;
                return existing;
            }
        );
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
        var entry = _cache.GetOrAdd(
            expression,
            expr => {
                var ast = astFactory(expr);
                var compiled = compileFactory(ast);
                return new CacheEntry { Ast = ast, Compiled = compiled };
            }
        );
        
        if (entry.Compiled != null) {
            return entry.Compiled;
        }
        
        var compiledExpr = compileFactory(entry.Ast!);
        entry.Compiled = compiledExpr;
        return compiledExpr;
    }
}
