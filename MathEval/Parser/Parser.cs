using System.Text;
using MathEval.AST;
using MathEval.Exceptions;

namespace MathEval.Parser;

public class Parser
{
    private readonly Lexer.Lexer _lexer;
    private Lexer.Token _currentToken;
    private int _depth;
    private const int MaxDepth = 1024;

    public Parser(Lexer.Lexer lexer)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _lexer.MoveNext();
        _currentToken = _lexer.CurrentToken;
    }

    private Lexer.Token CurrentToken => _currentToken;

    private void MoveNext()
    {
        _lexer.MoveNext();
        _currentToken = _lexer.CurrentToken;
    }

    private void Expect(Lexer.TokenType type)
    {
        if (CurrentToken.Type != type)
            throw new ParseException($"Expected '{type}' but got '{CurrentToken.Type}'", CurrentToken.Line, CurrentToken.Column);
        MoveNext();
    }

    public LogicalExpression Parse()
    {
        _depth = 0;

        if (CurrentToken.Type == Lexer.TokenType.EOF)
            throw new ParseException("Expression cannot be empty", 1, 1);

        var expr = ParseExpression();
        if (CurrentToken.Type != Lexer.TokenType.EOF)
            throw new ParseException($"Unexpected token '{CurrentToken.Text}'", CurrentToken.Line, CurrentToken.Column);
        return expr;
    }

    private LogicalExpression ParseExpression()
    {
        return ParseConditional();
    }

    private LogicalExpression ParseConditional()
    {
        var condition = ParseLogicalOr();
        if (CurrentToken.Type == Lexer.TokenType.QuestionMark)
        {
            MoveNext();
            var trueExpr = ParseExpression();
            Expect(Lexer.TokenType.Colon);
            var falseExpr = ParseExpression();
            return new ConditionalExpression(condition, trueExpr, falseExpr);
        }
        return condition;
    }

    private LogicalExpression ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (CurrentToken.Type == Lexer.TokenType.OrKeyword || CurrentToken.Type == Lexer.TokenType.DoublePipe)
        {
            MoveNext();
            var right = ParseLogicalAnd();
            left = new BinaryExpression(BinaryExpressionType.Or, left, right);
        }
        return left;
    }

    private LogicalExpression ParseLogicalAnd()
    {
        var left = ParseEquality();
        while (CurrentToken.Type == Lexer.TokenType.AndKeyword || CurrentToken.Type == Lexer.TokenType.DoubleAmpersand)
        {
            MoveNext();
            var right = ParseEquality();
            left = new BinaryExpression(BinaryExpressionType.And, left, right);
        }
        return left;
    }

    private LogicalExpression ParseEquality()
    {
        var left = ParseRelational();
        while (CurrentToken.Type == Lexer.TokenType.Equal || CurrentToken.Type == Lexer.TokenType.NotEqual)
        {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseRelational();
            var type = op == Lexer.TokenType.Equal ? BinaryExpressionType.Equal : BinaryExpressionType.NotEqual;
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseRelational()
    {
        var left = ParseBitwiseOr();
        while (CurrentToken.Type == Lexer.TokenType.Less || CurrentToken.Type == Lexer.TokenType.Greater ||
               CurrentToken.Type == Lexer.TokenType.LessOrEqual || CurrentToken.Type == Lexer.TokenType.GreaterOrEqual)
        {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseBitwiseOr();
            var type = op switch
            {
                Lexer.TokenType.Less => BinaryExpressionType.LessThan,
                Lexer.TokenType.Greater => BinaryExpressionType.GreaterThan,
                Lexer.TokenType.LessOrEqual => BinaryExpressionType.LessThanOrEqual,
                Lexer.TokenType.GreaterOrEqual => BinaryExpressionType.GreaterThanOrEqual,
                _ => throw new Exceptions.InvalidOperationException($"Unknown relational operator: {op}")
            };
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();
        while (CurrentToken.Type == Lexer.TokenType.Pipe)
        {
            MoveNext();
            var right = ParseBitwiseXor();
            left = new BinaryExpression(BinaryExpressionType.BitwiseOr, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();
        while (CurrentToken.Type == Lexer.TokenType.XorKeyword)
        {
            MoveNext();
            var right = ParseBitwiseAnd();
            left = new BinaryExpression(BinaryExpressionType.BitwiseXor, left, right);
        }
        return left;
    }

    private LogicalExpression ParseBitwiseAnd()
    {
        var left = ParseShift();
        while (CurrentToken.Type == Lexer.TokenType.Ampersand)
        {
            MoveNext();
            var right = ParseShift();
            left = new BinaryExpression(BinaryExpressionType.BitwiseAnd, left, right);
        }
        return left;
    }

    private LogicalExpression ParseShift()
    {
        var left = ParseAdditive();
        while (CurrentToken.Type == Lexer.TokenType.LeftShift || CurrentToken.Type == Lexer.TokenType.RightShift)
        {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseAdditive();
            var type = op == Lexer.TokenType.LeftShift ? BinaryExpressionType.LeftShift : BinaryExpressionType.RightShift;
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (CurrentToken.Type == Lexer.TokenType.Plus || CurrentToken.Type == Lexer.TokenType.Minus)
        {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParseMultiplicative();
            var type = op == Lexer.TokenType.Plus ? BinaryExpressionType.Plus : BinaryExpressionType.Minus;
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParseMultiplicative()
    {
        var left = ParsePower();
        while (CurrentToken.Type == Lexer.TokenType.Asterisk || CurrentToken.Type == Lexer.TokenType.Slash ||
               CurrentToken.Type == Lexer.TokenType.DoubleSlash || CurrentToken.Type == Lexer.TokenType.Percent)
        {
            var op = CurrentToken.Type;
            MoveNext();
            var right = ParsePower();
            var type = op switch
            {
                Lexer.TokenType.Asterisk => BinaryExpressionType.Multiply,
                Lexer.TokenType.Slash => BinaryExpressionType.Divide,
                Lexer.TokenType.DoubleSlash => BinaryExpressionType.IntegerDivide,
                Lexer.TokenType.Percent => BinaryExpressionType.Modulo,
                _ => throw new Exceptions.InvalidOperationException($"Unknown multiplicative operator: {op}")
            };
            left = new BinaryExpression(type, left, right);
        }
        return left;
    }

    private LogicalExpression ParsePower()
    {
        var left = ParseUnary();
        if (CurrentToken.Type == Lexer.TokenType.Caret)
        {
            MoveNext();
            CheckDepth();
            var right = ParsePower();
            _depth--;
            return new BinaryExpression(BinaryExpressionType.Power, left, right);
        }
        return left;
    }

    private LogicalExpression ParseUnary()
    {
        if (CurrentToken.Type == Lexer.TokenType.Plus)
        {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Positive, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.Minus)
        {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Negate, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.NotKeyword || CurrentToken.Type == Lexer.TokenType.Exclamation)
        {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.Not, operand);
        }
        if (CurrentToken.Type == Lexer.TokenType.Tilde)
        {
            MoveNext();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryExpressionType.BitwiseNot, operand);
        }
        return ParsePrimary();
    }

    private LogicalExpression ParsePrimary()
    {
        CheckDepth();

        switch (CurrentToken.Type)
        {
            case Lexer.TokenType.Integer:
                var intValue = ParseInteger(CurrentToken.Text);
                MoveNext();
                _depth--;
                return new ValueExpression(intValue);

            case Lexer.TokenType.Float:
                var floatValue = double.Parse(CurrentToken.Text);
                MoveNext();
                _depth--;
                return new ValueExpression(floatValue);

            case Lexer.TokenType.String:
                var stringValue = CurrentToken.Text;
                MoveNext();
                _depth--;
                return new ValueExpression(stringValue);

            case Lexer.TokenType.Boolean:
                var boolValue = CurrentToken.Text.Equals("true", StringComparison.OrdinalIgnoreCase);
                MoveNext();
                _depth--;
                return new ValueExpression(boolValue);

            case Lexer.TokenType.NaN:
                MoveNext();
                _depth--;
                return new ValueExpression(double.NaN);

            case Lexer.TokenType.INF:
                MoveNext();
                _depth--;
                return new ValueExpression(double.PositiveInfinity);

            case Lexer.TokenType.Identifier:
                _depth--;
                return ParseIdentifierOrFunction();

            case Lexer.TokenType.LeftParenthesis:
                MoveNext();
                var expr = ParseExpression();
                Expect(Lexer.TokenType.RightParenthesis);
                _depth--;
                return expr;

            case Lexer.TokenType.InterpolatedString:
                var interpolated = ParseInterpolatedString(CurrentToken.Text);
                MoveNext();
                _depth--;
                return interpolated;

            default:
                throw new ParseException($"Unexpected token '{CurrentToken.Text}'", CurrentToken.Line, CurrentToken.Column);
        }
    }

    private long ParseInteger(string text)
    {
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(text[2..], 16);
            }
            if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(text[2..], 8);
            }
            if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(text[2..], 2);
            }
            return long.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new ParseException($"Invalid number format: {text}", CurrentToken.Line, CurrentToken.Column, ex);
        }
        catch (global::System.OverflowException)
        {
            throw new Exceptions.OverflowException($"Number '{text}' is too large");
        }
    }

    private LogicalExpression ParseIdentifierOrFunction()
    {
        var name = CurrentToken.Text;
        MoveNext();
        if (CurrentToken.Type == Lexer.TokenType.LeftParenthesis)
        {
            MoveNext();
            var arguments = new List<LogicalExpression>();
            if (CurrentToken.Type != Lexer.TokenType.RightParenthesis)
            {
                arguments.Add(ParseExpression());
                while (CurrentToken.Type == Lexer.TokenType.Comma)
                {
                    MoveNext();
                    arguments.Add(ParseExpression());
                }
            }
            Expect(Lexer.TokenType.RightParenthesis);
            return new FunctionCall(name, arguments);
        }
        return new Identifier(name);
    }

    /// <summary>
    /// 解析插值字符串 Token 文本，构建 InterpolatedString AST 节点。
    /// Token 文本格式：$"content" 或 $'content'，包含 {{ }} 转义和 {expr:format} 插值
    /// </summary>
    private InterpolatedString ParseInterpolatedString(string rawText)
    {
        // 跳过 $" 或 $' 前缀
        int pos = 2;
        char quote = rawText[1];
        var segments = new List<InterpolationSegment>();
        var textBuilder = new StringBuilder();

        while (pos < rawText.Length)
        {
            char ch = rawText[pos];

            if (ch == quote)
            {
                break;
            }

            if (ch == '{')
            {
                if (pos + 1 < rawText.Length && rawText[pos + 1] == '{')
                {
                    textBuilder.Append('{');
                    pos += 2;
                    continue;
                }

                if (textBuilder.Length > 0)
                {
                    segments.Add(new TextSegment(textBuilder.ToString()));
                    textBuilder.Clear();
                }

                pos++;
                var (expression, formatSpec, newPos) = ParseInterpolationExpression(rawText, pos);
                segments.Add(new ExpressionSegment(expression, formatSpec));
                pos = newPos;
                continue;
            }

            if (ch == '}')
            {
                if (pos + 1 < rawText.Length && rawText[pos + 1] == '}')
                {
                    textBuilder.Append('}');
                    pos += 2;
                    continue;
                }
            }

            textBuilder.Append(ch);
            pos++;
        }

        if (textBuilder.Length > 0)
        {
            segments.Add(new TextSegment(textBuilder.ToString()));
        }

        if (segments.Count == 0)
        {
            segments.Add(new TextSegment(""));
        }

        return new InterpolatedString(segments);
    }

    /// <summary>
    /// 解析插值表达式 {expr:format}，返回表达式AST、格式说明符和结束位置
    /// </summary>
    private (LogicalExpression expression, string? formatSpec, int endPos) ParseInterpolationExpression(string rawText, int startPos)
    {
        var exprBuilder = new StringBuilder();
        int depth = 1;
        int pos = startPos;

        while (pos < rawText.Length && depth > 0)
        {
            char ch = rawText[pos];
            if (ch == '{')
            {
                depth++;
                exprBuilder.Append(ch);
            }
            else if (ch == '}')
            {
                depth--;
                if (depth > 0)
                    exprBuilder.Append(ch);
            }
            else if (ch == '\'' || ch == '"')
            {
                exprBuilder.Append(ch);
                pos++;
                while (pos < rawText.Length && rawText[pos] != ch)
                {
                    if (rawText[pos] == '\\')
                    {
                        exprBuilder.Append(rawText[pos]);
                        pos++;
                        if (pos < rawText.Length)
                        {
                            exprBuilder.Append(rawText[pos]);
                        }
                    }
                    else
                    {
                        exprBuilder.Append(rawText[pos]);
                    }
                    pos++;
                }
                if (pos < rawText.Length)
                {
                    exprBuilder.Append(rawText[pos]);
                }
            }
            else
            {
                exprBuilder.Append(ch);
            }
            pos++;
        }

        var exprText = exprBuilder.ToString().Trim();

        string? formatSpec = null;
        var colonIndex = FindFormatColon(exprText);
        if (colonIndex >= 0)
        {
            formatSpec = exprText[(colonIndex + 1)..].Trim();
            exprText = exprText[..colonIndex].Trim();
        }

        var innerLexer = new Lexer.Lexer(exprText);
        var innerParser = new Parser(innerLexer);
        var expression = innerParser.Parse();

        return (expression, formatSpec, pos);
    }

    /// <summary>
    /// 查找格式说明符的冒号位置，跳过嵌套的括号和字符串
    /// </summary>
    private static int FindFormatColon(string text)
    {
        int depth = 0;
        bool inString = false;
        char stringQuote = '\0';

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (inString)
            {
                if (ch == '\\')
                {
                    i++;
                    continue;
                }
                if (ch == stringQuote)
                    inString = false;
                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                inString = true;
                stringQuote = ch;
                continue;
            }

            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ':' && depth == 0)
                return i;
        }

        return -1;
    }

    private void CheckDepth()
    {
        _depth++;
        if (_depth > MaxDepth)
            throw new ParseException("Expression exceeds maximum nesting depth of 1024", CurrentToken.Line, CurrentToken.Column);
    }
}
