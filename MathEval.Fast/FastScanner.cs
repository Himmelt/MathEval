using MathEval.Fast.Exceptions;

namespace MathEval.Fast;

/// <summary>
/// 快速字符扫描器，基于 ReadOnlySpan&lt;char&gt; 操作，零字符串分配
/// </summary>
internal struct FastScanner(string text) {
    private readonly string _text = text ?? throw new ArgumentNullException(nameof(text));
    private int _position = 0;

    public readonly bool IsAtEnd => _position >= _text.Length;

    public readonly int Position => _position;

    public readonly ReadOnlySpan<char> Text => _text.AsSpan();

    public readonly char Peek() {
        return _position < _text.Length ? _text[_position] : '\0';
    }

    public readonly char PeekNext() {
        return _position + 1 < _text.Length ? _text[_position + 1] : '\0';
    }

    public char Read() {
        return _text[_position++];
    }

    public void SkipWhitespace() {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            _position++;
    }

    public void Advance(int count) {
        _position += count;
    }

    public ReadOnlySpan<char> ReadIdentifierSpan() {
        var start = _position;
        while (_position < _text.Length && IsIdentifierPart(_text[_position]))
            _position++;
        return _text.AsSpan(start, _position - start);
    }

    public double ReadDouble() {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X')) {
            _position += 2;
            return ReadHexAsDouble();
        }
        if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O')) {
            _position += 2;
            return ReadOctalAsDouble();
        }
        if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B')) {
            _position += 2;
            return ReadBinaryAsDouble();
        }
        return ReadDecimalAsDouble();
    }

    public long ReadLong() {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X')) {
            _position += 2;
            return ReadHexAsLong();
        }
        if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O')) {
            _position += 2;
            return ReadOctalAsLong();
        }
        if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B')) {
            _position += 2;
            return ReadBinaryAsLong();
        }
        return ReadDecimalAsLong();
    }

    internal static bool IsIdentifierStart(char ch) {
        return char.IsLetter(ch) || ch == '_'
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter;
    }

    internal static bool IsIdentifierPart(char ch) {
        return char.IsLetterOrDigit(ch) || ch == '_'
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherNumber;
    }

    private double ReadDecimalAsDouble() {
        var start = _position;

        while (_position < _text.Length && char.IsDigit(_text[_position]))
            _position++;

        if (_position < _text.Length && _text[_position] == '.') {
            _position++;
            while (_position < _text.Length && char.IsDigit(_text[_position]))
                _position++;
        }

        if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E')) {
            _position++;
            if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
                _position++;
            while (_position < _text.Length && char.IsDigit(_text[_position]))
                _position++;
        }

        return double.Parse(_text.AsSpan(start, _position - start));
    }

    private long ReadDecimalAsLong() {
        var start = _position;

        while (_position < _text.Length && char.IsDigit(_text[_position]))
            _position++;

        if (_position < _text.Length && (_text[_position] == '.' || _text[_position] == 'e' || _text[_position] == 'E')) {
            _position = start;
            return (long)ReadDecimalAsDouble();
        }

        if (_position == start)
            throw new FastEvalException("无效的数字", _position);

        return long.Parse(_text.AsSpan(start, _position - start));
    }

    private double ReadHexAsDouble() => ReadHexAsLong();

    private long ReadHexAsLong() {
        var start = _position;
        while (_position < _text.Length && IsHexDigit(_text[_position]))
            _position++;
        if (_position == start)
            throw new FastEvalException("无效的十六进制数", _position);
        return Convert.ToInt64(_text.Substring(start, _position - start), 16);
    }

    private double ReadOctalAsDouble() => ReadOctalAsLong();

    private long ReadOctalAsLong() {
        var start = _position;
        while (_position < _text.Length && IsOctalDigit(_text[_position]))
            _position++;
        if (_position == start)
            throw new FastEvalException("无效的八进制数", _position);
        return Convert.ToInt64(_text.Substring(start, _position - start), 8);
    }

    private double ReadBinaryAsDouble() => ReadBinaryAsLong();

    private long ReadBinaryAsLong() {
        var start = _position;
        while (_position < _text.Length && IsBinaryDigit(_text[_position]))
            _position++;
        if (_position == start)
            throw new FastEvalException("无效的二进制数", _position);
        return Convert.ToInt64(_text.Substring(start, _position - start), 2);
    }

    private static bool IsHexDigit(char ch) =>
        ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static bool IsOctalDigit(char ch) => ch is >= '0' and <= '7';

    private static bool IsBinaryDigit(char ch) => ch is '0' or '1';
}
