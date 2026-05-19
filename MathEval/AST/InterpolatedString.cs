using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示插值字符串表达式
/// </summary>
/// <remarks>
/// 初始化 InterpolatedString 类的新实例
/// </remarks>
/// <param name="segments">段列表</param>
public class InterpolatedString(List<InterpolationSegment> segments) : LogicalExpression {
    /// <summary>
    /// 获取插值字符串段列表
    /// </summary>
    public List<InterpolationSegment> Segments { get; } = segments;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
