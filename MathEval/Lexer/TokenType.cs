namespace MathEval.Lexer;

/// <summary>
/// 表示词法分析器生成的令牌类型
/// </summary>
public enum TokenType {
    // 字面量
    Number,
    NaN,
    INF,

    // 标识符
    Identifier,

    // 算术运算符
    Plus,
    Minus,
    Asterisk,
    Slash,
    DoubleSlash,
    Percent,
    ModKeyword,
    Caret,
    DoubleAsterisk,

    // 位运算符
    Ampersand,
    Pipe,
    XorKeyword,
    Tilde,
    LeftShift,
    RightShift,
    UnsignedRightShift,

    // 逻辑运算符
    AndKeyword,
    OrKeyword,
    NotKeyword,
    DoubleAmpersand,
    DoublePipe,
    Exclamation,

    // 比较运算符
    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,

    // 三元运算符
    QuestionMark,
    Colon,

    // 分隔符
    LeftParenthesis,
    RightParenthesis,
    LeftBracket,
    RightBracket,
    Comma,

    // 结束
    EOF
}
