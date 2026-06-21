using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
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

    /// <summary>
    /// 聚合函数名集合 — 这些函数不应 element-wise 广播，需展平后全局归约
    /// </summary>
    private static readonly HashSet<string> _aggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "max", "min",
    };

    public object Evaluate(ExpressionContext context) {
        return _compiledFunc(context);
    }

    /// <summary>
    /// 调用函数，处理数组广播或聚合展平（匹配 EvaluationVisitor 行为）
    /// </summary>
    internal static object CallFunctionWithBroadcast(ExpressionFunction func, object[] args, string funcName)
    {
        if (_aggregateFunctions.Contains(funcName))
        {
            // 聚合函数：展平数组参数后全局归约
            var flatList = new List<object>();
            foreach (var arg in args)
            {
                if (arg is double[] arr)
                {
                    foreach (var item in arr)
                        flatList.Add(item);
                }
                else
                {
                    flatList.Add(arg);
                }
            }
            return func([.. flatList])!;
        }

        // 非聚合函数：检测数组参数做 element-wise 广播
        var arrayArg = args.FirstOrDefault(a => a is double[]);
        if (arrayArg is double[] arr2)
        {
            var result = new double[arr2.Length];
            for (int i = 0; i < arr2.Length; i++)
            {
                var scalarArgs = new object[args.Length];
                for (int j = 0; j < args.Length; j++)
                {
                    scalarArgs[j] = args[j] is double[] da ? da[i] : args[j];
                }
                result[i] = TypeHelper.ToDouble(func(scalarArgs));
            }
            return result;
        }

        return func(args)!;
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
    private static BlockExpression CompileBinaryExpression(AST.BinaryExpression expr, ParameterExpression contextParam) {
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
        var funcName = LinqExpression.Constant(expr.Name);
        var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");

        var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod!, funcName, funcVar);

        var throwFuncNotFound = LinqExpression.Throw(
            LinqExpression.New(typeof(FunctionNotFoundException).GetConstructor([typeof(string)])!,
            funcName),
            typeof(object)
        );

        // 调用 CallFunctionWithBroadcast 处理数组广播/聚合展平，确保解释模式与编译模式一致
        var broadcastMethod = typeof(CompiledExpression).GetMethod(
            nameof(CallFunctionWithBroadcast),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
            null,
            [typeof(ExpressionFunction), typeof(object[]), typeof(string)],
            null)!;
        var invokeExpr = LinqExpression.Call(broadcastMethod, funcVar, argsArrayVar, funcName);

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
        var toDoubleCall = LinqExpression.Call(toDoubleMethod, conditionVar);
        var conditionBool = LinqExpression.NotEqual(toDoubleCall, LinqExpression.Constant(0.0));

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

        // Get index as int
        var toIntMethod = typeof(TypeHelper).GetMethod("ToInteger", new[] { typeof(object), typeof(string) })!;
        var indexCall = LinqExpression.Call(toIntMethod,
            LinqExpression.Convert(indexExpr, typeof(object)),
            LinqExpression.Constant("数组索引"));
        var intIndex = LinqExpression.Convert(indexCall, typeof(int));

        // Handle both array and scalar (for index pushdown optimization)
        var arrayVar = LinqExpression.Variable(typeof(object), "array");
        var assignArray = LinqExpression.Assign(arrayVar, arrayExpr);

        var isArray = LinqExpression.TypeIs(arrayVar, typeof(double[]));
        var arrayAccess = LinqExpression.ArrayIndex(
            LinqExpression.Convert(arrayVar, typeof(double[])),
            intIndex);
        var arrayResult = LinqExpression.Convert(arrayAccess, typeof(object));

        // If scalar, return the scalar itself
        var scalarResult = arrayVar;

        var condition = LinqExpression.Condition(isArray, arrayResult, scalarResult, typeof(object));

        return LinqExpression.Block([arrayVar], assignArray, condition);
    }
}