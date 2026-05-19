using System.Text;
using System.Text.RegularExpressions;
using MathEval.Exceptions;

namespace MathEval.Lexer;

/// <summary>
/// 词法分析器，将表达式字符串转换为 Token 序列
/// </summary>
public class Lexer
{
    private readonly string _text;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private int _startPosition;
    private int _startLine;
    private int _startColumn;

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "true", TokenType.Boolean },
        { "false", TokenType.Boolean },
        { "and", TokenType.AndKeyword },
        { "or", TokenType.OrKeyword },
        { "not", TokenType.NotKeyword },
        { "xor", TokenType.XorKeyword },
        { "NaN", TokenType.NaN },
        { "INF", TokenType.INF }
    };

    private static readonly HashSet<char> HexDigits = new("0123456789abcdefABCDEF");
    private static readonly HashSet<char> OctalDigits = new("01234567");
    private static readonly HashSet<char> BinaryDigits = new("01");

    public Lexer(string text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));

        if (_text.Length > 4096)
            throw new ParseException("Expression exceeds maximum length of 4096 characters", 1, 1);
    }

    public Token CurrentToken { get; private set; } = null!;

    public void MoveNext()
    {
        SkipWhitespace();
        if (IsAtEnd())
        {
            CurrentToken = new Token(TokenType.EOF, "", _position, _line, _column);
            return;
        }

        _startPosition = _position;
        _startLine = _line;
        _startColumn = _column;

        char ch = Peek();

        if (char.IsDigit(ch))
        {
            ScanNumber();
        }
        else if (IsIdentifierStart(ch))
        {
            ScanIdentifier();
        }
        else if (ch == '\'' || ch == '"')
        {
            ScanString();
        }
        else if (ch == '$' && (PeekNext() == '\'' || PeekNext() == '"'))
        {
            ScanInterpolatedString();
        }
        else
        {
            ScanOperator();
        }
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
        {
            if (Peek() == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private bool IsAtEnd() => _position >= _text.Length;

    private char Peek() => IsAtEnd() ? '\0' : _text[_position];

    private char PeekNext() => _position + 1 >= _text.Length ? '\0' : _text[_position + 1];

    private char Read()
    {
        char ch = _text[_position++];
        _column++;
        return ch;
    }

    private bool IsIdentifierStart(char ch)
    {
        return char.IsLetter(ch) || ch == '_' || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter;
    }

    private bool IsIdentifierPart(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherNumber;
    }

    private void ScanNumber()
    {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            Read();
            Read();
            ScanHexNumber();
        }
        else if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O'))
        {
            Read();
            Read();
            ScanOctalNumber();
        }
        else if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B'))
        {
            Read();
            Read();
            ScanBinaryNumber();
        }
        else
        {
            ScanDecimalNumber();
        }
    }

    private void ScanHexNumber()
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && HexDigits.Contains(Peek()))
        {
            sb.Append(Read());
        }
        if (sb.Length == 0)
            throw new ParseException("Invalid hexadecimal number", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Integer, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanOctalNumber()
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && OctalDigits.Contains(Peek()))
        {
            sb.Append(Read());
        }
        if (sb.Length == 0)
            throw new ParseException("Invalid octal number", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Integer, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanBinaryNumber()
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && BinaryDigits.Contains(Peek()))
        {
            sb.Append(Read());
        }
        if (sb.Length == 0)
            throw new ParseException("Invalid binary number", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Integer, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanDecimalNumber()
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Read());
        }

        if (Peek() == '.')
        {
            sb.Append(Read());
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Read());
            }
            CurrentToken = new Token(TokenType.Float, sb.ToString(), _startPosition, _startLine, _startColumn);
        }
        else if (Peek() == 'e' || Peek() == 'E')
        {
            sb.Append(Read());
            if (Peek() == '+' || Peek() == '-')
                sb.Append(Read());
            if (!IsAtEnd() && char.IsDigit(Peek()))
            {
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    sb.Append(Read());
                CurrentToken = new Token(TokenType.Float, sb.ToString(), _startPosition, _startLine, _startColumn);
            }
            else
            {
                CurrentToken = new Token(TokenType.Integer, sb.ToString(), _startPosition, _startLine, _startColumn);
            }
        }
        else
        {
            CurrentToken = new Token(TokenType.Integer, sb.ToString(), _startPosition, _startLine, _startColumn);
        }
    }

    private void ScanIdentifier()
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && IsIdentifierPart(Peek()))
        {
            sb.Append(Read());
        }

        var text = sb.ToString();
        if (Keywords.TryGetValue(text, out var type))
        {
            CurrentToken = new Token(type, text, _startPosition, _startLine, _startColumn);
        }
        else
        {
            CurrentToken = new Token(TokenType.Identifier, text, _startPosition, _startLine, _startColumn);
        }
    }

    private void ScanString()
    {
        char quote = Read();
        var sb = new StringBuilder();
        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '\\')
            {
                Read();
                if (IsAtEnd())
                    throw new ParseException("Unexpected end of string", _line, _column);
                char escaped = Read();
                switch (escaped)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '0': sb.Append('\0'); break;
                    case '\\': sb.Append('\\'); break;
                    case '\'': sb.Append('\''); break;
                    case '"': sb.Append('"'); break;
                    case 'x':
                        if (_position + 2 > _text.Length)
                            throw new ParseException("Invalid hexadecimal escape sequence", _line, _column);
                        var hex = new string(new[] { Read(), Read() });
                        sb.Append((char)Convert.ToInt32(hex, 16));
                        break;
                    case 'u':
                        if (_position + 4 > _text.Length)
                            throw new ParseException("Invalid unicode escape sequence", _line, _column);
                        var uni = new string(new[] { Read(), Read(), Read(), Read() });
                        sb.Append((char)Convert.ToInt32(uni, 16));
                        break;
                    default:
                        throw new ParseException($"Invalid escape sequence '\\{escaped}'", _line, _column);
                }
            }
            else
            {
                sb.Append(Read());
            }
        }

        if (IsAtEnd())
            throw new ParseException("Unterminated string literal", _line, _column);

        Read();
        CurrentToken = new Token(TokenType.String, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanInterpolatedString()
    {
        Read();
        char quote = Read();
        var sb = new StringBuilder();
        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '{')
            {
                if (PeekNext() == '{')
                {
                    sb.Append(Read());
                    Read();
                }
                else
                {
                    sb.Append(Read());
                }
            }
            else if (Peek() == '}')
            {
                if (PeekNext() == '}')
                {
                    sb.Append(Read());
                    Read();
                }
                else
                {
                    sb.Append(Read());
                }
            }
            else
            {
                sb.Append(Read());
            }
        }

        if (IsAtEnd())
            throw new ParseException("Unterminated interpolated string", _line, _column);

        Read();
        CurrentToken = new Token(TokenType.String, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanOperator()
    {
        char ch = Read();
        switch (ch)
        {
            case '+':
                CurrentToken = new Token(TokenType.Plus, "+", _startPosition, _startLine, _startColumn);
                break;
            case '-':
                CurrentToken = new Token(TokenType.Minus, "-", _startPosition, _startLine, _startColumn);
                break;
            case '*':
                CurrentToken = new Token(TokenType.Asterisk, "*", _startPosition, _startLine, _startColumn);
                break;
            case '%':
                CurrentToken = new Token(TokenType.Percent, "%", _startPosition, _startLine, _startColumn);
                break;
            case '^':
                CurrentToken = new Token(TokenType.Caret, "^", _startPosition, _startLine, _startColumn);
                break;
            case '&':
                if (Peek() == '&')
                {
                    Read();
                    CurrentToken = new Token(TokenType.DoubleAmpersand, "&&", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Ampersand, "&", _startPosition, _startLine, _startColumn);
                }
                break;
            case '|':
                if (Peek() == '|')
                {
                    Read();
                    CurrentToken = new Token(TokenType.DoublePipe, "||", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Pipe, "|", _startPosition, _startLine, _startColumn);
                }
                break;
            case '~':
                CurrentToken = new Token(TokenType.Tilde, "~", _startPosition, _startLine, _startColumn);
                break;
            case '!':
                if (Peek() == '=')
                {
                    Read();
                    CurrentToken = new Token(TokenType.NotEqual, "!=", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Exclamation, "!", _startPosition, _startLine, _startColumn);
                }
                break;
            case '=':
                if (Peek() == '=')
                {
                    Read();
                    CurrentToken = new Token(TokenType.Equal, "==", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    throw new ParseException("Expected '=' after '='", _line, _column);
                }
                break;
            case '<':
                if (Peek() == '=')
                {
                    Read();
                    CurrentToken = new Token(TokenType.LessOrEqual, "<=", _startPosition, _startLine, _startColumn);
                }
                else if (Peek() == '<')
                {
                    Read();
                    CurrentToken = new Token(TokenType.LeftShift, "<<", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Less, "<", _startPosition, _startLine, _startColumn);
                }
                break;
            case '>':
                if (Peek() == '=')
                {
                    Read();
                    CurrentToken = new Token(TokenType.GreaterOrEqual, ">=", _startPosition, _startLine, _startColumn);
                }
                else if (Peek() == '>')
                {
                    Read();
                    CurrentToken = new Token(TokenType.RightShift, ">>", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Greater, ">", _startPosition, _startLine, _startColumn);
                }
                break;
            case '?':
                CurrentToken = new Token(TokenType.QuestionMark, "?", _startPosition, _startLine, _startColumn);
                break;
            case ':':
                CurrentToken = new Token(TokenType.Colon, ":", _startPosition, _startLine, _startColumn);
                break;
            case '(':
                CurrentToken = new Token(TokenType.LeftParenthesis, "(", _startPosition, _startLine, _startColumn);
                break;
            case ')':
                CurrentToken = new Token(TokenType.RightParenthesis, ")", _startPosition, _startLine, _startColumn);
                break;
            case ',':
                CurrentToken = new Token(TokenType.Comma, ",", _startPosition, _startLine, _startColumn);
                break;
            case '/':
                if (Peek() == '/')
                {
                    Read();
                    CurrentToken = new Token(TokenType.DoubleSlash, "//", _startPosition, _startLine, _startColumn);
                }
                else
                {
                    CurrentToken = new Token(TokenType.Slash, "/", _startPosition, _startLine, _startColumn);
                }
                break;
            default:
                throw new ParseException($"Unexpected character '{ch}'", _line, _column);
        }
    }
}
