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
            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (leftDouble == 0) return 0.0;  // short-circuit
            var rightResult = expr.Right.Accept(this);
            return TypeHelper.ToDouble(rightResult) != 0 ? 1.0 : 0.0;
        }

        if (expr.Type == BinaryExpressionType.Or) {
            var leftResult = expr.Left.Accept(this);
            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (leftDouble != 0) return 1.0;  // short-circuit
            var rightResult = expr.Right.Accept(this);
            return TypeHelper.ToDouble(rightResult) != 0 ? 1.0 : 0.0;
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
            // Check for array arguments - auto broadcast
            var arrayArg = args.FirstOrDefault(a => a is double[]);
            if (arrayArg is double[] arr) {
                var result = new double[arr.Length];
                for (int i = 0; i < arr.Length; i++) {
                    var scalarArgs = new object[args.Length];
                    for (int j = 0; j < args.Length; j++) {
                        scalarArgs[j] = args[j] is double[] da ? da[i] : args[j];
                    }
                    result[i] = TypeHelper.ToDouble(func(scalarArgs));
                }
                return result;
            }

            return func(args);
        }

        throw new FunctionNotFoundException(expr.Name);
    }

    public object Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        var condDouble = TypeHelper.ToDouble(condition);
        if (condDouble != 0)
            return expr.TrueExpression.Accept(this);
        else
            return expr.FalseExpression.Accept(this);
    }

    public object Visit(ArrayLiteralExpression expr) {
        var results = new double[expr.Elements.Count];
        for (int i = 0; i < results.Length; i++)
            results[i] = TypeHelper.ToDouble(expr.Elements[i].Accept(this));
        return results;
    }

    public object Visit(ArrayIndexExpression expr) {
        var array = expr.Array.Accept(this);
        var indexValue = expr.Index.Accept(this);
        var intIndex = TypeHelper.ToInteger(indexValue, "数组索引");

        if (array is double[] arr) {
            if (intIndex < 0 || intIndex >= arr.Length)
                throw new EvaluateException($"索引 {intIndex} 超出数组范围 [0, {arr.Length})");
            return arr[intIndex];
        }

        // Scalar value with index (from index pushdown optimization) - return the scalar itself
        // This handles cases like (arr * x + 10)[i] where x and 10 are scalars
        if (array is double)
            return array;

        throw new TypeMismatchException("索引操作需要数组类型", "array", array?.GetType().Name ?? "null");
    }
}
