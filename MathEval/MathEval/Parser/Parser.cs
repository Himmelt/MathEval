using MathEval.AST;
using MathEval.Exceptions;
using System.Globalization;

namespace MathEval.Parser;

public class Parser {
    private readonly Lexer.Lexer _lexer;
    private Lexer.Token _currentToken;
    private int _depth;
    /// <summary>
    /// 默认最大嵌套深度
    /// </summary>
    public const int DefaultMaxDepth = 1024;
    private readonly int _maxDepth;

    public Parser(Lexer.Lexer lexer, int maxDepth = DefaultMaxDepth) {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth), "最大嵌套深度必须为正数");
        _maxDepth = maxDepth;
        _lexer.MoveNext();
        _currentToken = _lexer.CurrentToken;
    }

    private Lexer.Token CurrentToken => _currentToken;

    private void MoveNext() {
        _lexer.MoveNext();
        _currentToken = _lexer.CurrentToken;
    }

    private void Expect(Lexer.TokenType type) {
        if (CurrentToken.Type != type)
            throw new SyntaxException(MathEvalErrorCode.ExpectedToken, $"期望 '{type}'，但得到 '{CurrentToken.Type}'", CurrentToken.Position);
        MoveNext();
    }

    public LogicalExpression Parse() {
        _depth = 0;

        if (CurrentToken.Type == Lexer.TokenType.EOF)
            throw new SyntaxException(MathEvalErrorCode.EmptyExpression, "表达式不能为空", 0);

        var expr = ParseExpression();
        if (CurrentToken.Type != Lexer.TokenType.EOF)
            throw new SyntaxException(MathEvalErrorCode.UnexpectedToken, $"意外的标记 '{CurrentToken.Text}'", CurrentToken.Position);
        return expr;
    }

    private LogicalExpression ParseExpression() {
        return ParseConditional();
    }

    private LogicalExpression ParseConditional() {
        var condition = ParseLogicalOr();
        if (CurrentToken.Type == Lexer.TokenType.QuestionMark) {
            MoveNext();
            var trueExpr = ParseExpression();
            Expect(Lexer.TokenType.Colon);
            var falseExpr = ParseExpression();
            return new ConditionalExpression(condition, trueExpr, falseExpr);
        }
        return condition;
    }

    private LogicalExpression ParseLogicalOr() {
        var left = ParseLogicalAnd();
        while (CurrentToken.Type == Lexer.TokenType.OrKeyword || CurrentToken.Type == Lexer.TokenType.DoublePipe) {
            MoveNext();
            var right = ParseLogicalAnd();
            left = new BinaryExpression(BinaryExpressionType.Or, left, right);
        }
        return left;
    }

    private LogicalExpression ParseLogicalAnd() {
        var left = ParseEquality();
        while (CurrentToken.Type == Lexer.TokenType.AndKeyword || CurrentToken.Type == Lexer.TokenType.DoubleAmpersand) {
            MoveNext();
            var right = ParseEquality();
            left = new BinaryExpression(BinaryExpressionType.And, left, right);
        }
        return left;
    }

    private LogicalExpression ParseEquality() {
        var left = ParseRelational();
        while (CurrentToken.Type == Lexer.TokenType.Equal || CurrentToken.Type == Lexer.TokenType.NotEqual) {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseRelational();
            var type = op == Lexer.TokenType.Equal ? BinaryExpressionType.Equal : BinaryExpressionType.NotEqual;
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseRelational() {
        var left = ParseBitwiseOr();
        while (CurrentToken.Type == Lexer.TokenType.Less || CurrentToken.Type == Lexer.TokenType.Greater ||
               CurrentToken.Type == Lexer.TokenType.LessOrEqual || CurrentToken.Type == Lexer.TokenType.GreaterOrEqual) {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseBitwiseOr();
            var type = op switch {
                Lexer.TokenType.Less => BinaryExpressionType.LessThan,
                Lexer.TokenType.Greater => BinaryExpressionType.GreaterThan,
                Lexer.TokenType.LessOrEqual => BinaryExpressionType.LessThanOrEqual,
                Lexer.TokenType.GreaterOrEqual => BinaryExpressionType.GreaterThanOrEqual,
                _ => throw new System.InvalidOperationException($"未知的关系运算符：{op}")
            };
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseOr() {
        var left = ParseBitwiseXor();
        while (CurrentToken.Type == Lexer.TokenType.Pipe) {
            MoveNext();
            var right = ParseBitwiseXor();
            left = new BinaryExpression(BinaryExpressionType.BitwiseOr, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseXor() {
        var left = ParseBitwiseAnd();
        while (CurrentToken.Type == Lexer.TokenType.XorKeyword) {
            MoveNext();
            var right = ParseBitwiseAnd();
            left = new BinaryExpression(BinaryExpressionType.BitwiseXor, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseAnd() {
        var left = ParseShift();
        while (CurrentToken.Type == Lexer.TokenType.Ampersand) {
            MoveNext();
            var right = ParseShift();
            left = new BinaryExpression(BinaryExpressionType.BitwiseAnd, left, right);
        }
        return left;
    }

    private LogicalExpression ParseShift() {
        var left = ParseAdditive();
        while (CurrentToken.Type == Lexer.TokenType.LeftShift || CurrentToken.Type == Lexer.TokenType.RightShift || CurrentToken.Type == Lexer.TokenType.UnsignedRightShift) {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseAdditive();
            var type = op switch {
                Lexer.TokenType.LeftShift => BinaryExpressionType.LeftShift,
                Lexer.TokenType.RightShift => BinaryExpressionType.RightShift,
                Lexer.TokenType.UnsignedRightShift => BinaryExpressionType.UnsignedRightShift,
                _ => throw new System.InvalidOperationException($"未知的移位运算符：{op}")
            };
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseAdditive() {
        var left = ParseMultiplicative();
        while (CurrentToken.Type == Lexer.TokenType.Plus || CurrentToken.Type == Lexer.TokenType.Minus) {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseMultiplicative();
            var type = op == Lexer.TokenType.Plus ? BinaryExpressionType.Plus : BinaryExpressionType.Minus;
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseMultiplicative() {
        var left = ParsePower();
        while (CurrentToken.Type == Lexer.TokenType.Asterisk || CurrentToken.Type == Lexer.TokenType.Slash ||
               CurrentToken.Type == Lexer.TokenType.DoubleSlash || CurrentToken.Type == Lexer.TokenType.Percent ||
               CurrentToken.Type == Lexer.TokenType.ModKeyword) {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParsePower();
            var type = op switch {
                Lexer.TokenType.Asterisk => BinaryExpressionType.Multiply,
                Lexer.TokenType.Slash => BinaryExpressionType.Divide,
                Lexer.TokenType.DoubleSlash => BinaryExpressionType.IntegerDivide,
                Lexer.TokenType.Percent => BinaryExpressionType.Remainder,
                Lexer.TokenType.ModKeyword => BinaryExpressionType.Modulo,
                _ => throw new System.InvalidOperationException($"未知的乘法运算符：{op}")
            };
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParsePower() {
        var left = ParseUnary();
        if (CurrentToken.Type == Lexer.TokenType.Caret || CurrentToken.Type == Lexer.TokenType.DoubleAsterisk) {
            MoveNext();
            CheckDepth();
            var right = ParsePower();
            _depth--;
            return new BinaryExpression(BinaryExpressionType.Power, left, right);
        }
        return left;
    }

    private LogicalExpression ParseUnary() {
        if (CurrentToken.Type == Lexer.TokenType.Plus) {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Positive, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.Minus) {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Negate, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.NotKeyword || CurrentToken.Type == Lexer.TokenType.Exclamation) {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Not, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.Tilde) {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.BitwiseNot, operand);
        }
        return ParsePrimary();
    }

    private LogicalExpression ParsePrimary() {
        CheckDepth();

        LogicalExpression expr;
        switch (CurrentToken.Type) {
            case Lexer.TokenType.Number:
                var numText = CurrentToken.Text;
                double numValue;
                if (numText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    numValue = Convert.ToInt64(numText[2..], 16);
                else if (numText.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                    numValue = Convert.ToInt64(numText[2..], 8);
                else if (numText.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                    numValue = Convert.ToInt64(numText[2..], 2);
                else
                    numValue = double.Parse(numText, CultureInfo.InvariantCulture);
                MoveNext();
                expr = new ValueExpression(numValue);
                break;

            case Lexer.TokenType.String:
                var strValue = CurrentToken.Text;
                MoveNext();
                expr = new ValueExpression(strValue);
                break;

            case Lexer.TokenType.NaN:
                MoveNext();
                expr = new ValueExpression(double.NaN);
                break;

            case Lexer.TokenType.INF:
                MoveNext();
                expr = new ValueExpression(double.PositiveInfinity);
                break;

            case Lexer.TokenType.InterpolatedString:
                expr = ParseInterpolatedString(CurrentToken.Text);
                MoveNext();
                break;

            case Lexer.TokenType.Identifier:
                // 注意：此处不再提前 _depth--，否则函数调用内的递归嵌套不会累积深度（BUG-5）
                // _depth-- 统一在 switch 之后执行，确保 ParseIdentifierOrFunction 内的
                // ParseExpression → ... → ParsePrimary 链路正确累积深度
                expr = ParseIdentifierOrFunction();
                break;

            case Lexer.TokenType.LeftParenthesis:
                MoveNext();
                expr = ParseExpression();
                Expect(Lexer.TokenType.RightParenthesis);
                break;

            case Lexer.TokenType.LeftBracket:
                expr = ParseArrayLiteral();
                break;

            default:
                throw new SyntaxException(MathEvalErrorCode.UnexpectedToken, $"意外的标记 '{CurrentToken.Text}'", CurrentToken.Position);
        }

        // 统一在 switch 后递减深度（匹配 CheckDepth 的 +1）
        _depth--;

        // Postfix array indexing: supports arr[i], (expr)[i], [1,2,3][i]
        while (CurrentToken.Type == Lexer.TokenType.LeftBracket) {
            MoveNext();
            var index = ParseExpression();
            Expect(Lexer.TokenType.RightBracket);
            expr = new ArrayIndexExpression(expr, index);
        }

        return expr;
    }

    private LogicalExpression ParseIdentifierOrFunction() {
        var name = CurrentToken.Text;
        MoveNext();
        if (CurrentToken.Type == Lexer.TokenType.LeftParenthesis) {
            MoveNext();
            var arguments = new List<LogicalExpression>();
            if (CurrentToken.Type != Lexer.TokenType.RightParenthesis) {
                arguments.Add(ParseExpression());
                while (CurrentToken.Type == Lexer.TokenType.Comma) {
                    MoveNext();
                    arguments.Add(ParseExpression());
                }
            }
            Expect(Lexer.TokenType.RightParenthesis);
            return new FunctionCall(name, arguments);
        }
        return new Identifier(name);
    }

    private LogicalExpression ParseArrayLiteral() {
        MoveNext(); // skip [
        var elements = new List<LogicalExpression>();
        if (CurrentToken.Type != Lexer.TokenType.RightBracket) {
            elements.Add(ParseExpression());
            while (CurrentToken.Type == Lexer.TokenType.Comma) {
                MoveNext();
                elements.Add(ParseExpression());
            }
        }
        Expect(Lexer.TokenType.RightBracket);
        return new ArrayLiteralExpression(elements);
    }

    /// <summary>
    /// 解析插值字符串 Token 文本，构建 InterpolatedString AST 节点。
    /// Token 文本格式：$"content" 或 $'content'，包含 {{ }} 转义和 {expr:format} 插值
    /// </summary>
    private InterpolatedString ParseInterpolatedString(string rawText) {
        // 跳过 $" 或 $' 前缀
        int pos = 2;
        char quote = rawText[1];
        var segments = new List<InterpolationSegment>();
        var textBuilder = new StringBuilder();

        while (pos < rawText.Length) {
            char ch = rawText[pos];

            if (ch == quote) {
                break;
            }

            if (ch == '{') {
                if (pos + 1 < rawText.Length && rawText[pos + 1] == '{') {
                    textBuilder.Append('{');
                    pos += 2;
                    continue;
                }

                if (textBuilder.Length > 0) {
                    segments.Add(new TextSegment(textBuilder.ToString()));
                    textBuilder.Clear();
                }

                pos++;
                var (expression, formatSpec, newPos) = ParseInterpolationExpression(rawText, pos);
                segments.Add(new ExpressionSegment(expression, formatSpec));
                pos = newPos;
                continue;
            }

            if (ch == '}') {
                if (pos + 1 < rawText.Length && rawText[pos + 1] == '}') {
                    textBuilder.Append('}');
                    pos += 2;
                    continue;
                }
            }

            textBuilder.Append(ch);
            pos++;
        }

        if (textBuilder.Length > 0) {
            segments.Add(new TextSegment(textBuilder.ToString()));
        }

        if (segments.Count == 0) {
            segments.Add(new TextSegment(""));
        }

        return new InterpolatedString(segments);
    }

    /// <summary>
    /// 解析插值表达式 {expr:format}，返回表达式AST、格式说明符和结束位置
    /// </summary>
    private (LogicalExpression expression, string? formatSpec, int endPos) ParseInterpolationExpression(string rawText, int startPos) {
        var exprBuilder = new StringBuilder();
        int depth = 1;
        int pos = startPos;

        while (pos < rawText.Length && depth > 0) {
            char ch = rawText[pos];
            if (ch == '{') {
                depth++;
                exprBuilder.Append(ch);
            } else if (ch == '}') {
                depth--;
                if (depth > 0)
                    exprBuilder.Append(ch);
            } else if (ch == '\'' || ch == '"') {
                exprBuilder.Append(ch);
                pos++;
                while (pos < rawText.Length && rawText[pos] != ch) {
                    if (rawText[pos] == '\\') {
                        exprBuilder.Append(rawText[pos]);
                        pos++;
                        if (pos < rawText.Length) {
                            exprBuilder.Append(rawText[pos]);
                        }
                    } else {
                        exprBuilder.Append(rawText[pos]);
                    }
                    pos++;
                }
                if (pos < rawText.Length) {
                    exprBuilder.Append(rawText[pos]);
                }
            } else {
                exprBuilder.Append(ch);
            }
            pos++;
        }

        var exprText = exprBuilder.ToString().Trim();

        string? formatSpec = null;
        var colonIndex = FindFormatColon(exprText);
        if (colonIndex >= 0) {
            formatSpec = exprText[(colonIndex + 1)..].Trim();
            exprText = exprText[..colonIndex].Trim();
            if (formatSpec.Length == 0) formatSpec = null;   // 空格式说明符视为无格式
        }

        var innerLexer = new Lexer.Lexer(exprText);
        var innerParser = new Parser(innerLexer, _maxDepth);
        var expression = innerParser.Parse();

        return (expression, formatSpec, pos);
    }

    /// <summary>
    /// 查找格式说明符的冒号位置，跳过嵌套的括号、字符串与三元条件表达式的冒号
    /// </summary>
    private static int FindFormatColon(string text) {
        int parenDepth = 0;
        int ternaryDepth = 0;
        bool inString = false;
        char stringQuote = '\0';

        for (int i = 0; i < text.Length; i++) {
            char ch = text[i];

            if (inString) {
                if (ch == '\\') {
                    i++;
                    continue;
                }
                if (ch == stringQuote)
                    inString = false;
                continue;
            }

            if (ch == '\'' || ch == '"') {
                inString = true;
                stringQuote = ch;
                continue;
            }

            if (ch == '(') parenDepth++;
            else if (ch == ')') parenDepth--;
            else if (ch == '?') ternaryDepth++;
            else if (ch == ':') {
                if (ternaryDepth > 0) ternaryDepth--;
                else if (parenDepth == 0)
                    return i;
            }
        }

        return -1;
    }

    private void CheckDepth() {
        _depth++;
        if (_depth > _maxDepth)
            throw new SyntaxException(MathEvalErrorCode.NestingTooDeep, $"表达式嵌套深度超过最大限制 {_maxDepth}", CurrentToken.Position);
    }
}
