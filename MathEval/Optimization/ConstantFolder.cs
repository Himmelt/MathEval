using MathEval.AST;
using MathEval.Parser;
using MathEval.TypeSystem;

namespace MathEval.Optimization;

/// <summary>
/// 常量折叠优化：在编译阶段预先计算常量表达式
/// </summary>
public static class ConstantFolder {
    public static LogicalExpression Fold(LogicalExpression expr) {
        return FoldNode(expr);
    }
    
    private static LogicalExpression FoldNode(LogicalExpression node) {
        switch (node) {
            case BinaryExpression binExpr:
                return FoldBinary(binExpr);
            case UnaryExpression unaryExpr:
                return FoldUnary(unaryExpr);
            case FunctionCall funcCall:
                return FoldFunction(funcCall);
            case ConditionalExpression condExpr:
                return FoldConditional(condExpr);
            default:
                return node;
        }
    }
    
    private static LogicalExpression FoldBinary(BinaryExpression expr) {
        var left = FoldNode(expr.Left);
        var right = FoldNode(expr.Right);
        
        // 如果两边都是常量值，直接计算
        if (left is ValueExpression leftVal && right is ValueExpression rightVal) {
            try {
                var result = TypeHelper.EvaluateBinary(expr.Type, leftVal.Value, rightVal.Value);
                return new ValueExpression(result);
            } catch {
                // 计算失败，保持原样
                return new BinaryExpression(expr.Type, left, right);
            }
        }
        
        return new BinaryExpression(expr.Type, left, right);
    }
    
    private static LogicalExpression FoldUnary(UnaryExpression expr) {
        var operand = FoldNode(expr.Operand);
        
        // 如果操作数是常量值，直接计算
        if (operand is ValueExpression valExpr) {
            try {
                var result = TypeHelper.EvaluateUnary(expr.Type, valExpr.Value);
                return new ValueExpression(result);
            } catch {
                // 计算失败，保持原样
                return new UnaryExpression(expr.Type, operand);
            }
        }
        
        return new UnaryExpression(expr.Type, operand);
    }
    
    private static LogicalExpression FoldFunction(FunctionCall expr) {
        var foldedArgs = expr.Arguments.Select(FoldNode).ToList();
        
        // 只有当所有参数都是常量值，并且是内置函数时，才考虑折叠
        var allArgsAreConstants = foldedArgs.All(arg => arg is ValueExpression);
        if (!allArgsAreConstants) {
            return new FunctionCall(expr.Name, foldedArgs);
        }
        
        // 对于某些纯函数，我们可以预先计算，但需要一个无上下文的方式
        // 这里我们暂时不实现复杂的内置函数预计算，保持原样
        return new FunctionCall(expr.Name, foldedArgs);
    }
    
    private static LogicalExpression FoldConditional(ConditionalExpression expr) {
        var condition = FoldNode(expr.Condition);
        var trueExpr = FoldNode(expr.TrueExpression);
        var falseExpr = FoldNode(expr.FalseExpression);
        
        // 如果条件是常量值，直接返回对应的分支
        if (condition is ValueExpression condVal && condVal.Value is bool b) {
            return b ? trueExpr : falseExpr;
        }
        
        return new ConditionalExpression(condition, trueExpr, falseExpr);
    }
}
