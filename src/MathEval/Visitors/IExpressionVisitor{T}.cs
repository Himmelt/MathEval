using MathEval.AST;

namespace MathEval.Visitors;

/// <summary>
/// 表示访问抽象语法树节点并返回结果的泛型访问者接口
/// </summary>
/// <typeparam name="T">返回类型</typeparam>
public interface IExpressionVisitor<out T>
{
    /// <summary>
    /// 访问值表达式节点
    /// </summary>
    T Visit(ValueExpression expr);

    /// <summary>
    /// 访问标识符节点
    /// </summary>
    T Visit(Identifier expr);

    /// <summary>
    /// 访问二元表达式节点
    /// </summary>
    T Visit(BinaryExpression expr);

    /// <summary>
    /// 访问一元表达式节点
    /// </summary>
    T Visit(UnaryExpression expr);

    /// <summary>
    /// 访问函数调用节点
    /// </summary>
    T Visit(FunctionCall expr);

    /// <summary>
    /// 访问插值字符串节点
    /// </summary>
    T Visit(InterpolatedString expr);

    /// <summary>
    /// 访问条件表达式节点
    /// </summary>
    T Visit(ConditionalExpression expr);
}
