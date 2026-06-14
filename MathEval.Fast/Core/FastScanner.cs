using MathEval.Fast.Exceptions;
using System.Globalization;

namespace MathEval.Fast.Core;

/// <summary>
/// 高性能扫描器，供 FastEvaluator 和 BytecodeCompiler 复用
/// ref struct 零堆分配，JIT 可内联跨 struct 调用
/// </summary>
internal struct FastScanner(string expression) {
    private int _position = 0;
    private readonly string _expression = expression ?? throw new FastEvalException("表达式不能为 null");

    // 供错误消息使用
    public readonly int Position => _position;
    public readonly string Expression => _expression;

    public readonly bool IsAtEnd => _position >= _expression.Length;

    public readonly char Peek() => _position < _expression.Length ? _expression[_position] : '\0';

    public readonly char PeekNext() => _position + 1 < _expression.Length ? _expression[_position + 1] : '\0';

    public readonly char PeekNextNext() => _position + 2 < _expression.Length ? _expression[_position + 2] : '\0';

    public char Read() => _expression[_position++];

    public void SkipWhitespace() {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position])) _position++;
    }

    public ReadOnlySpan<char> ReadIdentifierSpan() {
        var start = _position;
        while (_position < _expression.Length && IsIdentifierPart(_expression[_position])) _position++;
        return _expression.AsSpan(start, _position - start);
    }

    public double ReadNumber() {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X')) {
            _position += 2;
            return ReadHex();
        }
        if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O')) {
            _position += 2;
            return ReadOctal();
        }
        if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B')) {
            _position += 2;
            return ReadBinary();
        }
        return ReadDecimal();
    }

    private double ReadDecimal() {
        var start = _position;
        while (_position < _expression.Length && char.IsDigit(_expression[_position])) _position++;
        if (_position < _expression.Length && _expression[_position] == '.') {
            _position++;
            while (_position < _expression.Length && char.IsDigit(_expression[_position])) _position++;
        }
        if (_position < _expression.Length && (_expression[_position] == 'e' || _expression[_position] == 'E')) {
            _position++;
            if (_position < _expression.Length && (_expression[_position] == '+' || _expression[_position] == '-')) _position++;
            while (_position < _expression.Length && char.IsDigit(_expression[_position])) _position++;
        }
        return double.Parse(_expression.AsSpan(start, _position - start), CultureInfo.InvariantCulture);
    }

    private double ReadHex() {
        var start = _position;
        while (_position < _expression.Length && IsHexDigit(_expression[_position])) _position++;
        if (_position == start) throw new FastEvalException("无效的十六进制数", _expression, _position);
        return Convert.ToInt64(_expression[start.._position], 16);
    }

    private double ReadOctal() {
        var start = _position;
        while (_position < _expression.Length && IsOctalDigit(_expression[_position])) _position++;
        if (_position == start) throw new FastEvalException("无效的八进制数", _expression, _position);
        return Convert.ToInt64(_expression[start.._position], 8);
    }

    private double ReadBinary() {
        var start = _position;
        while (_position < _expression.Length && IsBinaryDigit(_expression[_position])) _position++;
        if (_position == start) throw new FastEvalException("无效的二进制数", _expression, _position);
        return Convert.ToInt64(_expression[start.._position], 2);
    }

    internal static bool IsIdentifierStart(char ch) {
        return char.IsLetter(ch) || ch == '_' || char.GetUnicodeCategory(ch) == UnicodeCategory.OtherLetter;
    }

    internal static bool IsIdentifierPart(char ch) {
        return char.IsLetterOrDigit(ch) || ch == '_'
            || char.GetUnicodeCategory(ch) == UnicodeCategory.OtherLetter
            || char.GetUnicodeCategory(ch) == UnicodeCategory.OtherNumber;
    }

    private static bool IsHexDigit(char ch) => ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    private static bool IsOctalDigit(char ch) => ch is >= '0' and <= '7';
    private static bool IsBinaryDigit(char ch) => ch is '0' or '1';

    /// <summary>
    /// 尝试在当前位置匹配关键字（大小写不敏感），匹配成功则推进位置
    /// </summary>
    public bool TryMatchKeyword(string keyword) {
        SkipWhitespace();
        var pos = _position;

        if (pos + keyword.Length > _expression.Length) return false;

        for (int i = 0; i < keyword.Length; i++) {
            if (char.ToLowerInvariant(_expression[pos + i]) != char.ToLowerInvariant(keyword[i])) return false;
        }

        // 确保关键字后不是标识符字符
        if (pos + keyword.Length < _expression.Length && IsIdentifierPart(_expression[pos + keyword.Length])) return false;

        _position = pos + keyword.Length;
        return true;
    }

    public static bool EqualsLower(ReadOnlySpan<char> span, string keyword) {
        if (span.Length != keyword.Length) return false;
        for (int i = 0; i < keyword.Length; i++) {
            if (char.ToLowerInvariant(span[i]) != char.ToLowerInvariant(keyword[i])) return false;
        }
        return true;
    }
}