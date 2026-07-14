using MathEval.AST;

namespace MathEval.Optimization;

/// <summary>
/// 索引下推优化器：将 (arr*scalar)[i] 等模式优化为 arr[i]*scalar，避免全量计算
/// </summary>
public static class IndexPushdownOptimizer {
    /// <summary>
    /// 对表达式树执行索引下推优化
    /// </summary>
    /// <param name="expr">要优化的表达式</param>
    /// <param name="aggregateFunctions">聚合函数名集合，这些函数不进行索引下推</param>
    public static LogicalExpression Optimize(LogicalExpression expr, HashSet<string>? aggregateFunctions = null) {
        return expr switch {
            // (a op b)[i] → push index into non-literal operands
            // 生成的 ArrayIndexExpression 标记为 IsSynthetic，允许标量静默回退
            ArrayIndexExpression { Array: BinaryExpression bin, Index: var idx }
                => new BinaryExpression(bin.Type,
                    Optimize(bin.Left is ValueExpression ? bin.Left : new ArrayIndexExpression(bin.Left, idx, isSynthetic: true), aggregateFunctions),
                    Optimize(bin.Right is ValueExpression ? bin.Right : new ArrayIndexExpression(bin.Right, idx, isSynthetic: true), aggregateFunctions)),

            // (op a)[i] → op a[i]  (unary operations)
            ArrayIndexExpression { Array: UnaryExpression unary, Index: var idx }
                => new UnaryExpression(unary.Type, Optimize(new ArrayIndexExpression(unary.Operand, idx, isSynthetic: true), aggregateFunctions)),

            // f(a)[i] → f(a[i])  (function calls - push index into non-literal args)
            // 聚合函数跳过索引下推，避免语义错误
            ArrayIndexExpression { Array: FunctionCall func, Index: var idx }
                when aggregateFunctions == null || !aggregateFunctions.Contains(func.Name)
                => Optimize(new FunctionCall(func.Name,
                    [.. func.Arguments.Select(arg => arg is ValueExpression
                        ? arg                              // scalar literal: don't push
                        : new ArrayIndexExpression(arg, idx, isSynthetic: true))]), aggregateFunctions),

            // 聚合函数 f(a)[i] → 保留原样，不下推索引（否则改变语义）
            ArrayIndexExpression { Array: FunctionCall func, Index: var idx, IsSynthetic: var synth } => new ArrayIndexExpression(Optimize(func, aggregateFunctions), Optimize(idx, aggregateFunctions), synth),

            // Recurse into binary expressions
            BinaryExpression bin => new BinaryExpression(bin.Type, Optimize(bin.Left, aggregateFunctions), Optimize(bin.Right, aggregateFunctions)),

            // Recurse into unary expressions
            UnaryExpression unary => new UnaryExpression(unary.Type, Optimize(unary.Operand, aggregateFunctions)),

            // Recurse into function calls
            FunctionCall func => new FunctionCall(func.Name, [.. func.Arguments.Select(a => Optimize(a, aggregateFunctions))]),

            // Recurse into conditional expressions
            ConditionalExpression cond => new ConditionalExpression(
                Optimize(cond.Condition, aggregateFunctions),
                Optimize(cond.TrueExpression, aggregateFunctions),
                Optimize(cond.FalseExpression, aggregateFunctions)),

            // Recurse into array literal elements
            ArrayLiteralExpression arrLit => new ArrayLiteralExpression([.. arrLit.Elements.Select(a => Optimize(a, aggregateFunctions))]),

            // Recurse into array index (non-optimizable pattern)，保留原始 IsSynthetic 标记
            ArrayIndexExpression arrIdx => new ArrayIndexExpression(Optimize(arrIdx.Array, aggregateFunctions), Optimize(arrIdx.Index, aggregateFunctions), arrIdx.IsSynthetic),

            // Leaf nodes - no change
            _ => expr
        };
    }
}
