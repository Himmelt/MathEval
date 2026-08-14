namespace MathEval.AST;

/// <summary>
/// 表示插值字符串中的表达式段
/// </summary>
/// <remarks>
/// 初始化 ExpressionSegment 类的新实例
/// </remarks>
/// <param name="expression">表达式</param>
/// <param name="formatSpec">格式说明符</param>
public class ExpressionSegment(LogicalExpression expression, string? formatSpec = null) : InterpolationSegment {
    /// <summary>
    /// 获取表达式
    /// </summary>
    public LogicalExpression Expression { get; } = expression;

    /// <summary>
    /// 获取格式说明符（可选）
    /// </summary>
    public string? FormatSpec { get; } = formatSpec;
}
