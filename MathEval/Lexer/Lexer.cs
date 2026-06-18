using MathEval.Exceptions;

namespace MathEval.Lexer;

/// <summary>
/// 词法分析器，将表达式字符串转换为 Token 序列
/// </summary>
public class Lexer {
    private readonly string _text;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private int _startPosition;
    private int _startLine;
    private int _startColumn;

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "true", TokenType.Number },
        { "false", TokenType.Number },
        { "and", TokenType.AndKeyword },
        { "or", TokenType.OrKeyword },
        { "not", TokenType.NotKeyword },
        { "xor", TokenType.XorKeyword },
        { "mod", TokenType.ModKeyword },
        { "NaN", TokenType.NaN },
        { "INF", TokenType.INF }
    };

    private static readonly HashSet<char> HexDigits = [.. "0123456789abcdefABCDEF"];
    private static readonly HashSet<char> OctalDigits = [.. "01234567"];
    private static readonly HashSet<char> BinaryDigits = [.. "01"];

    public Lexer(string text) {
        _text = text ?? throw new ArgumentNullException(nameof(text));

        if (_text.Length > 4096)
            throw new ParseException("表达式长度超过最大限制 4096 个字符", 1, 1);
    }

    public Token CurrentToken { get; private set; } = null!;

    /// <summary>
    /// 将表达式文本中的所有 Token 一次性收集为列表
    /// </summary>
    public List<Token> TokenizeAll() {
        var tokens = new List<Token>();
        do {
            MoveNext();
            tokens.Add(CurrentToken);
        } while (CurrentToken.Type != TokenType.EOF);
        return tokens;
    }

    public void MoveNext() {
        SkipWhitespace();
        if (IsAtEnd()) {
            CurrentToken = new Token(TokenType.EOF, "", _position, _line, _column);
            return;
        }

        _startPosition = _position;
        _startLine = _line;
        _startColumn = _column;

        char ch = Peek();

        if (char.IsDigit(ch)) {
            ScanNumber();
        } else if (IsIdentifierStart(ch)) {
            ScanIdentifier();
        } else {
            ScanOperator();
        }
    }

    private void SkipWhitespace() {
        while (!IsAtEnd() && char.IsWhiteSpace(Peek())) {
            if (Peek() == '\n') {
                _line++;
                _column = 1;
            } else {
                _column++;
            }
            _position++;
        }
    }

    private bool IsAtEnd() => _position >= _text.Length;

    private char Peek() => IsAtEnd() ? '\0' : _text[_position];

    private char PeekNext() => _position + 1 >= _text.Length ? '\0' : _text[_position + 1];

    private char Read() {
        char ch = _text[_position++];
        _column++;
        return ch;
    }

    private static bool IsIdentifierStart(char ch) {
        return char.IsLetter(ch) || ch == '_' || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter;
    }

    private static bool IsIdentifierPart(char ch) {
        return char.IsLetterOrDigit(ch) || ch == '_' || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherNumber;
    }

    private void ScanNumber() {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X')) {
            Read();
            Read();
            ScanHexNumber();
        } else if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O')) {
            Read();
            Read();
            ScanOctalNumber();
        } else if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B')) {
            Read();
            Read();
            ScanBinaryNumber();
        } else {
            ScanDecimalNumber();
        }
    }

    private void ScanHexNumber() {
        var sb = new StringBuilder("0x");
        while (!IsAtEnd() && HexDigits.Contains(Peek())) {
            sb.Append(Read());
        }
        if (sb.Length == 2)
            throw new ParseException("无效的十六进制数：'0x' 后缺少数字", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Number, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanOctalNumber() {
        var sb = new StringBuilder("0o");
        while (!IsAtEnd() && OctalDigits.Contains(Peek())) {
            sb.Append(Read());
        }
        if (sb.Length == 2)
            throw new ParseException("无效的八进制数：'0o' 后缺少数字", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Number, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanBinaryNumber() {
        var sb = new StringBuilder("0b");
        while (!IsAtEnd() && BinaryDigits.Contains(Peek())) {
            sb.Append(Read());
        }
        if (sb.Length == 2)
            throw new ParseException("无效的二进制数：'0b' 后缺少数字", _startLine, _startColumn);

        CurrentToken = new Token(TokenType.Number, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanDecimalNumber() {
        var sb = new StringBuilder();
        while (!IsAtEnd() && char.IsDigit(Peek())) {
            sb.Append(Read());
        }

        if (Peek() == '.') {
            sb.Append(Read());
            while (!IsAtEnd() && char.IsDigit(Peek())) {
                sb.Append(Read());
            }
        }

        if (Peek() == 'e' || Peek() == 'E') {
            sb.Append(Read());
            if (Peek() == '+' || Peek() == '-')
                sb.Append(Read());
            if (!IsAtEnd() && char.IsDigit(Peek())) {
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    sb.Append(Read());
            } else {
                throw new ParseException("无效的数字：指数后缺少数字", _startLine, _startColumn);
            }
        }

        CurrentToken = new Token(TokenType.Number, sb.ToString(), _startPosition, _startLine, _startColumn);
    }

    private void ScanIdentifier() {
        var sb = new StringBuilder();
        while (!IsAtEnd() && IsIdentifierPart(Peek())) {
            sb.Append(Read());
        }

        var text = sb.ToString();
        if (Keywords.TryGetValue(text, out var type)) {
            if (type == TokenType.Number) {
                var numberText = text.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
                CurrentToken = new Token(TokenType.Number, numberText, _startPosition, _startLine, _startColumn);
            } else {
                CurrentToken = new Token(type, text, _startPosition, _startLine, _startColumn);
            }
        } else {
            CurrentToken = new Token(TokenType.Identifier, text, _startPosition, _startLine, _startColumn);
        }
    }

    private void ScanOperator() {
        char ch = Read();
        switch (ch) {
            case '+':
                CurrentToken = new Token(TokenType.Plus, "+", _startPosition, _startLine, _startColumn);
                break;
            case '-':
                CurrentToken = new Token(TokenType.Minus, "-", _startPosition, _startLine, _startColumn);
                break;
            case '*':
                if (Peek() == '*') {
                    Read();
                    CurrentToken = new Token(TokenType.DoubleAsterisk, "**", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Asterisk, "*", _startPosition, _startLine, _startColumn);
                }
                break;
            case '%':
                CurrentToken = new Token(TokenType.Percent, "%", _startPosition, _startLine, _startColumn);
                break;
            case '^':
                CurrentToken = new Token(TokenType.Caret, "^", _startPosition, _startLine, _startColumn);
                break;
            case '&':
                if (Peek() == '&') {
                    Read();
                    CurrentToken = new Token(TokenType.DoubleAmpersand, "&&", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Ampersand, "&", _startPosition, _startLine, _startColumn);
                }
                break;
            case '|':
                if (Peek() == '|') {
                    Read();
                    CurrentToken = new Token(TokenType.DoublePipe, "||", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Pipe, "|", _startPosition, _startLine, _startColumn);
                }
                break;
            case '~':
                CurrentToken = new Token(TokenType.Tilde, "~", _startPosition, _startLine, _startColumn);
                break;
            case '!':
                if (Peek() == '=') {
                    Read();
                    CurrentToken = new Token(TokenType.NotEqual, "!=", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Exclamation, "!", _startPosition, _startLine, _startColumn);
                }
                break;
            case '=':
                if (Peek() == '=') {
                    Read();
                    CurrentToken = new Token(TokenType.Equal, "==", _startPosition, _startLine, _startColumn);
                } else {
                    throw new ParseException("'=' 后应为 '='", _line, _column);
                }
                break;
            case '<':
                if (Peek() == '=') {
                    Read();
                    CurrentToken = new Token(TokenType.LessOrEqual, "<=", _startPosition, _startLine, _startColumn);
                } else if (Peek() == '<') {
                    Read();
                    CurrentToken = new Token(TokenType.LeftShift, "<<", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Less, "<", _startPosition, _startLine, _startColumn);
                }
                break;
            case '>':
                if (Peek() == '=') {
                    Read();
                    CurrentToken = new Token(TokenType.GreaterOrEqual, ">=", _startPosition, _startLine, _startColumn);
                } else if (Peek() == '>') {
                    Read();
                    if (Peek() == '>') {
                        Read();
                        CurrentToken = new Token(TokenType.UnsignedRightShift, ">>>", _startPosition, _startLine, _startColumn);
                    } else {
                        CurrentToken = new Token(TokenType.RightShift, ">>", _startPosition, _startLine, _startColumn);
                    }
                } else {
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
            case '[':
                CurrentToken = new Token(TokenType.LeftBracket, "[", _startPosition, _startLine, _startColumn);
                break;
            case ']':
                CurrentToken = new Token(TokenType.RightBracket, "]", _startPosition, _startLine, _startColumn);
                break;
            case ',':
                CurrentToken = new Token(TokenType.Comma, ",", _startPosition, _startLine, _startColumn);
                break;
            case '/':
                if (Peek() == '/') {
                    Read();
                    CurrentToken = new Token(TokenType.DoubleSlash, "//", _startPosition, _startLine, _startColumn);
                } else {
                    CurrentToken = new Token(TokenType.Slash, "/", _startPosition, _startLine, _startColumn);
                }
                break;
            default:
                throw new ParseException($"意外的字符 '{ch}'", _line, _column);
        }
    }
}
