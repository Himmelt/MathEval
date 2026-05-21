using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.TypeSystem;
using System.Linq.Expressions;
using LinqExpression = System.Linq.Expressions.Expression;

namespace MathEval.Optimization;

/// <summary>
/// 编译优化：将 AST 编译为委托，提升执行效率
/// </summary>
public class CompiledExpression(LogicalExpression ast) {
    private readonly Func<ExpressionContext, object> _compiledFunc = CompileToDelegate(ast);

    public object Evaluate(ExpressionContext context) {
        return _compiledFunc(context);
    }

    /// <summary>
    /// 将 AST 编译为委托
    /// </summary>
    private static Func<ExpressionContext, object> CompileToDelegate(LogicalExpression ast) {
        var contextParam = LinqExpression.Parameter(typeof(ExpressionContext), "context");
        var body = CompileNode(ast, contextParam);
        var lambda = LinqExpression.Lambda<Func<ExpressionContext, object>>(body, contextParam);
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
            InterpolatedString interpolated => CompileInterpolatedString(interpolated, contextParam),
            _ => throw new System.InvalidOperationException($"不支持的节点类型：{node.GetType().Name}")
        };
    }

    /// <summary>
    /// 编译常量值节点
    /// </summary>
    private static ConstantExpression CompileValueExpression(ValueExpression expr) {
        return LinqExpression.Constant(expr.Value);
    }

    /// <summary>
    /// 编译标识符节点
    /// </summary>
    private static BlockExpression CompileIdentifier(Identifier expr, ParameterExpression contextParam) {
        var tryGetSymbolMethod = typeof(ExpressionContext).GetMethod("TryGetSymbol");
        var symbolName = LinqExpression.Constant(expr.Name);
        var resultVar = LinqExpression.Variable(typeof(object), "symbolValue");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetSymbolMethod!, symbolName, resultVar);

        var throwExpr = LinqExpression.Throw(
            LinqExpression.New(typeof(SymbolNotFoundException).GetConstructor([typeof(string)])!,
            symbolName),
            typeof(object)
        );

        var body = LinqExpression.Block(
            [resultVar],
            LinqExpression.IfThen(
                LinqExpression.Not(tryGetCall),
                throwExpr
            ),
            resultVar
        );

        return body;
    }

    /// <summary>
    /// 编译二元表达式
    /// </summary>
    private static BlockExpression CompileBinaryExpression(AST.BinaryExpression expr, ParameterExpression contextParam) {
        var leftExpr = CompileNode(expr.Left, contextParam);
        var rightExpr = CompileNode(expr.Right, contextParam);

        var leftVar = LinqExpression.Variable(typeof(object), "left");
        var rightVar = LinqExpression.Variable(typeof(object), "right");

        var assignLeft = LinqExpression.Assign(leftVar, leftExpr);
        var assignRight = LinqExpression.Assign(rightVar, rightExpr);

        var typeHelperMethod = typeof(TypeHelper).GetMethod("EvaluateBinary");
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod!, opType, leftVar, rightVar);

        return LinqExpression.Block([leftVar, rightVar], assignLeft, assignRight, call);
    }

    /// <summary>
    /// 编译一元表达式
    /// </summary>
    private static BlockExpression CompileUnaryExpression(AST.UnaryExpression expr, ParameterExpression contextParam) {
        var operandExpr = CompileNode(expr.Operand, contextParam);

        var operandVar = LinqExpression.Variable(typeof(object), "operand");
        var assign = LinqExpression.Assign(operandVar, operandExpr);

        var typeHelperMethod = typeof(TypeHelper).GetMethod("EvaluateUnary");
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod!, opType, operandVar);

        return LinqExpression.Block([operandVar], assign, call);
    }

    /// <summary>
    /// 编译函数调用
    /// </summary>
    private static BlockExpression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
        var argsExpr = expr.Arguments.Select(arg => CompileNode(arg, contextParam)).ToArray();
        var argsArrayVar = LinqExpression.Variable(typeof(object[]), "args");
        var initArray = LinqExpression.NewArrayInit(typeof(object), argsExpr);
        var assignArray = LinqExpression.Assign(argsArrayVar, initArray);

        var tryGetFuncMethod = typeof(ExpressionContext).GetMethod("TryGetFunction");
        var funcName = LinqExpression.Constant(expr.Name);
        var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod!, funcName, funcVar);

        var throwFuncNotFound = LinqExpression.Throw(
            LinqExpression.New(typeof(FunctionNotFoundException).GetConstructor([typeof(string)])!,
            funcName),
            typeof(object)
        );

        var invokeExpr = LinqExpression.Invoke(funcVar, argsArrayVar);

        var body = LinqExpression.Block(
            [argsArrayVar, funcVar],
            assignArray,
            LinqExpression.IfThen(
                LinqExpression.Not(tryGetCall),
                throwFuncNotFound
            ),
            invokeExpr
        );

        return body;
    }

    /// <summary>
    /// 编译条件表达式
    /// </summary>
    private static BlockExpression CompileConditionalExpression(AST.ConditionalExpression expr, ParameterExpression contextParam) {
        var conditionExpr = CompileNode(expr.Condition, contextParam);
        var trueExpr = CompileNode(expr.TrueExpression, contextParam);
        var falseExpr = CompileNode(expr.FalseExpression, contextParam);

        var conditionVar = LinqExpression.Variable(typeof(object), "condition");
        var assignCondition = LinqExpression.Assign(conditionVar, conditionExpr);

        var requireBoolMethod = typeof(TypeHelper).GetMethod("RequireBool");

        var checkBool = LinqExpression.Call(requireBoolMethod, conditionVar);

        var conditionBool = LinqExpression.Convert(conditionVar, typeof(bool));

        var conditional = LinqExpression.Condition(conditionBool, trueExpr, falseExpr, typeof(object));

        return LinqExpression.Block([conditionVar], assignCondition, checkBool, conditional);
    }

    /// <summary>
    /// 编译插值字符串
    /// </summary>
    private static BlockExpression CompileInterpolatedString(InterpolatedString expr, ParameterExpression contextParam) {
        var sbVar = LinqExpression.Variable(typeof(StringBuilder), "sb");
        var newSb = LinqExpression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes)!);
        var assignSb = LinqExpression.Assign(sbVar, newSb);

        var appendExpressions = new List<LinqExpression>();

        foreach (var segment in expr.Segments) {
            if (segment is TextSegment textSeg) {
                var appendText = LinqExpression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", [typeof(string)])!, LinqExpression.Constant(textSeg.Text));
                appendExpressions.Add(appendText);
            } else if (segment is ExpressionSegment exprSeg) {
                var valueExpr = CompileNode(exprSeg.Expression, contextParam);
                var valueVar = LinqExpression.Variable(typeof(object), "value");
                var assignValue = LinqExpression.Assign(valueVar, valueExpr);

                LinqExpression appendExpr;

                if (exprSeg.FormatSpec != null) {
                    var formatMethod = typeof(TypeHelper).GetMethod("Format");
                    var formatCall = LinqExpression.Call(formatMethod!, valueVar, LinqExpression.Constant(exprSeg.FormatSpec));
                    appendExpr = LinqExpression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", [typeof(string)])!, formatCall);
                } else {
                    var toStringMethod = typeof(TypeHelper).GetMethod("ToString");
                    var toStringCall = LinqExpression.Call(toStringMethod!, valueVar);
                    appendExpr = LinqExpression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", [typeof(string)])!, toStringCall);
                }

                var block = LinqExpression.Block([valueVar], assignValue, appendExpr);
                appendExpressions.Add(block);
            }
        }

        var toStringCallFinal = LinqExpression.Call(sbVar, typeof(object).GetMethod("ToString")!);

        var allExpressions = new List<LinqExpression> {
            assignSb
        };
        allExpressions.AddRange(appendExpressions);
        allExpressions.Add(toStringCallFinal);

        return LinqExpression.Block([sbVar], allExpressions);
    }
}