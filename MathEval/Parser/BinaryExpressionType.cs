namespace MathEval.Parser;

/// <summary>
/// 表示二元表达式的类型
/// </summary>
public enum BinaryExpressionType
{
    // 算术运算符
    Plus,
    Minus,
    Multiply,
    Divide,
    IntegerDivide,
    Modulo,
    Power,

    // 位运算符
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,

    // 比较运算符
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    // 逻辑运算符
    And,
    Or
}
