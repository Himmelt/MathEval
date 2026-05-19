using MathEval.AST;
using MathEval.Exceptions;

namespace MathEval.Parser;

public class Parser
{
    private readonly Lexer.Lexer _lexer;
    private Lexer.Token _currentToken;
    private int _depth = 0;
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
                _ => throw new InvalidOperationException()
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
                _ => throw new InvalidOperationException()
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
                return new ValueExpression(intValue);

            case Lexer.TokenType.Float:
                var floatValue = double.Parse(CurrentToken.Text);
                MoveNext();
                return new ValueExpression(floatValue);

            case Lexer.TokenType.String:
                var stringValue = CurrentToken.Text;
                MoveNext();
                return new ValueExpression(stringValue);

            case Lexer.TokenType.Boolean:
                var boolValue = CurrentToken.Text.Equals("true", StringComparison.OrdinalIgnoreCase);
                MoveNext();
                return new ValueExpression(boolValue);

            case Lexer.TokenType.NaN:
                MoveNext();
                return new ValueExpression(double.NaN);

            case Lexer.TokenType.INF:
                MoveNext();
                return new ValueExpression(double.PositiveInfinity);

            case Lexer.TokenType.Identifier:
                return ParseIdentifierOrFunction();

            case Lexer.TokenType.LeftParenthesis:
                MoveNext();
                var expr = ParseExpression();
                Expect(Lexer.TokenType.RightParenthesis);
                return expr;

            default:
                throw new ParseException($"Unexpected token '{CurrentToken.Text}'", CurrentToken.Line, CurrentToken.Column);
        }
    }

    private long ParseInteger(string text)
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

    private void CheckDepth()
    {
        _depth++;
        if (_depth > MaxDepth)
            throw new ParseException("Expression exceeds maximum nesting depth of 1024", CurrentToken.Line, CurrentToken.Column);
    }
}
