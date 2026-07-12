using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Parser;
using MathEval.TypeSystem;

namespace MathEval.Visitors;

public class EvaluationVisitor(ExpressionContext context) : IExpressionVisitor<object> {
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// 聚合函数名集合 — 这些函数不应进行 element-wise 广播，
    /// 应将数组参数展平后再归约（如 max([1,2],[3,4]) → 4）
    /// </summary>
    private static readonly HashSet<string> _aggregateFunctions = ["max", "min"];

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
            // 数组操作数无法进行标量短路，转交 EvaluateBinary 做 element-wise 计算
            if (leftResult is double[]) {
                var rhs = expr.Right.Accept(this);
                return TypeHelper.EvaluateBinary(BinaryExpressionType.And, leftResult, rhs);
            }

            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (leftDouble == 0) return 0.0;  // short-circuit
            var rightResult = expr.Right.Accept(this);
            if (rightResult is double[]) return TypeHelper.EvaluateBinary(BinaryExpressionType.And, leftDouble, rightResult);
            return TypeHelper.ToDouble(rightResult) != 0 ? 1.0 : 0.0;
        }

        if (expr.Type == BinaryExpressionType.Or) {
            var leftResult = expr.Left.Accept(this);
            // 数组操作数无法进行标量短路，转交 EvaluateBinary 做 element-wise 计算
            if (leftResult is double[]) {
                var rhs = expr.Right.Accept(this);
                return TypeHelper.EvaluateBinary(BinaryExpressionType.Or, leftResult, rhs);
            }

            var leftDouble = TypeHelper.ToDouble(leftResult);
            if (leftDouble != 0) return 1.0;  // short-circuit
            var rightResult = expr.Right.Accept(this);
            if (rightResult is double[]) return TypeHelper.EvaluateBinary(BinaryExpressionType.Or, leftDouble, rightResult);
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
            // 聚合函数：不广播，展平数组参数后直接调函数（全局归约语义）
            if (_aggregateFunctions.Contains(expr.Name)) {
                return func(FlattenArgs(args));
            }

            // Check for array arguments - auto broadcast
            var arrayArg = args.FirstOrDefault(a => a is double[]);
            if (arrayArg is double[] arr) {
                // Validate all array arguments have the same length
                foreach (var arg in args) {
                    if (arg is double[] da && da.Length != arr.Length) {
                        throw new EvaluateException(
                            $"数组广播时所有数组参数长度必须一致，但遇到长度 {da.Length} 和 {arr.Length}");
                    }
                }

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

    /// <summary>
    /// 将参数展平：数组参数拆分为单个元素，标量参数保持不变
    /// </summary>
    private static object[] FlattenArgs(object[] args) {
        var list = new List<object>();
        foreach (var arg in args) {
            if (arg is double[] arr) {
                foreach (var item in arr) list.Add(item);
            } else {
                list.Add(arg);
            }
        }
        return [.. list];
    }

    public object Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        var condDouble = TypeHelper.ToDouble(condition);
        if (condDouble != 0) return expr.TrueExpression.Accept(this);
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

        // Scalar value with index. This legitimately occurs for variable scalar indexing
        // (e.g. x[0] where x is a scalar) and index-pushdown optimization (e.g. (arr * x + 10)[i]
        // becomes arr[i] * x[i] with x a scalar). However, a directly written literal scalar index
        // like 5[0] or 5[999] is a user error and must not be silently ignored.
        if (array is double) {
            if (expr.Array is ValueExpression)
                throw new EvaluateException($"无法对标量 {array} 进行索引");
            return array;
        }

        throw new TypeMismatchException("索引操作需要数组类型", "array", array?.GetType().Name ?? "null");
    }
}