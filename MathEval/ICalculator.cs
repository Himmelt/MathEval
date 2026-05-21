namespace MathEval;

/// <summary>
/// 计算器接口
/// </summary>
public interface ICalculator {
    /// <summary>
    /// 求值表达式，返回 object
    /// </summary>
    object Eval();

    /// <summary>
    /// 求值表达式，返回指定类型
    /// </summary>
    T Eval<T>();

    /// <summary>
    /// 设置符号值 和 延迟值（Func）
    /// </summary>
    void Set(string name, object value);

    /// <summary>
    /// 删除符号或函数
    /// </summary>
    void Remove(string name);
}