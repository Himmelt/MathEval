using MathEval.Context;

namespace MathEval;

/// <summary>
/// 表达式主入口类，提供静态方法快捷计算表达式
/// </summary>
public static class Expression {
    /// <summary>
    /// 求值表达式
    /// </summary>
    public static object Eval(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None) {
        return Eval<object>(expression, context, options);
    }

    /// <summary>
    /// 求值表达式并返回指定类型
    /// </summary>
    public static T Eval<T>(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None) {
        context ??= new ExpressionContext();
        var calculator = new Calculator(expression, context, options);
        return calculator.Eval<T>();
    }

    /// <summary>
    /// 获取表达式构建器
    /// </summary>
    public static ExpressionBuilder Builder => new();
}