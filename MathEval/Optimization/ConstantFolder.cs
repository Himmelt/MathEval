using MathEval.AST;
using MathEval.TypeSystem;

namespace MathEval.Optimization;

/// <summary>
/// 常量折叠优化：在编译阶段预先计算常量表达式
/// </summary>
public static class ConstantFolder {

    // OPT-1: 内置纯数学函数表，当所有参数均为常量时可预计算
    // 注意：若用户覆盖了同名内置函数，折叠结果可能不一致。
    // ConstantFolding 为 opt-in 选项，用户启用时接受此行为。
    private static readonly Dictionary<string, Func<double[], double>> s_pureFunctions = new(StringComparer.Ordinal) {
        ["sin"] = a => Math.Sin(a[0]),
        ["cos"] = a => Math.Cos(a[0]),
        ["tan"] = a => Math.Tan(a[0]),
        ["asin"] = a => Math.Asin(a[0]),
        ["acos"] = a => Math.Acos(a[0]),
        ["atan"] = a => Math.Atan(a[0]),
        ["atan2"] = a => Math.Atan2(a[0], a[1]),
        ["exp"] = a => Math.Exp(a[0]),
        ["pow"] = a => Math.Pow(a[0], a[1]),
        ["ln"] = a => Math.Log(a[0]),
        ["lg"] = a => Math.Log10(a[0]),
        ["log"] = a => a.Length == 1 ? Math.Log(a[0]) : Math.Log(a[0], a[1]),
        ["log2"] = a => Math.Log2(a[0]),
        ["log10"] = a => Math.Log10(a[0]),
        ["abs"] = a => Math.Abs(a[0]),
        ["sqrt"] = a => Math.Sqrt(a[0]),
        ["sign"] = a => Math.Sign(a[0]),
        ["ceil"] = a => Math.Ceiling(a[0]),
        ["floor"] = a => Math.Floor(a[0]),
        ["trunc"] = a => Math.Truncate(a[0]),
        ["round"] = a => a.Length == 1 ? Math.Round(a[0]) : Math.Round(a[0], (int)a[1]),
        ["max"] = a => a.Max(),
        ["min"] = a => a.Min(),
    };

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
            case ArrayLiteralExpression arrExpr:
                return FoldArrayLiteral(arrExpr);
            case ArrayIndexExpression idxExpr:
                return FoldArrayIndex(idxExpr);
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
        // 折叠参数中的常量子表达式（如 sin(1+2) → sin(3)）
        var foldedArgs = expr.Arguments.Select(FoldNode).ToList();

        // OPT-1: 若所有参数均为常量 double 且函数为已知纯函数，直接预计算
        if (s_pureFunctions.TryGetValue(expr.Name, out var func)) {
            var args = new double[foldedArgs.Count];
            bool allConst = true;
            for (int i = 0; i < foldedArgs.Count; i++) {
                if (foldedArgs[i] is ValueExpression val && val.Value is double d) {
                    args[i] = d;
                } else {
                    allConst = false;
                    break;
                }
            }
            if (allConst) {
                try {
                    return new ValueExpression(func(args));
                } catch {
                    // 计算失败（如定义域错误），保持原样
                }
            }
        }

        return new FunctionCall(expr.Name, foldedArgs);
    }

    private static LogicalExpression FoldConditional(ConditionalExpression expr) {
        var condition = FoldNode(expr.Condition);
        var trueExpr = FoldNode(expr.TrueExpression);
        var falseExpr = FoldNode(expr.FalseExpression);

        // 如果条件是常量值，直接返回对应的分支
        if (condition is ValueExpression condVal && condVal.Value is double d) {
            return TypeHelper.IsTruthy(d) ? trueExpr : falseExpr;
        }

        return new ConditionalExpression(condition, trueExpr, falseExpr);
    }

    private static LogicalExpression FoldArrayLiteral(ArrayLiteralExpression expr) {
        var folded = expr.Elements.Select(FoldNode).ToList();
        return new ArrayLiteralExpression(folded);
    }

    private static LogicalExpression FoldArrayIndex(ArrayIndexExpression expr) {
        var foldedArray = FoldNode(expr.Array);
        var foldedIndex = FoldNode(expr.Index);

        // If array is a constant literal and index is a constant, we can pre-compute
        if (foldedArray is ArrayLiteralExpression lit && foldedIndex is ValueExpression val) {
            var idx = TypeHelper.ToInteger(val.Value, "数组索引");
            if (idx >= 0 && idx < lit.Elements.Count)
                return lit.Elements[(int)idx];
        }

        return new ArrayIndexExpression(foldedArray, foldedIndex);
    }
}
