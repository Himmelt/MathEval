using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示抽象语法树节点的基类
/// <br/>
/// ARCH-9: AST 节点设计为不可变——所有属性均为只读（{ get; }），
/// 构造后不应被修改。子类必须标记为 sealed 以强制不可变性约束。
/// 优化器（如 IndexPushdownOptimizer）可安全地在多个节点间共享同一子表达式实例。
/// </summary>
public abstract class LogicalExpression {
    /// <summary>
    /// 接受访问者访问
    /// </summary>
    /// <param name="visitor">访问者</param>
    public abstract void Accept(IExpressionVisitor visitor);

    /// <summary>
    /// 接受访问者访问并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="visitor">访问者</param>
    /// <returns>访问结果</returns>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}
