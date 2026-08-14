using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;
using MathEval.Parser;
using MathEval.TypeSystem;

namespace MathEval.Visitors;

/// <summary>
/// 解释模式求值访问者：内部值流为 <see cref="MathValue"/>（零装箱内核），
/// Context 符号在 Identifier 处经 <see cref="MathValue.FromObject"/> 归一化进入内核。
/// </summary>
public class EvaluationVisitor(ExpressionContext context) : IExpressionVisitor<MathValue> {
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public MathValue Visit(ValueExpression expr) {
        return MathValue.FromObject(expr.Value);
    }

    public MathValue Visit(Identifier expr) {
        if (_context.TryGetSymbol(expr.Name, out var value)) {
            return MathValue.FromObject(value);
        }
        throw new SymbolNotFoundException(expr.Name);
    }

    public MathValue Visit(BinaryExpression expr) {
        if (expr.Type == BinaryExpressionType.And) {
            var leftResult = expr.Left.Accept(this);
            if (!TypeHelper.IsTruthy(leftResult)) return MathValue.Number(0.0);  // short-circuit (NaN = falsy)
            var rightResult = expr.Right.Accept(this);
            return MathValue.Number(TypeHelper.IsTruthy(rightResult) ? 1.0 : 0.0);
        }

        if (expr.Type == BinaryExpressionType.Or) {
            var leftResult = expr.Left.Accept(this);
            if (TypeHelper.IsTruthy(leftResult)) return MathValue.Number(1.0);  // short-circuit (NaN = falsy)
            var rightResult = expr.Right.Accept(this);
            return MathValue.Number(TypeHelper.IsTruthy(rightResult) ? 1.0 : 0.0);
        }

        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);
        return TypeHelper.EvaluateBinary(expr.Type, left, right);
    }

    public MathValue Visit(UnaryExpression expr) {
        var operand = expr.Operand.Accept(this);
        return TypeHelper.EvaluateUnary(expr.Type, operand);
    }

    public MathValue Visit(FunctionCall expr) {
        var args = new MathValue[expr.Arguments.Count];
        for (int i = 0; i < args.Length; i++)
            args[i] = expr.Arguments[i].Accept(this);

        if (_context.TryGetFunction(expr.Name, out var func)) {
            // 通过 FunctionCallEvaluator 统一处理聚合展平与 element-wise 广播
            // 是否聚合由 ExpressionContext 中注册的 FunctionFlags 决定
            return FunctionCallEvaluator.Evaluate(func, args, _context.IsAggregateFunction(expr.Name));
        }

        throw new FunctionNotFoundException(expr.Name);
    }

    public MathValue Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        if (TypeHelper.IsTruthy(condition)) return expr.TrueExpression.Accept(this);
        else return expr.FalseExpression.Accept(this);
    }

    public MathValue Visit(ArrayLiteralExpression expr) {
        var results = new MathValue[expr.Elements.Count];
        for (int i = 0; i < results.Length; i++) results[i] = expr.Elements[i].Accept(this);
        return TypeHelper.BuildArrayLiteral(results);
    }

    public MathValue Visit(ArrayIndexExpression expr) {
        var array = expr.Array.Accept(this);
        var index = expr.Index.Accept(this);
        return TypeHelper.ArrayIndex(array, index, expr.IsSynthetic);
    }
}
