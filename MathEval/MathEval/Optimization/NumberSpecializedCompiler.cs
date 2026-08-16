using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Parser;
using MathEval.TypeSystem;
using System.Linq.Expressions;
using LinqExpression = System.Linq.Expressions.Expression;

namespace MathEval.Optimization;

/// <summary>
/// 纯 Number 特化编译器：当 KindInferencePass 证明整棵树 Kind 均为 Number（无 Unknown）时，
/// 将 AST 编译为 <see cref="Func{TContext, Double}"/>——内部值流为裸 double，
/// 无 MathValue 构造/传递与 Kind 分支（见设计文档 §9 性能特化）。
/// 运算语义通过共享 TypeHelper.EvaluateNumberOp/EvaluateNumberUnary 保证与通用路径一致。
/// 纯 Number 树中不存在数组/插值节点；若遇到（推断遗漏等异常情况）抛 NotSupportedException，
/// 由调用方回退通用编译路径。
/// </summary>
internal static class NumberSpecializedCompiler {
    /// <summary>符号值 → double（与 MathValue.FromObject(...).AsNumber 语义一致，函数/符号边界用）</summary>
    internal static double ToNumberOrThrow(object? value)
        => MathValue.FromObject(value).AsNumber;

    public static Func<ExpressionContext, double> Compile(LogicalExpression ast) {
        var contextParam = LinqExpression.Parameter(typeof(ExpressionContext), "context");
        var body = CompileNode(ast, contextParam);
        return LinqExpression.Lambda<Func<ExpressionContext, double>>(body, contextParam).Compile();
    }

    private static LinqExpression CompileNode(LogicalExpression node, ParameterExpression contextParam) {
        return node switch {
            ValueExpression valueExpr => CompileValue(valueExpr),
            Identifier identifier => CompileIdentifier(identifier, contextParam),
            AST.BinaryExpression binaryExpr => CompileBinary(binaryExpr, contextParam),
            AST.UnaryExpression unaryExpr => CompileUnary(unaryExpr, contextParam),
            AST.ConditionalExpression condExpr => CompileConditional(condExpr, contextParam),
            FunctionCall functionCall => CompileFunctionCall(functionCall, contextParam),
            // 纯 Number 树不含这些节点；出现说明推断结论与树不符，交由调用方回退通用路径
            _ => throw new NotSupportedException($"节点 {node.GetType().Name} 不参与纯 Number 特化"),
        };
    }

    private static LinqExpression CompileValue(ValueExpression expr) {
        // 纯 Number 判定下常量必为数值类型；异常情况交由调用方回退通用路径
        if (expr.Value is double d) return LinqExpression.Constant(d);
        if (expr.Value is not string) return LinqExpression.Constant(System.Convert.ToDouble(expr.Value));
        throw new NotSupportedException($"常量 {expr.Value} 不是数值类型");
    }

