using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示插值字符串中的表达式段
/// </summary>
public class ExpressionSegment : InterpolationSegment
{
    /// <summary>
    /// 获取表达式
    /// </summary>
    public LogicalExpression Expression { get; }

    /// <summary>
    /// 获取格式说明符（可选）
    /// </summary>
    public string? FormatSpec { get; }

    /// <summary>
    /// 初始化 ExpressionSegment 类的新实例
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="formatSpec">格式说明符</param>
    public ExpressionSegment(LogicalExpression expression, string? formatSpec = null)
    {
        Expression = expression;
        FormatSpec = formatSpec;
    }
}
