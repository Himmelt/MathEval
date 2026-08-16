using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Parser;
using MathEval.TypeSystem;
using MathEval.Visitors;
using System.Linq.Expressions;
using LinqExpression = System.Linq.Expressions.Expression;

namespace MathEval.Optimization;

/// <summary>
/// 编译优化：将 AST 编译为委托，提升执行效率。
/// 编译树内部值流为 <see cref="MathValue"/>（与解释模式一致，见设计文档 §10.3）。
/// </summary>
public class CompiledExpression(LogicalExpression ast) {
    private readonly Func<ExpressionContext, MathValue> _compiledFunc = CompileToDelegate(ast);

    public object Evaluate(ExpressionContext context) {
        return _compiledFunc(context).ToObject();
    }

    /// <summary>
    /// 调用函数，处理数组广播或聚合展平（匹配 EvaluationVisitor 行为）
    /// isAggregate 由调用方通过 ExpressionContext.IsAggregateFunction 查询 FunctionFlags 得到
    /// </summary>
    internal static MathValue CallFunctionWithBroadcast(ExpressionFunction func, MathValue[] args, bool isAggregate) {
        return FunctionCallEvaluator.Evaluate(func, args, isAggregate);
    }

    /// <summary>
    /// 将 AST 编译为委托
    /// </summary>
    private static Func<ExpressionContext, MathValue> CompileToDelegate(LogicalExpression ast) {
        var contextParam = LinqExpression.Parameter(typeof(ExpressionContext), "context");
        var body = CompileNode(ast, contextParam);
        var lambda = LinqExpression.Lambda<Func<ExpressionContext, MathValue>>(body, contextParam);
        return lambda.Compile();
    }

    /// <summary>
    /// 递归编译 AST 节点
    /// </summary>
    private static LinqExpression CompileNode(LogicalExpression node, ParameterExpression contextParam) {
        return node switch {
            ValueExpression valueExpr => CompileValueExpression(valueExpr),
            Identifier identifier => CompileIdentifier(identifier, contextParam),
            AST.BinaryExpression binaryExpr => CompileBinaryExpression(binaryExpr, contextParam),
            AST.UnaryExpression unaryExpr => CompileUnaryExpression(unaryExpr, contextParam),
            FunctionCall functionCall => CompileFunctionCall(functionCall, contextParam),
            AST.ConditionalExpression condExpr => CompileConditionalExpression(condExpr, contextParam),
            ArrayLiteralExpression arrExpr => CompileArrayLiteral(arrExpr, contextParam),
            ArrayIndexExpression idxExpr => CompileArrayIndex(idxExpr, contextParam),
            InterpolatedString interpolated => CompileInterpolatedString(interpolated, contextParam),
            _ => throw new System.InvalidOperationException($"不支持的节点类型：{node.GetType().Name}")
        };
    }

    /// <summary>
    /// 编译常量值节点：常量在编译期归一化为 MathValue
    /// </summary>
    private static LinqExpression CompileValueExpression(ValueExpression expr) {
        return LinqExpression.Constant(MathValue.FromObject(expr.Value), typeof(MathValue));
    }

