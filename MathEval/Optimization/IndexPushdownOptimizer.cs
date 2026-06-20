using MathEval.AST;

namespace MathEval.Optimization;

/// <summary>
/// 索引下推优化器：将 (arr*scalar)[i] 等模式优化为 arr[i]*scalar，避免全量计算
/// </summary>
public static class IndexPushdownOptimizer {
    /// <summary>
    /// 对表达式树执行索引下推优化
    /// </summary>
    public static LogicalExpression Optimize(LogicalExpression expr) {
        return expr switch {
            // (a op b)[i] → push index into non-literal operands
            ArrayIndexExpression { Array: BinaryExpression bin, Index: var idx }
                => new BinaryExpression(bin.Type,
                    Optimize(bin.Left is ValueExpression ? bin.Left : new ArrayIndexExpression(bin.Left, idx)),
                    Optimize(bin.Right is ValueExpression ? bin.Right : new ArrayIndexExpression(bin.Right, idx))),

            // (op a)[i] → op a[i]  (unary operations)
            ArrayIndexExpression { Array: UnaryExpression unary, Index: var idx }
                => new UnaryExpression(unary.Type,
                    Optimize(new ArrayIndexExpression(unary.Operand, idx))),

            // f(a)[i] → f(a[i])  (function calls - push index into non-literal args)
            ArrayIndexExpression { Array: FunctionCall func, Index: var idx }
                => Optimize(new FunctionCall(func.Name,
                    func.Arguments.Select(arg => arg is ValueExpression
                        ? arg                              // scalar literal: don't push
                        : new ArrayIndexExpression(arg, idx))  // other: push
                    .ToList())),

            // Recurse into binary expressions
            BinaryExpression bin => new BinaryExpression(bin.Type,
                Optimize(bin.Left), Optimize(bin.Right)),

            // Recurse into unary expressions
            UnaryExpression unary => new UnaryExpression(unary.Type,
                Optimize(unary.Operand)),

            // Recurse into function calls
            FunctionCall func => new FunctionCall(func.Name,
                func.Arguments.Select(Optimize).ToList()),

            // Recurse into conditional expressions
            ConditionalExpression cond => new ConditionalExpression(
                Optimize(cond.Condition),
                Optimize(cond.TrueExpression),
                Optimize(cond.FalseExpression)),

            // Recurse into array literal elements
            ArrayLiteralExpression arrLit => new ArrayLiteralExpression(
                arrLit.Elements.Select(Optimize).ToList()),

            // Recurse into array index (non-optimizable pattern)
            ArrayIndexExpression arrIdx => new ArrayIndexExpression(
                Optimize(arrIdx.Array), Optimize(arrIdx.Index)),

            // Leaf nodes - no change
            _ => expr
        };
    }
}
