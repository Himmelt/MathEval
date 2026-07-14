using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;
using MathEval.Parser;
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
    /// 调用函数，处理数组广播或聚合展平（匹配 EvaluationVisitor 行为）
    /// isAggregate 由调用方通过 ExpressionContext.IsAggregateFunction 查询 FunctionFlags 得到
    /// </summary>
    internal static object CallFunctionWithBroadcast(ExpressionFunction func, object[] args, bool isAggregate)
    {
        return FunctionCallEvaluator.Evaluate(func, args, isAggregate);
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
            ArrayLiteralExpression arrExpr => CompileArrayLiteral(arrExpr, contextParam),
            ArrayIndexExpression idxExpr => CompileArrayIndex(idxExpr, contextParam),
            _ => throw new System.InvalidOperationException($"不支持的节点类型：{node.GetType().Name}")
        };
    }

    /// <summary>
    /// 编译常量值节点
    /// </summary>
    private static LinqExpression CompileValueExpression(ValueExpression expr) {
        var constant = LinqExpression.Constant(expr.Value);
        // 值类型需要显式 Convert(object) 才能在表达式树中作为 object 使用
        if (expr.Value != null && expr.Value.GetType().IsValueType) return LinqExpression.Convert(constant, typeof(object));
        return constant;
    }

    /// <summary>
    /// 编译标识符节点
    /// </summary>
    private static BlockExpression CompileIdentifier(Identifier expr, ParameterExpression contextParam) {
        var tryGetSymbolMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetSymbol));
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
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwExpr),
            resultVar
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

        var leftVar = LinqExpression.Variable(typeof(object), "left");
        var rightVar = LinqExpression.Variable(typeof(object), "right");

        var assignLeft = LinqExpression.Assign(leftVar, leftExpr);
        var assignRight = LinqExpression.Assign(rightVar, rightExpr);

        var typeHelperMethod = ((Func<BinaryExpressionType, object, object, object>)TypeHelper.EvaluateBinary).Method;
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod!, opType, leftVar, rightVar);

        return LinqExpression.Block([leftVar, rightVar], assignLeft, assignRight, call);
    }

    /// <summary>
    /// 编译 And/Or 短路求值，右操作数仅在短路条件不满足时求值
    /// </summary>
    private static LinqExpression CompileShortCircuitBinary(AST.BinaryExpression expr, ParameterExpression contextParam) {
        var toDoubleMethod = ((Func<object, double>)TypeHelper.ToDouble).Method;
        var isTruthyMethod = ((Func<double, bool>)TypeHelper.IsTruthy).Method;

        var leftVar = LinqExpression.Variable(typeof(object), "left");
        var leftDoubleVar = LinqExpression.Variable(typeof(double), "leftDouble");
        var assignLeft = LinqExpression.Assign(leftVar, CompileNode(expr.Left, contextParam));
        var assignLeftDouble = LinqExpression.Assign(leftDoubleVar, LinqExpression.Call(toDoubleMethod, leftVar));
        var isLeftTruthy = LinqExpression.Call(isTruthyMethod, leftDoubleVar);

        // 右操作数求值（仅在短路条件不满足时执行）
        var rightVar = LinqExpression.Variable(typeof(object), "right");
        var rightDoubleVar = LinqExpression.Variable(typeof(double), "rightDouble");
        var assignRight = LinqExpression.Assign(rightVar, CompileNode(expr.Right, contextParam));
        var assignRightDouble = LinqExpression.Assign(rightDoubleVar, LinqExpression.Call(toDoubleMethod, rightVar));
        var isRightTruthy = LinqExpression.Call(isTruthyMethod, rightDoubleVar);
        var rightResult = LinqExpression.Condition(isRightTruthy,
            LinqExpression.Convert(LinqExpression.Constant(1.0), typeof(object)),
            LinqExpression.Convert(LinqExpression.Constant(0.0), typeof(object)),
            typeof(object));
        var evalRight = LinqExpression.Block([rightVar, rightDoubleVar], assignRight, assignRightDouble, rightResult);

        LinqExpression condition;
        if (expr.Type == BinaryExpressionType.And) {
            // And: left truthy → eval right; left falsy → 0.0
            condition = LinqExpression.Condition(isLeftTruthy, evalRight,
                LinqExpression.Convert(LinqExpression.Constant(0.0), typeof(object)), typeof(object));
        } else {
            // Or: left truthy → 1.0; left falsy → eval right
            condition = LinqExpression.Condition(isLeftTruthy,
                LinqExpression.Convert(LinqExpression.Constant(1.0), typeof(object)),
                evalRight, typeof(object));
        }

        return LinqExpression.Block([leftVar, leftDoubleVar], assignLeft, assignLeftDouble, condition);
    }

    /// <summary>
    /// 编译一元表达式
    /// </summary>
    private static BlockExpression CompileUnaryExpression(AST.UnaryExpression expr, ParameterExpression contextParam) {
        var operandExpr = CompileNode(expr.Operand, contextParam);

        var operandVar = LinqExpression.Variable(typeof(object), "operand");
        var assign = LinqExpression.Assign(operandVar, operandExpr);

        var typeHelperMethod = ((Func<UnaryExpressionType, object, object>)TypeHelper.EvaluateUnary).Method;
        var opType = LinqExpression.Constant(expr.Type);

        var call = LinqExpression.Call(typeHelperMethod!, opType, operandVar);

        return LinqExpression.Block([operandVar], assign, call);
    }

    /// <summary>
    /// 编译函数调用
    /// </summary>
    private static BlockExpression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
        var argsExpr = expr.Arguments.Select(arg => LinqExpression.Convert(CompileNode(arg, contextParam), typeof(object))).ToArray();
        var argsArrayVar = LinqExpression.Variable(typeof(object[]), "args");
        var initArray = LinqExpression.NewArrayInit(typeof(object), argsExpr);
        var assignArray = LinqExpression.Assign(argsArrayVar, initArray);

        var tryGetFuncMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetFunction));
        var isAggregateMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.IsAggregateFunction),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var funcName = LinqExpression.Constant(expr.Name);
        var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod!, funcName, funcVar);

        var throwFuncNotFound = LinqExpression.Throw(
            LinqExpression.New(typeof(FunctionNotFoundException).GetConstructor([typeof(string)])!,
            funcName),
            typeof(object)
        );

        // 运行时查询上下文判断是否为聚合函数（依据 FunctionFlags），
        // 再调用 CallFunctionWithBroadcast 处理数组广播/聚合展平，确保解释模式与编译模式一致
        var isAggregateCall = LinqExpression.Call(contextParam, isAggregateMethod!, funcName);

        // OPT-5: 用方法组代替反射 GetMethod + BindingFlags，编译期类型安全
        var broadcastMethod = ((Func<ExpressionFunction, object[], bool, object>)CallFunctionWithBroadcast).Method;
        var invokeExpr = LinqExpression.Call(broadcastMethod, funcVar, argsArrayVar, isAggregateCall);

        var body = LinqExpression.Block(
            [argsArrayVar, funcVar],
            assignArray,
            LinqExpression.IfThen(LinqExpression.Not(tryGetCall), throwFuncNotFound),
            invokeExpr
        );

        return body;
    }

    /// <summary>
    /// 编译条件表达式
    /// </summary>
    private static LinqExpression CompileConditionalExpression(AST.ConditionalExpression expr, ParameterExpression contextParam) {
        var conditionExpr = CompileNode(expr.Condition, contextParam);

        var conditionVar = LinqExpression.Variable(typeof(object), "condition");
        var assignCondition = LinqExpression.Assign(conditionVar, conditionExpr);

        var toDoubleMethod = ((Func<object, double>)TypeHelper.ToDouble).Method;
        var isTruthyMethod = ((Func<double, bool>)TypeHelper.IsTruthy).Method;
        var toDoubleCall = LinqExpression.Call(toDoubleMethod, conditionVar);
        var conditionBool = LinqExpression.Call(isTruthyMethod, toDoubleCall);

        var trueExprObj = LinqExpression.Convert(CompileNode(expr.TrueExpression, contextParam), typeof(object));
        var falseExprObj = LinqExpression.Convert(CompileNode(expr.FalseExpression, contextParam), typeof(object));

        var conditional = LinqExpression.Condition(conditionBool, trueExprObj, falseExprObj);

        return LinqExpression.Block([conditionVar], assignCondition, conditional);
    }

    /// <summary>
    /// 编译数组常量表达式
    /// </summary>
    private static LinqExpression CompileArrayLiteral(ArrayLiteralExpression expr, ParameterExpression contextParam) {
        var elementExprs = expr.Elements.Select(e => CompileNode(e, contextParam)).ToArray();
        // Convert each element to double using TypeHelper.ToDouble
        var toDoubleMethod = ((Func<object, double>)TypeHelper.ToDouble).Method;
        var conversions = elementExprs.Select(e =>
            LinqExpression.Call(toDoubleMethod, e)).ToArray();
        return LinqExpression.NewArrayInit(typeof(double), conversions);
    }

    /// <summary>
    /// 编译数组索引表达式
    /// </summary>
    private static LinqExpression CompileArrayIndex(ArrayIndexExpression expr, ParameterExpression contextParam) {
        var arrayExpr = CompileNode(expr.Array, contextParam);
        var indexExpr = CompileNode(expr.Index, contextParam);

        // Get index as int (OPT-5: 方法组代替反射 GetMethod)
        var toIntMethod = ((Func<object, string, long>)TypeHelper.ToInteger).Method;
        var indexCall = LinqExpression.Call(toIntMethod,
            LinqExpression.Convert(indexExpr, typeof(object)),
            LinqExpression.Constant("数组索引"));
        var intIndex = LinqExpression.Convert(indexCall, typeof(int));

        // Handle both array and scalar (for index pushdown optimization)
        var arrayVar = LinqExpression.Variable(typeof(object), "array");
        var indexVar = LinqExpression.Variable(typeof(int), "idx");
        var assignArray = LinqExpression.Assign(arrayVar, arrayExpr);
        var assignIndex = LinqExpression.Assign(indexVar, intIndex);

        var isArray = LinqExpression.TypeIs(arrayVar, typeof(double[]));

        // Bounds check: throw friendly EvaluateException instead of IndexOutOfRangeException
        var arrayLen = LinqExpression.ArrayLength(LinqExpression.Convert(arrayVar, typeof(double[])));
        var indexOutOfRange = LinqExpression.OrElse(
            LinqExpression.LessThan(indexVar, LinqExpression.Constant(0)),
            LinqExpression.GreaterThanOrEqual(indexVar, arrayLen));
        var evalExCtor = typeof(EvaluateException).GetConstructor(new[] { typeof(string) })!;
        var formatMethod = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object), typeof(object) })!;
        var errorMsg = LinqExpression.Call(formatMethod,
            LinqExpression.Constant("索引 {0} 超出数组范围 [0, {1})"),
            LinqExpression.Convert(indexVar, typeof(object)),
            LinqExpression.Convert(arrayLen, typeof(object)));
        var throwOutOfRange = LinqExpression.Throw(
            LinqExpression.New(evalExCtor, errorMsg),
            typeof(object));

        // Safe array access: check bounds first, then access
        var safeArrayAccess = LinqExpression.Condition(
            indexOutOfRange,
            throwOutOfRange,
            LinqExpression.Convert(
                LinqExpression.ArrayIndex(
                    LinqExpression.Convert(arrayVar, typeof(double[])),
                    indexVar),
                typeof(object)),
            typeof(object));

        // 标量回退行为：合成索引（IndexPushdownOptimizer 生成）静默返回标量本身；
        // 用户原始索引对标量抛 TypeMismatchException，提供清晰错误反馈
        LinqExpression scalarFallback;
        if (expr.IsSynthetic) {
            scalarFallback = arrayVar;
        } else {
            var typeMismatchCtor = typeof(TypeMismatchException).GetConstructor([typeof(string), typeof(string), typeof(string)])!;
            scalarFallback = LinqExpression.Throw(
                LinqExpression.New(typeMismatchCtor,
                    LinqExpression.Constant("索引操作需要数组类型"),
                    LinqExpression.Constant("array"),
                    LinqExpression.Constant("scalar")),
                typeof(object));
        }

        var condition = LinqExpression.Condition(isArray, safeArrayAccess, scalarFallback, typeof(object));

        return LinqExpression.Block([arrayVar, indexVar], assignArray, assignIndex, condition);
    }
}