    /// <summary>
    /// 编译标识符节点：符号查询后经 FromObject 归一化
    /// </summary>
    private static BlockExpression CompileIdentifier(Identifier expr, ParameterExpression contextParam) {
        var tryGetSymbolMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetSymbol))!;
        var fromObjectMethod = ((Func<object?, MathValue>)MathValue.FromObject).Method;
        var symbolName = LinqExpression.Constant(expr.Name);
        var resultVar = LinqExpression.Variable(typeof(object), "symbolValue");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetSymbolMethod, symbolName, resultVar);

        var throwExpr = LinqExpression.Throw(
            LinqExpression.New(typeof(SymbolNotFoundException).GetConstructor([typeof(string)])!,
            symbolName),
            typeof(MathValue)
        );

        var body = LinqExpression.Block(
            [resultVar],
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwExpr),
            LinqExpression.Call(fromObjectMethod, LinqExpression.Convert(resultVar, typeof(object)))
        );

        return body;
    }

    /// <summary>
    /// 编译二元表达式
    /// </summary>
    private static LinqExpression CompileBinaryExpression(AST.BinaryExpression expr, ParameterExpression contextParam) {
        // And/Or 短路求值：仅在需要时编译右操作数
        if (expr.Type == BinaryExpressionType.And || expr.Type == BinaryExpressionType.Or) {
            return CompileShortCircuitBinary(expr, contextParam);
        }

        var leftExpr = CompileNode(expr.Left, contextParam);
        var rightExpr = CompileNode(expr.Right, contextParam);

        var leftVar = LinqExpression.Variable(typeof(MathValue), "left");
        var rightVar = LinqExpression.Variable(typeof(MathValue), "right");

        var assignLeft = LinqExpression.Assign(leftVar, leftExpr);
        var assignRight = LinqExpression.Assign(rightVar, rightExpr);

        // OPT-5: 用方法组代替反射 GetMethod，编译期类型安全
        var typeHelperMethod = ((Func<BinaryExpressionType, MathValue, MathValue, MathValue>)TypeHelper.EvaluateBinary).Method;
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod, opType, leftVar, rightVar);

        return LinqExpression.Block([leftVar, rightVar], assignLeft, assignRight, call);
    }

    /// <summary>
    /// 编译 And/Or 短路求值，右操作数仅在短路条件不满足时求值
    /// </summary>
    private static LinqExpression CompileShortCircuitBinary(AST.BinaryExpression expr, ParameterExpression contextParam) {
        var isTruthyMethod = ((Func<MathValue, bool>)TypeHelper.IsTruthy).Method;

        var leftVar = LinqExpression.Variable(typeof(MathValue), "left");
        var assignLeft = LinqExpression.Assign(leftVar, CompileNode(expr.Left, contextParam));
        var isLeftTruthy = LinqExpression.Call(isTruthyMethod, leftVar);

        // 右操作数求值（仅在短路条件不满足时执行）
        var rightVar = LinqExpression.Variable(typeof(MathValue), "right");
        var assignRight = LinqExpression.Assign(rightVar, CompileNode(expr.Right, contextParam));
        var rightResult = LinqExpression.Condition(
            LinqExpression.Call(isTruthyMethod, rightVar),
            LinqExpression.Constant(MathValue.Number(1.0), typeof(MathValue)),
            LinqExpression.Constant(MathValue.Number(0.0), typeof(MathValue)),
            typeof(MathValue));
        var evalRight = LinqExpression.Block([rightVar], assignRight, rightResult);

        LinqExpression condition;
        if (expr.Type == BinaryExpressionType.And) {
            // And: left truthy → eval right; left falsy → 0
            condition = LinqExpression.Condition(isLeftTruthy, evalRight,
                LinqExpression.Constant(MathValue.Number(0.0), typeof(MathValue)), typeof(MathValue));
        } else {
            // Or: left truthy → 1; left falsy → eval right
            condition = LinqExpression.Condition(isLeftTruthy,
                LinqExpression.Constant(MathValue.Number(1.0), typeof(MathValue)),
                evalRight, typeof(MathValue));
        }

        return LinqExpression.Block([leftVar], assignLeft, condition);
    }

    /// <summary>
    /// 编译一元表达式
    /// </summary>
    private static LinqExpression CompileUnaryExpression(AST.UnaryExpression expr, ParameterExpression contextParam) {
        var operandExpr = CompileNode(expr.Operand, contextParam);

        var operandVar = LinqExpression.Variable(typeof(MathValue), "operand");
        var assign = LinqExpression.Assign(operandVar, operandExpr);

        var typeHelperMethod = ((Func<UnaryExpressionType, MathValue, MathValue>)TypeHelper.EvaluateUnary).Method;
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod, opType, operandVar);

        return LinqExpression.Block([operandVar], assign, call);
    }

    /// <summary>
    /// 编译函数调用
    /// </summary>
    private static LinqExpression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
        var argsExpr = expr.Arguments.Select(arg => CompileNode(arg, contextParam)).ToArray();
        var argsArrayVar = LinqExpression.Variable(typeof(MathValue[]), "args");
        var initArray = LinqExpression.NewArrayInit(typeof(MathValue), argsExpr);
        var assignArray = LinqExpression.Assign(argsArrayVar, initArray);

        var tryGetFuncMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetFunction))!;
        var isAggregateMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.IsAggregateFunction),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var funcName = LinqExpression.Constant(expr.Name);
        var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod, funcName, funcVar);

        var throwFuncNotFound = LinqExpression.Throw(
            LinqExpression.New(typeof(FunctionNotFoundException).GetConstructor([typeof(string)])!,
            funcName),
            typeof(MathValue)
        );

        // 运行时查询上下文判断是否为聚合函数（依据 FunctionFlags），
        // 再调用 CallFunctionWithBroadcast 处理数组广播/聚合展平，确保解释模式与编译模式一致
        var isAggregateCall = LinqExpression.Call(contextParam, isAggregateMethod, funcName);

        var broadcastMethod = ((Func<ExpressionFunction, MathValue[], bool, MathValue>)CallFunctionWithBroadcast).Method;
        var invokeExpr = LinqExpression.Call(broadcastMethod, funcVar, argsArrayVar, isAggregateCall);

        return LinqExpression.Block(
            [argsArrayVar, funcVar],
            assignArray,
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwFuncNotFound),
            invokeExpr
        );
    }

    /// <summary>
    /// 编译条件表达式
    /// </summary>
    private static LinqExpression CompileConditionalExpression(AST.ConditionalExpression expr, ParameterExpression contextParam) {
        var conditionExpr = CompileNode(expr.Condition, contextParam);

        var conditionVar = LinqExpression.Variable(typeof(MathValue), "condition");
        var assignCondition = LinqExpression.Assign(conditionVar, conditionExpr);

        var isTruthyMethod = ((Func<MathValue, bool>)TypeHelper.IsTruthy).Method;
        var conditionBool = LinqExpression.Call(isTruthyMethod, conditionVar);

        var trueExpr = LinqExpression.Convert(CompileNode(expr.TrueExpression, contextParam), typeof(MathValue));
        var falseExpr = LinqExpression.Convert(CompileNode(expr.FalseExpression, contextParam), typeof(MathValue));

        var conditional = LinqExpression.Condition(conditionBool, trueExpr, falseExpr);

        return LinqExpression.Block([conditionVar], assignCondition, conditional);
    }

    /// <summary>
    /// 编译数组常量表达式：元素求值后经 TypeHelper.BuildArrayLiteral 推断 Kind
    /// （number[]/text[]），与解释模式共享一致性与类型检查语义
    /// </summary>
    private static LinqExpression CompileArrayLiteral(ArrayLiteralExpression expr, ParameterExpression contextParam) {
        var elementExprs = expr.Elements.Select(e => CompileNode(e, contextParam)).ToArray();
        var arrayExpr = LinqExpression.NewArrayInit(typeof(MathValue), elementExprs);
        var buildMethod = ((Func<MathValue[], MathValue>)TypeHelper.BuildArrayLiteral).Method;
        return LinqExpression.Call(buildMethod, arrayExpr);
    }

    /// <summary>
    /// 编译插值字符串：各段转为 string 后经 string.Concat 拼接，
    /// 段格式化调用 TypeHelper.Format/ToDisplayString 与解释模式共享语义
    /// </summary>
    private static LinqExpression CompileInterpolatedString(InterpolatedString expr, ParameterExpression contextParam) {
        var displayMethod = ((Func<MathValue, string>)TypeHelper.ToDisplayString).Method;
        var formatMethod = ((Func<MathValue, string, string>)TypeHelper.Format).Method;
        var textFactory = ((Func<string, MathValue>)MathValue.Text).Method;

        var parts = new List<LinqExpression>();
        foreach (var segment in expr.Segments) {
            if (segment is TextSegment textSeg) {
                parts.Add(LinqExpression.Constant(textSeg.Text, typeof(string)));
            } else if (segment is ExpressionSegment exprSeg) {
                var valueExpr = CompileNode(exprSeg.Expression, contextParam);
                var stringExpr = exprSeg.FormatSpec != null
                    ? LinqExpression.Call(formatMethod, valueExpr, LinqExpression.Constant(exprSeg.FormatSpec))
                    : (LinqExpression)LinqExpression.Call(displayMethod, valueExpr);
                parts.Add(stringExpr);
            }
        }

        var concatMethod = typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])])!;
        return LinqExpression.Call(textFactory,
            LinqExpression.Call(concatMethod, LinqExpression.NewArrayInit(typeof(string), parts)));
    }

    /// <summary>
    /// 编译数组索引表达式：调用 TypeHelper.ArrayIndex 与解释模式共享边界检查语义
    /// </summary>
    private static LinqExpression CompileArrayIndex(ArrayIndexExpression expr, ParameterExpression contextParam) {
        var arrayExpr = CompileNode(expr.Array, contextParam);
        var indexExpr = CompileNode(expr.Index, contextParam);

        var arrayIndexMethod = ((Func<MathValue, MathValue, MathValue>)TypeHelper.ArrayIndex).Method;

        return LinqExpression.Call(arrayIndexMethod, arrayExpr, indexExpr);
    }
}
