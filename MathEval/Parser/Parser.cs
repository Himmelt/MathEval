using MathEval.AST;
using MathEval.Exceptions;
using System.Globalization;

namespace MathEval.Parser;

public class Parser {
    private readonly Lexer.Lexer _lexer;
    private Lexer.Token _currentToken;
    private int _depth;
    private const int MaxDepth = 1024;

    public Parser(Lexer.Lexer lexer) {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
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
            throw new ParseException($"期望 '{type}'，但得到 '{CurrentToken.Type}'", CurrentToken.Line, CurrentToken.Column);
        MoveNext();
    }

    public LogicalExpression Parse() {
        _depth = 0;

        if (CurrentToken.Type == Lexer.TokenType.EOF)
            throw new ParseException("表达式不能为空", 1, 1);

        var expr = ParseExpression();
        if (CurrentToken.Type != Lexer.TokenType.EOF)
            throw new ParseException($"意外的标记 '{CurrentToken.Text}'", CurrentToken.Line, CurrentToken.Column);
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
                _ => throw new Exceptions.InvalidOperationException($"未知的关系运算符：{op}")
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
                _ => throw new Exceptions.InvalidOperationException($"未知的移位运算符：{op}")
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
                _ => throw new Exceptions.InvalidOperationException($"未知的乘法运算符：{op}")
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
                _depth--;
                expr = new ValueExpression(numValue);
                break;

            case Lexer.TokenType.NaN:
                MoveNext();
                _depth--;
                expr = new ValueExpression(double.NaN);
                break;

            case Lexer.TokenType.INF:
                MoveNext();
                _depth--;
                expr = new ValueExpression(double.PositiveInfinity);
                break;

            case Lexer.TokenType.Identifier:
                _depth--;
                expr = ParseIdentifierOrFunction();
                break;

            case Lexer.TokenType.LeftParenthesis:
                MoveNext();
                expr = ParseExpression();
                Expect(Lexer.TokenType.RightParenthesis);
                _depth--;
                break;

            case Lexer.TokenType.LeftBracket:
                _depth--;
                expr = ParseArrayLiteral();
                break;

            default:
                throw new ParseException($"意外的标记 '{CurrentToken.Text}'", CurrentToken.Line, CurrentToken.Column);
        }

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

    private void CheckDepth() {
        _depth++;
        if (_depth > MaxDepth)
            throw new ParseException("表达式嵌套深度超过最大限制 1024", CurrentToken.Line, CurrentToken.Column);
    }
}
