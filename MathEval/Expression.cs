using MathEval.Context;

namespace MathEval;

/// <summary>
/// 表达式主入口类，提供静态方法快捷计算表达式
/// </summary>
public static class Expression {
    // OPT-6: 无参重载复用默认上下文，避免每次调用 new ExpressionContext()。
    // 该上下文仅用于求值（只读），用户无法获取引用去修改它，故共享安全。
    // 内置函数/常量已通过静态 FrozenDictionary 共享（ARCH-8），构造本身已很轻量。
    private static readonly ExpressionContext s_defaultContext = new();

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
        context ??= s_defaultContext;
        var calculator = new Calculator(expression, context, options);
        return calculator.Eval<T>();
    }

    /// <summary>
    /// 优化求值：启用常量折叠和编译优化
    /// </summary>
    public static object OptimizedEval(string expression, ExpressionContext? context = null) {
        return Eval<object>(expression, context, ExpressionOptions.ConstantFolding | ExpressionOptions.CompileOptimization);
    }

    /// <summary>
    /// 优化求值：启用常量折叠和编译优化
    /// </summary>
    public static T OptimizedEval<T>(string expression, ExpressionContext? context = null) {
        return Eval<T>(expression, context, ExpressionOptions.ConstantFolding | ExpressionOptions.CompileOptimization);
    }

    /// <summary>
    /// 获取表达式构建器
    /// </summary>
    public static ExpressionBuilder Builder => new();
}