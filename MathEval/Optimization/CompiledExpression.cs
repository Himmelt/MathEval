using System.Linq.Expressions;
using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Parser;
using MathEval.TypeSystem;

namespace MathEval.Optimization;

/// <summary>
/// 编译优化：将 AST 编译为委托，提升执行效率
/// </summary>
public class CompiledExpression {
    private readonly Func<ExpressionContext, object> _compiledFunc;
    
    public CompiledExpression(LogicalExpression ast) {
        _compiledFunc = CompileToDelegate(ast);
    }
    
    public object Evaluate(ExpressionContext context) {
        return _compiledFunc(context);
    }
    
    /// <summary>
    /// 将 AST 编译为委托
    /// </summary>
    private static Func<ExpressionContext, object> CompileToDelegate(LogicalExpression ast) {
        var contextParam = Expression.Parameter(typeof(ExpressionContext), "context");
        var body = CompileNode(ast, contextParam);
        var lambda = Expression.Lambda<Func<ExpressionContext, object>>(body, contextParam);
        return lambda.Compile();
    }
    
    /// <summary>
    /// 递归编译 AST 节点
    /// </summary>
    private static System.Linq.Expressions.Expression CompileNode(LogicalExpression node, ParameterExpression contextParam) {
        return node switch {
            ValueExpression valueExpr => CompileValueExpression(valueExpr),
            Identifier identifier => CompileIdentifier(identifier, contextParam),
            BinaryExpression binaryExpr => CompileBinaryExpression(binaryExpr, contextParam),
            UnaryExpression unaryExpr => CompileUnaryExpression(unaryExpr, contextParam),
            FunctionCall functionCall => CompileFunctionCall(functionCall, contextParam),
            ConditionalExpression condExpr => CompileConditionalExpression(condExpr, contextParam),
            InterpolatedString interpolated => CompileInterpolatedString(interpolated, contextParam),
            _ => throw new InvalidOperationException($"不支持的节点类型：{node.GetType().Name}")
        };
    }
    
    /// <summary>
    /// 编译常量值节点
    /// </summary>
    private static System.Linq.Expressions.Expression CompileValueExpression(ValueExpression expr) {
        return Expression.Constant(expr.Value);
    }
    
    /// <summary>
    /// 编译标识符节点
    /// </summary>
    private static System.Linq.Expressions.Expression CompileIdentifier(Identifier expr, ParameterExpression contextParam) {
        var tryGetSymbolMethod = typeof(ExpressionContext).GetMethod("TryGetSymbol");
        var symbolName = Expression.Constant(expr.Name);
        var resultVar = Expression.Variable(typeof(object), "symbolValue");
        
        var tryGetCall = Expression.Call(contextParam, tryGetSymbolMethod!, symbolName, resultVar);
        
        var throwExpr = Expression.Throw(
            Expression.New(typeof(SymbolNotFoundException).GetConstructor(new[] { typeof(string) })!,
            symbolName),
            typeof(object)
        );
        
        var body = Expression.Block(
            new[] { resultVar },
            Expression.IfThen(
                Expression.Not(tryGetCall),
                throwExpr
            ),
            resultVar
        );
        
        return body;
    }
    
    /// <summary>
    /// 编译二元表达式
    /// </summary>
    private static System.Linq.Expressions.Expression CompileBinaryExpression(BinaryExpression expr, ParameterExpression contextParam) {
        var leftExpr = CompileNode(expr.Left, contextParam);
        var rightExpr = CompileNode(expr.Right, contextParam);
        
        var leftVar = Expression.Variable(typeof(object), "left");
        var rightVar = Expression.Variable(typeof(object), "right");
        
        var assignLeft = Expression.Assign(leftVar, leftExpr);
        var assignRight = Expression.Assign(rightVar, rightExpr);
        
        var typeHelperMethod = typeof(TypeHelper).GetMethod("EvaluateBinary");
        var opType = Expression.Constant(expr.Type);
        
        var call = Expression.Call(typeHelperMethod!, opType, leftVar, rightVar);
        
        return Expression.Block(new[] { leftVar, rightVar }, assignLeft, assignRight, call);
    }
    
    /// <summary>
    /// 编译一元表达式
    /// </summary>
    private static System.Linq.Expressions.Expression CompileUnaryExpression(UnaryExpression expr, ParameterExpression contextParam) {
        var operandExpr = CompileNode(expr.Operand, contextParam);
        
        var operandVar = Expression.Variable(typeof(object), "operand");
        var assign = Expression.Assign(operandVar, operandExpr);
        
        var typeHelperMethod = typeof(TypeHelper).GetMethod("EvaluateUnary");
        var opType = Expression.Constant(expr.Type);
        
        var call = Expression.Call(typeHelperMethod!, opType, operandVar);
        
        return Expression.Block(new[] { operandVar }, assign, call);
    }
    
