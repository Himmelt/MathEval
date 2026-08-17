namespace MathEval.Exceptions;

/// <summary>
/// MathEval 表达式的机器可读错误码，供调用方在不依赖异常消息文本的前提下程序化区分错误。
/// 编码分段：1xxx 语法/词法，2xxx 名字解析，3xxx 类型，4xxx 运行期。
/// </summary>
public enum MathEvalErrorCode {
    /// <summary>未知/未分类</summary>
    None = 0,

    // ---- 语法 / 词法（SyntaxException） ----
    /// <summary>表达式为空</summary>
    EmptyExpression = 1000,
    /// <summary>表达式长度超过限制</summary>
    ExpressionTooLong = 1001,
    /// <summary>意外字符</summary>
    UnexpectedCharacter = 1002,
    /// <summary>意外标记</summary>
    UnexpectedToken = 1003,
    /// <summary>期望某标记但得到其他标记</summary>
    ExpectedToken = 1004,
    /// <summary>无效的数字字面量</summary>
    InvalidNumberLiteral = 1005,
    /// <summary>无效的转义序列</summary>
    InvalidEscapeSequence = 1006,
    /// <summary>未终止的字符串字面量</summary>
    UnterminatedString = 1007,
    /// <summary>字符串意外结束</summary>
    UnexpectedEndOfString = 1008,
    /// <summary>插值字符串中的花括号不匹配</summary>
    UnmatchedInterpolationDelimiter = 1009,
    /// <summary>表达式嵌套过深</summary>
    NestingTooDeep = 1010,
    /// <summary>无效的格式说明符</summary>
    InvalidFormatSpecifier = 1011,

    // ---- 名字解析（ResolutionException） ----
    /// <summary>符号未定义</summary>
    SymbolNotFound = 2000,
    /// <summary>函数未定义</summary>
    FunctionNotFound = 2001,

    // ---- 类型（TypeMismatchException） ----
    /// <summary>类型不匹配</summary>
    TypeMismatch = 3000,
    /// <summary>函数参数类型不匹配</summary>
    FunctionArgumentType = 3001,

    // ---- 运行期（EvaluationException） ----
    /// <summary>函数参数个数不匹配</summary>
    FunctionArity = 4000,
    /// <summary>调用用户函数时其内部抛出异常</summary>
    FunctionInvocation = 4001,
    /// <summary>数组索引越界</summary>
    IndexOutOfRange = 4002,
    /// <summary>数组长度不匹配</summary>
    ArrayLengthMismatch = 4003,
    /// <summary>移位量为负数</summary>
    InvalidShiftAmount = 4004,
    /// <summary>格式说明符用于不支持的数值类型</summary>
    FormatSpecifierError = 4005,
    /// <summary>函数广播时数组参数长度不一致</summary>
    BroadcastLengthMismatch = 4006,
}
