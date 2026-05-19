using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示插值字符串表达式
/// </summary>
public class InterpolatedString : LogicalExpression
{
    /// <summary>
    /// 获取插值字符串段列表
    /// </summary>
    public List<InterpolationSegment> Segments { get; }

    /// <summary>
    /// 初始化 InterpolatedString 类的新实例
    /// </summary>
    /// <param name="segments">段列表</param>
    public InterpolatedString(List<InterpolationSegment> segments)
    {
        Segments = segments;
    }

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}