    /// <summary>
    /// 编译函数调用
    /// </summary>
    private static System.Linq.Expressions.Expression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
        var argsExpr = expr.Arguments.Select(arg => CompileNode(arg, contextParam)).ToArray();
        var argsArrayVar = Expression.Variable(typeof(object[]), "args");
        var initArray = Expression.NewArrayInit(typeof(object), argsExpr);
        var assignArray = Expression.Assign(argsArrayVar, initArray);
        
        var tryGetFuncMethod = typeof(ExpressionContext).GetMethod("TryGetFunction");
        var funcName = Expression.Constant(expr.Name);
        var funcVar = Expression.Variable(typeof(ExpressionFunction), "func");
        
        var tryGetCall = Expression.Call(contextParam, tryGetFuncMethod!, funcName, funcVar);
        
        var throwFuncNotFound = Expression.Throw(
            Expression.New(typeof(FunctionNotFoundException).GetConstructor(new[] { typeof(string) })!,
            funcName),
            typeof(object)
        );
        
        var invokeExpr = Expression.Invoke(funcVar, argsArrayVar);
        
        var body = Expression.Block(
            new[] { argsArrayVar, funcVar },
            assignArray,
            Expression.IfThen(
                Expression.Not(tryGetCall),
                throwFuncNotFound
            ),
            invokeExpr
        );
        
        return body;
    }
    
    /// <summary>
    /// 编译条件表达式
    /// </summary>
    private static System.Linq.Expressions.Expression CompileConditionalExpression(ConditionalExpression expr, ParameterExpression contextParam) {
        var conditionExpr = CompileNode(expr.Condition, contextParam);
        var trueExpr = CompileNode(expr.TrueExpression, contextParam);
        var falseExpr = CompileNode(expr.FalseExpression, contextParam);
        
        var conditionVar = Expression.Variable(typeof(object), "condition");
        var assignCondition = Expression.Assign(conditionVar, conditionExpr);
        
        var requireBoolMethod = typeof(TypeHelper).GetMethod("RequireBool");
        
        var checkBool = Expression.Call(requireBoolMethod, conditionVar);
        
        var conditionBool = Expression.Convert(conditionVar, typeof(bool));
        
        var conditional = Expression.Condition(conditionBool, trueExpr, falseExpr, typeof(object));
        
        return Expression.Block(new[] { conditionVar }, assignCondition, checkBool, conditional);
    }
    
    /// <summary>
    /// 编译插值字符串
    /// </summary>
    private static System.Linq.Expressions.Expression CompileInterpolatedString(InterpolatedString expr, ParameterExpression contextParam) {
        var sbVar = Expression.Variable(typeof(StringBuilder), "sb");
        var newSb = Expression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes)!);
        var assignSb = Expression.Assign(sbVar, newSb);
        
        var appendExpressions = new List<System.Linq.Expressions.Expression>();
        
        foreach (var segment in expr.Segments) {
            if (segment is TextSegment textSeg) {
                var appendText = Expression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) })!, Expression.Constant(textSeg.Text));
                appendExpressions.Add(appendText);
            } else if (segment is ExpressionSegment exprSeg) {
                    var valueExpr = CompileNode(exprSeg.Expression, contextParam);
                    var valueVar = Expression.Variable(typeof(object), "value");
                    var assignValue = Expression.Assign(valueVar, valueExpr);
                    
                    System.Linq.Expressions.Expression appendExpr;
                    
                    if (exprSeg.FormatSpec != null) {
                        var formatMethod = typeof(TypeHelper).GetMethod("Format");
                        var formatCall = Expression.Call(formatMethod!, valueVar, Expression.Constant(exprSeg.FormatSpec));
                        appendExpr = Expression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) })!, formatCall);
                    } else {
                        var toStringMethod = typeof(TypeHelper).GetMethod("ToString");
                        var toStringCall = Expression.Call(toStringMethod!, valueVar);
                        appendExpr = Expression.Call(sbVar, typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) })!, toStringCall);
                    }
                    
                    var block = Expression.Block(new[] { valueVar }, assignValue, appendExpr);
                    appendExpressions.Add(block);
                }
            }
        
            var toStringCallFinal = Expression.Call(sbVar, typeof(object).GetMethod("ToString")!);
            
            var allExpressions = new List<System.Linq.Expressions.Expression>();
            allExpressions.Add(assignSb);
            allExpressions.AddRange(appendExpressions);
            allExpressions.Add(toStringCallFinal);
            
            return Expression.Block(new[] { sbVar }, allExpressions);
        }
    }
}
