namespace MathEval.Fast.Exceptions;

public class FastEvalException(string msg, string expr = "", int pos = -1) : Exception($"{msg}，[{expr}]@[{pos}]") {
    /// <summary>
    /// 出错的表达式
    /// </summary>
    public string Expression => expr;
    /// <summary>
    /// 错误位置（字符偏移量），-1 表示未知
    /// </summary>
    public int Position => pos;
}