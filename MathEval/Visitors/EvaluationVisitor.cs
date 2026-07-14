using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;
using MathEval.Parser;
using MathEval.TypeSystem;

namespace MathEval.Visitors;

public class EvaluationVisitor(ExpressionContext context) : IExpressionVisitor<object> {
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public object Visit(ValueExpression expr) {
        return expr.Value;
    }

    public object Visit(Identifier expr) {
        if (_context.TryGetSymbol(expr.Name, out var value)) {
            return value;
        }
        throw new SymbolNotFoundException(expr.Name);
    }

    public object Visit(BinaryExpression expr) {
        if (expr.Type == BinaryExpressionType.And) {
            var leftResult = expr.Left.Accept(this);
            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (!TypeHelper.IsTruthy(leftDouble)) return 0.0;  // short-circuit (NaN = falsy)
            var rightResult = expr.Right.Accept(this);
            return TypeHelper.IsTruthy(TypeHelper.ToDouble(rightResult)) ? 1.0 : 0.0;
        }

        if (expr.Type == BinaryExpressionType.Or) {
            var leftResult = expr.Left.Accept(this);
            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (TypeHelper.IsTruthy(leftDouble)) return 1.0;  // short-circuit (NaN = falsy)
            var rightResult = expr.Right.Accept(this);
            return TypeHelper.IsTruthy(TypeHelper.ToDouble(rightResult)) ? 1.0 : 0.0;
        }

        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);
        return TypeHelper.EvaluateBinary(expr.Type, left, right);
    }

    public object Visit(UnaryExpression expr) {
        var operand = expr.Operand.Accept(this);
        return TypeHelper.EvaluateUnary(expr.Type, operand);
    }

    public object Visit(FunctionCall expr) {
        var args = new object[expr.Arguments.Count];
        for (int i = 0; i < args.Length; i++)
            args[i] = expr.Arguments[i].Accept(this);

        if (_context.TryGetFunction(expr.Name, out var func)) {
            // 通过 FunctionCallEvaluator 统一处理聚合展平与 element-wise 广播
            // 是否聚合由 ExpressionContext 中注册的 FunctionFlags 决定
            return FunctionCallEvaluator.Evaluate(func, args, _context.IsAggregateFunction(expr.Name));
        }

        throw new FunctionNotFoundException(expr.Name);
    }

    public object Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        var condDouble = TypeHelper.ToDouble(condition);
        if (TypeHelper.IsTruthy(condDouble)) return expr.TrueExpression.Accept(this);
        else return expr.FalseExpression.Accept(this);
    }

    public object Visit(ArrayLiteralExpression expr) {
        var results = new double[expr.Elements.Count];
        for (int i = 0; i < results.Length; i++) results[i] = TypeHelper.ToDouble(expr.Elements[i].Accept(this));
        return results;
    }

    public object Visit(ArrayIndexExpression expr) {
        var array = expr.Array.Accept(this);
        var indexValue = expr.Index.Accept(this);
        var intIndex = TypeHelper.ToInteger(indexValue, "数组索引");

        if (array is double[] arr) {
            if (intIndex < 0 || intIndex >= arr.Length) throw new EvaluateException($"索引 {intIndex} 超出数组范围 [0, {arr.Length})");
            return arr[intIndex];
        }

        // 合成索引（IndexPushdownOptimizer 生成）对标量值静默返回标量本身，
        // 用于混合标量/数组运算的优化（如 (arr * scalar)[i] → arr[i] * scalar[i]）
        if (expr.IsSynthetic && array is double) return array;

        // 用户原始编写的标量索引（如 x[0]）抛出类型错误，提供清晰的错误反馈
        throw new TypeMismatchException("索引操作需要数组类型", "array", array?.GetType().Name ?? "null");
    }
}