using MathEval.Fast.BuiltIn;
using MathEval.Fast.Exceptions;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace MathEval.Fast.Core;

/// <summary>
/// 递归求值器，边扫描边求值，零 AST 中间层
/// <br/>
/// 内部统一使用 double 运算，仅在最终返回时按需转换类型
/// <br/>
/// 优化：ref struct 栈分配、Span 直接比较、stackalloc 参数、内联运算符、标识符统一解析
/// </summary>
internal ref struct FastEvaluator(string expression) {

    private bool _skipMode = false;
    private FastScanner _scanner = new(expression);
    private readonly string _expression = expression ?? throw new FastEvalException("表达式不能为 null");

    // 变量查找：FrozenDictionary + AlternateLookup<ReadOnlySpan<char>>，零字符串分配
    private readonly FrozenDictionary<string, double>? _frozenVars;
    private readonly FrozenDictionary<string, double>.AlternateLookup<ReadOnlySpan<char>> _varLookup;

    public FastEvaluator(string expression, IReadOnlyDictionary<string, double>? variables = null) : this(expression) {
        if (variables != null) {
            _frozenVars = variables.ToFrozenDictionary();
            _varLookup = _frozenVars.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }

    public double Evaluate() {
        SkipWhitespace();
        if (IsAtEnd) throw new FastEvalException("表达式不能为空", _expression);

        var result = EvalExpression();

        SkipWhitespace();
        if (!IsAtEnd) throw new FastEvalException($"意外的字符 '{Peek()}'，位置 {_scanner.Position}", _expression);

        return result;
    }

    #region Scanner（委托给 FastScanner）

    private readonly bool IsAtEnd => _scanner.IsAtEnd;
    private readonly char Peek() => _scanner.Peek();
    private readonly char PeekNext() => _scanner.PeekNext();
    private readonly char PeekNextNext() => _scanner.PeekNextNext();
    private char Read() => _scanner.Read();
    private void SkipWhitespace() => _scanner.SkipWhitespace();
    private ReadOnlySpan<char> ReadIdentifierSpan() => _scanner.ReadIdentifierSpan();
    private double ReadNumber() => _scanner.ReadNumber();

    #endregion

    #region 求值方法

    private double EvalExpression() => EvalConditional();

    private double EvalConditional() {
        var condition = EvalLogicalOr();
        SkipWhitespace();

        if (Peek() == '?') {
            Read();
            if (ConvertToBool(condition)) {
                var trueValue = EvalExpression();
                SkipWhitespace();
                if (Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'", _expression);
                Read();
                var savedSkipMode = _skipMode;
                _skipMode = true;
                EvalExpression();
                _skipMode = savedSkipMode;
                return trueValue;
            } else {
                var savedSkipMode = _skipMode;
                _skipMode = true;
                EvalExpression();
                _skipMode = savedSkipMode;
                SkipWhitespace();
                if (Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'", _expression);
                Read();
                var falseValue = EvalExpression();
                return falseValue;
            }
        }

        return condition;
    }

    private double EvalLogicalOr() {
        var left = EvalLogicalAnd();
        while (true) {
            SkipWhitespace();
            if (Peek() == '|' && PeekNext() == '|') {
                Read(); Read();
                if (!_skipMode && ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return 1.0;
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : (ConvertToBool(right) ? 1.0 : 0.0);
            } else if (TryMatchKeyword("or")) {
                if (!_skipMode && ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return 1.0;
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : (ConvertToBool(right) ? 1.0 : 0.0);
            } else break;
        }
        return left;
    }

    private double EvalLogicalAnd() {
        var left = EvalEquality();
        while (true) {
            SkipWhitespace();
            if (Peek() == '&' && PeekNext() == '&') {
                Read(); Read();
                if (!_skipMode && !ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return 0.0;
                }
                var right = EvalEquality();
                left = _skipMode ? default : (ConvertToBool(right) ? 1.0 : 0.0);
            } else if (TryMatchKeyword("and")) {
                if (!_skipMode && !ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return 0.0;
                }
                var right = EvalEquality();
                left = _skipMode ? default : (ConvertToBool(right) ? 1.0 : 0.0);
            } else break;
        }
        return left;
    }

    private double EvalEquality() {
        var left = EvalRelational();
        while (true) {
            SkipWhitespace();
            if (Peek() == '=' && PeekNext() == '=') {
                Read(); Read();
                var right = EvalRelational();
                left = Equal(left, right);
            } else if (Peek() == '!' && PeekNext() == '=') {
                Read(); Read();
                var right = EvalRelational();
                left = NotEqual(left, right);
            } else break;
        }
        return left;
    }

    private double EvalRelational() {
        var left = EvalBitwiseOr();
        while (true) {
            SkipWhitespace();
            if (Peek() == '<' && PeekNext() == '=') {
                Read(); Read();
                var right = EvalBitwiseOr();
                left = left <= right ? 1.0 : 0.0;
            } else if (Peek() == '>' && PeekNext() == '=') {
                Read(); Read();
                var right = EvalBitwiseOr();
                left = left >= right ? 1.0 : 0.0;
            } else if (Peek() == '<' && PeekNext() != '<') {
                Read();
                var right = EvalBitwiseOr();
                left = left < right ? 1.0 : 0.0;
            } else if (Peek() == '>' && PeekNext() != '>') {
                Read();
                var right = EvalBitwiseOr();
                left = left > right ? 1.0 : 0.0;
            } else break;
        }
        return left;
    }

    private double EvalBitwiseOr() {
        var left = EvalBitwiseXor();
        while (true) {
            SkipWhitespace();
            if (Peek() == '|' && PeekNext() != '|') {
                Read();
                var right = EvalBitwiseXor();
                left = BuiltInOperators.BitwiseOr(left, right);
            } else break;
        }
        return left;
    }

    private double EvalBitwiseXor() {
        var left = EvalBitwiseAnd();
        while (true) {
            SkipWhitespace();
            if (TryMatchKeyword("xor")) {
                var right = EvalBitwiseAnd();
                left = BuiltInOperators.BitwiseXor(left, right);
            } else break;
        }
        return left;
    }

    private double EvalBitwiseAnd() {
        var left = EvalShift();
        while (true) {
            SkipWhitespace();
            if (Peek() == '&' && PeekNext() != '&') {
                Read();
                var right = EvalShift();
                left = BuiltInOperators.BitwiseAnd(left, right);
            } else break;
        }
        return left;
    }

    private double EvalShift() {
        var left = EvalAdditive();
        while (true) {
            SkipWhitespace();
            if (Peek() == '<' && PeekNext() == '<') {
                Read(); Read();
                var right = EvalAdditive();
                left = BuiltInOperators.LeftShift(left, right);
            } else if (Peek() == '>' && PeekNext() == '>' && PeekNextNext() == '>') {
                Read(); Read(); Read();
                var right = EvalAdditive();
                left = BuiltInOperators.UnsignedRightShift(left, right);
            } else if (Peek() == '>' && PeekNext() == '>') {
                Read(); Read();
                var right = EvalAdditive();
                left = BuiltInOperators.RightShift(left, right);
            } else break;
        }
        return left;
    }

    private double EvalAdditive() {
        var left = EvalMultiplicative();
        while (true) {
            SkipWhitespace();
            if (Peek() == '+') {
                Read();
                var right = EvalMultiplicative();
                left += right;
            } else if (Peek() == '-') {
                Read();
                var right = EvalMultiplicative();
                left -= right;
            } else break;
        }
        return left;
    }

    private double EvalMultiplicative() {
        var left = EvalPower();
        while (true) {
            SkipWhitespace();
            if (Peek() == '*' && PeekNext() != '*') {
                Read();
                var right = EvalPower();
                left = _skipMode ? default : left * right;
            } else if (Peek() == '/' && PeekNext() == '/') {
                Read(); Read();
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.IntegerDivide(left, right);
            } else if (Peek() == '/') {
                Read();
                var right = EvalPower();
                left = _skipMode ? default : left / right;
            } else if (Peek() == '%') {
                Read();
                var right = EvalPower();
                left = _skipMode ? default : left % right;
            } else if (TryMatchKeyword("mod")) {
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.Modulo(left, right);
            } else break;
        }
        return left;
    }

    private double EvalPower() {
        var left = EvalUnary();
        SkipWhitespace();
        if (Peek() == '^' || (Peek() == '*' && PeekNext() == '*')) {
            Read();
            if (Peek() == '*') Read(); // 消耗第二个 *
            var right = EvalPower();
            return _skipMode ? default : BuiltInOperators.Power(left, right);
        }
        return left;
    }

    private double EvalUnary() {
        SkipWhitespace();
        if (Peek() == '+') {
            Read();
            return EvalUnary();
        }
        if (Peek() == '-') {
            Read();
            var operand = EvalUnary();
            return -operand;
        }
        if (Peek() == '!' && PeekNext() != '=') {
            Read();
            var operand = EvalUnary();
            return ConvertToBool(operand) ? 0.0 : 1.0;
        }
        if (Peek() == '~') {
            Read();
            var operand = EvalUnary();
            return BuiltInOperators.BitwiseNot(operand);
        }
        // not 关键字在 EvalPrimary 的标识符解析中处理
        return EvalPrimary();
    }

    private double EvalPrimary() {
        SkipWhitespace();
        var ch = Peek();

        if (char.IsDigit(ch) || ch == '.') return ReadNumber();

        if (ch == '(') {
            Read();
            var result = EvalExpression();
            SkipWhitespace();
            if (Peek() != ')') throw new FastEvalException("未闭合的括号", _expression, _scanner.Position);
            Read();
            return result;
        }

        if (FastScanner.IsIdentifierStart(ch)) return EvalIdentifierOrKeyword();

        throw new FastEvalException($"意外的字符 '{ch}'", _expression, _scanner.Position);
    }

    /// <summary>
    /// 统一处理标识符：关键字(not) + 函数 + 常量 + 变量
    /// and/or/xor/mod 作为中缀运算符在各自的 Eval* 方法中处理
    /// </summary>
    private double EvalIdentifierOrKeyword() {
        var span = ReadIdentifierSpan();

        // not 关键字作为前缀一元运算符
        if (span.Length == 3 && FastScanner.EqualsLower(span, "not")) {
            var operand = EvalUnary();
            return ConvertToBool(operand) ? 0.0 : 1.0;
        }

        // 函数调用
        SkipWhitespace();
        if (Peek() == '(') return EvalFunctionCall(span);

        // 常量查找（Span 直接比较，无字符串分配）
        if (BuiltInConstants.TryGetValue(span, out var constValue)) return constValue;

        // 变量查找
        return LookupVariable(span);
    }

    private double EvalFunctionCall(ReadOnlySpan<char> name) {
        Read(); // 消耗 '('

        Span<double> buffer = stackalloc double[8];
        int count = 0;
        SkipWhitespace();
        if (Peek() != ')') {
            if (count >= buffer.Length) buffer = GrowBuffer(buffer);
            buffer[count++] = EvalExpression();
            while (true) {
                SkipWhitespace();
                if (Peek() != ',') break;
                Read();
                if (count >= buffer.Length) buffer = GrowBuffer(buffer);
                buffer[count++] = EvalExpression();
            }
        }

        SkipWhitespace();
        if (Peek() != ')') throw new FastEvalException("函数调用未闭合", _expression, _scanner.Position);
        Read();

        return CallBuiltInFunction(name, buffer[..count]);
    }

    private static Span<double> GrowBuffer(Span<double> buffer) {
        var newBuffer = new double[buffer.Length * 2];
        buffer.CopyTo(newBuffer);
        return newBuffer;
    }

    private readonly double LookupVariable(ReadOnlySpan<char> name) {
        if (_skipMode) return default;
        if (_frozenVars != null && _varLookup.TryGetValue(name, out var value)) return value;
        throw new FastEvalException($"未定义的变量 '{name}'", _expression);
    }

    private static double CallBuiltInFunction(ReadOnlySpan<char> name, ReadOnlySpan<double> args) {
        if (BuiltInFunctions.TryGetFunction(name, out var func)) {
            double[] arr = args.ToArray();
            return func(arr);
        }
        throw new FastEvalException($"未知函数 '{name}'", "");
    }

    #endregion

    #region 内联运算辅助

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ConvertToBool(double value) => value != 0 && !double.IsNaN(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Equal(double left, double right) => left == right ? 1.0 : 0.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NotEqual(double left, double right) => left != right ? 1.0 : 0.0;

    #endregion

    #region Span 比较

    private static bool EqualsLower(ReadOnlySpan<char> span, string keyword)
        => FastScanner.EqualsLower(span, keyword);

    /// <summary>
    /// 尝试在当前位置匹配关键字（大小写不敏感），匹配成功则推进位置
    /// </summary>
    private bool TryMatchKeyword(string keyword) => _scanner.TryMatchKeyword(keyword);

    #endregion
}