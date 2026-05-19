using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
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
            TypeHelper.RequireBool(leftResult);
            if (!(bool)leftResult)
                return false;
            var rightResult = expr.Right.Accept(this);
            TypeHelper.RequireBool(rightResult);
            return rightResult;
        }

        if (expr.Type == BinaryExpressionType.Or) {
            var leftResult = expr.Left.Accept(this);
            TypeHelper.RequireBool(leftResult);
            if ((bool)leftResult)
                return true;
            var rightResult = expr.Right.Accept(this);
            TypeHelper.RequireBool(rightResult);
            return rightResult;
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
        var args = new List<object>();
        foreach (var arg in expr.Arguments) {
            args.Add(arg.Accept(this));
        }

        if (_context.TryGetFunction(expr.Name, out var func)) {
            return func(args.ToArray());
        }

        throw new FunctionNotFoundException(expr.Name);
    }

    public object Visit(InterpolatedString expr) {
        var sb = new StringBuilder();
        foreach (var segment in expr.Segments) {
            if (segment is TextSegment textSeg) {
                sb.Append(textSeg.Text);
            } else if (segment is ExpressionSegment exprSeg) {
                var value = exprSeg.Expression.Accept(this);
                if (exprSeg.FormatSpec != null)
                    sb.Append(TypeHelper.Format(value, exprSeg.FormatSpec));
                else
                    sb.Append(TypeHelper.ToString(value));
            }
        }
        return sb.ToString();
    }

    public object Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        TypeHelper.RequireBool(condition);
        if ((bool)condition)
            return expr.TrueExpression.Accept(this);
        else
            return expr.FalseExpression.Accept(this);
    }
}