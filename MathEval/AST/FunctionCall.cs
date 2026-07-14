using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示函数调用表达式
/// </summary>
/// <remarks>
/// 初始化 FunctionCall 类的新实例
/// </remarks>
/// <param name="name">函数名称</param>
/// <param name="arguments">参数列表</param>
public sealed class FunctionCall(string name, List<LogicalExpression> arguments) : LogicalExpression {
    /// <summary>
    /// 获取函数名称
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 获取函数参数列表
    /// </summary>
    public IReadOnlyList<LogicalExpression> Arguments { get; } = arguments;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
