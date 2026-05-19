using MathEval.AST;

namespace MathEval.Visitors;

/// <summary>
/// 表示访问抽象语法树节点的访问者接口
/// </summary>
public interface IExpressionVisitor
{
    /// <summary>
    /// 访问值表达式节点
    /// </summary>
    void Visit(ValueExpression expr);

    /// <summary>
    /// 访问标识符节点
    /// </summary>
    void Visit(Identifier expr);

    /// <summary>
    /// 访问二元表达式节点
    /// </summary>
    void Visit(BinaryExpression expr);

    /// <summary>
    /// 访问一元表达式节点
    /// </summary>
    void Visit(UnaryExpression expr);

    /// <summary>
    /// 访问函数调用节点
    /// </summary>
    void Visit(FunctionCall expr);

    /// <summary>
    /// 访问插值字符串节点
    /// </summary>
    void Visit(InterpolatedString expr);

    /// <summary>
    /// 访问条件表达式节点
    /// </summary>
    void Visit(ConditionalExpression expr);
}