    private static LinqExpression CompileIdentifier(Identifier expr, ParameterExpression contextParam) {
        var tryGetSymbolMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetSymbol))!;
        var toNumberMethod = ((Func<object?, double>)ToNumberOrThrow).Method;
        var symbolName = LinqExpression.Constant(expr.Name);
        var resultVar = LinqExpression.Variable(typeof(object), "symbolValue");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetSymbolMethod, symbolName, resultVar);
        var throwExpr = LinqExpression.Throw(
            LinqExpression.New(typeof(SymbolNotFoundException).GetConstructor([typeof(string)])!, symbolName),
            typeof(double));

        return LinqExpression.Block(
            [resultVar],
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwExpr),
            LinqExpression.Call(toNumberMethod, LinqExpression.Convert(resultVar, typeof(object))));
    }

    private static LinqExpression CompileBinary(AST.BinaryExpression expr, ParameterExpression contextParam) {
        // And/Or 短路：IsTruthy(double) 判定，语义与 EvaluationVisitor 一致
        if (expr.Type is BinaryExpressionType.And or BinaryExpressionType.Or)
            return CompileShortCircuitBinary(expr, contextParam);

        var leftVar = LinqExpression.Variable(typeof(double), "left");
        var rightVar = LinqExpression.Variable(typeof(double), "right");

        // 共享 TypeHelper.EvaluateNumberOp：运算语义（含位运算整数检查、Modulo 符号等）与通用路径完全一致
        var numberOpMethod = ((Func<BinaryExpressionType, double, double, double>)TypeHelper.EvaluateNumberOp).Method;
        var call = LinqExpression.Call(numberOpMethod, LinqExpression.Constant(expr.Type), leftVar, rightVar);

        return LinqExpression.Block([leftVar, rightVar],
            LinqExpression.Assign(leftVar, CompileNode(expr.Left, contextParam)),
            LinqExpression.Assign(rightVar, CompileNode(expr.Right, contextParam)),
            call);
    }

    private static LinqExpression CompileShortCircuitBinary(AST.BinaryExpression expr, ParameterExpression contextParam) {
        var isTruthyMethod = ((Func<double, bool>)TypeHelper.IsTruthy).Method;
        var leftVar = LinqExpression.Variable(typeof(double), "left");
        var assignLeft = LinqExpression.Assign(leftVar, CompileNode(expr.Left, contextParam));
        var isLeftTruthy = LinqExpression.Call(isTruthyMethod, leftVar);

        var rightVar = LinqExpression.Variable(typeof(double), "right");
        var assignRight = LinqExpression.Assign(rightVar, CompileNode(expr.Right, contextParam));
        var evalRight = LinqExpression.Condition(
            LinqExpression.Call(isTruthyMethod, rightVar),
            LinqExpression.Constant(1.0),
            LinqExpression.Constant(0.0));

        // And: left truthy → eval right；left falsy → 0。Or 对称。
        return expr.Type == BinaryExpressionType.And
            ? LinqExpression.Block([leftVar], assignLeft,
                LinqExpression.Condition(isLeftTruthy, LinqExpression.Block([rightVar], assignRight, evalRight),
                    LinqExpression.Constant(0.0)))
            : LinqExpression.Block([leftVar], assignLeft,
                LinqExpression.Condition(isLeftTruthy, LinqExpression.Constant(1.0),
                    LinqExpression.Block([rightVar], assignRight, evalRight)));
    }

    private static LinqExpression CompileUnary(AST.UnaryExpression expr, ParameterExpression contextParam) {
        var numberUnaryMethod = ((Func<UnaryExpressionType, double, double>)TypeHelper.EvaluateNumberUnary).Method;
        return LinqExpression.Call(numberUnaryMethod, LinqExpression.Constant(expr.Type),
            CompileNode(expr.Operand, contextParam));
    }

    private static LinqExpression CompileConditional(AST.ConditionalExpression expr, ParameterExpression contextParam) {
        var isTruthyMethod = ((Func<double, bool>)TypeHelper.IsTruthy).Method;
        var conditionVar = LinqExpression.Variable(typeof(double), "condition");
        return LinqExpression.Block([conditionVar],
            LinqExpression.Assign(conditionVar, CompileNode(expr.Condition, contextParam)),
            LinqExpression.Condition(LinqExpression.Call(isTruthyMethod, conditionVar),
                CompileNode(expr.TrueExpression, contextParam),
                CompileNode(expr.FalseExpression, contextParam)));
    }

    private static LinqExpression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
        // 纯 Number 树中参数全为 Number 标量：聚合展平退化为原样、无广播，
        // 统一直接调用（与 FunctionCallEvaluator 标量路径一致），结果经 FromObject→AsNumber 归一化
        var args = expr.Arguments
            .Select(arg => (LinqExpression)LinqExpression.Convert(CompileNode(arg, contextParam), typeof(object)))
            .ToArray();
        var argsVar = LinqExpression.Variable(typeof(object[]), "args");
        var initArgs = LinqExpression.Assign(argsVar, LinqExpression.NewArrayInit(typeof(object), args));

        var tryGetFuncMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetFunction))!;
        var funcName = LinqExpression.Constant(expr.Name);
        var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");
        var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod, funcName, funcVar);
        var throwFuncNotFound = LinqExpression.Throw(
            LinqExpression.New(typeof(FunctionNotFoundException).GetConstructor([typeof(string)])!, funcName),
            typeof(double));

        var invokeResultVar = LinqExpression.Variable(typeof(object), "result");
        var invoke = LinqExpression.Assign(invokeResultVar, LinqExpression.Invoke(funcVar, argsVar));

        var toNumberMethod = ((Func<object?, double>)ToNumberOrThrow).Method;
        var toNumber = LinqExpression.Call(toNumberMethod, LinqExpression.Convert(invokeResultVar, typeof(object)));

        return LinqExpression.Block([argsVar, funcVar, invokeResultVar],
            initArgs,
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwFuncNotFound),
            invoke,
            toNumber);
    }
}
