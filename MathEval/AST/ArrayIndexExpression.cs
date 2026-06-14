using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示数组索引表达式，如 arr[0]、arr[i]
/// </summary>
/// <remarks>
/// 初始化 ArrayIndexExpression 类的新实例
/// </remarks>
/// <param name="arrayName">数组变量名</param>
/// <param name="index">索引表达式</param>
public class ArrayIndexExpression(string arrayName, LogicalExpression index) : LogicalExpression {
    /// <summary>
    /// 获取数组变量名
    /// </summary>
    public string ArrayName { get; } = arrayName;

    /// <summary>
    /// 获取索引表达式
    /// </summary>
    public LogicalExpression Index { get; } = index;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
