using MathEval.Exceptions;
using MathEval.Operators;

namespace MathEval.Fast;

/// <summary>
/// 泛型递归求值器，边扫描边求值，零 AST 中间层
/// </summary>
internal sealed class FastEvaluator<T> where T : struct {

    private int _depth;
    private bool _skipMode;
    private const int MaxDepth = 1024;
    private FastScanner _scanner;
    private readonly IReadOnlyDictionary<string, T>? _variables;

    public FastEvaluator(string expression, IReadOnlyDictionary<string, T>? variables = null) {
        if (string.IsNullOrEmpty(expression)) throw new FastEvalException("表达式不能为空");
        if (expression.Length > 4096) throw new FastEvalException("表达式长度超过最大限制 4096 个字符");

        _scanner = new FastScanner(expression);
        _variables = variables;
        _depth = 0;
    }

    public T Evaluate() {
        _scanner.SkipWhitespace();
        if (_scanner.IsAtEnd) throw new FastEvalException("表达式不能为空");

        var result = EvalExpression();

        _scanner.SkipWhitespace();
        if (!_scanner.IsAtEnd) throw new FastEvalException($"意外的字符 '{_scanner.Peek()}'，位置 {_scanner.Position}");

        return result;
    }

    private T EvalExpression() => EvalConditional();

    private T EvalConditional() {
        var condition = EvalLogicalOr();
        _scanner.SkipWhitespace();

        if (_scanner.Peek() == '?') {
            _scanner.Read();
            if (BuiltInOperators.ConvertToBool(condition)) {
                var trueValue = EvalExpression();
                _scanner.SkipWhitespace();
                if (_scanner.Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'");
                _scanner.Read();
                _skipMode = true;
                EvalExpression();
                _skipMode = false;
                return trueValue;
            } else {
                _skipMode = true;
                EvalExpression();
                _skipMode = false;
                _scanner.SkipWhitespace();
                if (_scanner.Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'");
                _scanner.Read();
                var falseValue = EvalExpression();
                return falseValue;
            }
        }

        return condition;
    }

    private T EvalLogicalOr() {
        var left = EvalLogicalAnd();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '|' && _scanner.PeekNext() == '|') {
                _scanner.Read(); _scanner.Read();
                if (!_skipMode && BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BuiltInOperators.BoolToT<T>(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BuiltInOperators.BoolToT<T>(BuiltInOperators.ConvertToBool(right));
            } else if (MatchKeyword("or")) {
                if (!_skipMode && BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BuiltInOperators.BoolToT<T>(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BuiltInOperators.BoolToT<T>(BuiltInOperators.ConvertToBool(right));
            } else break;
        }
        return left;
    }

    private T EvalLogicalAnd() {
        var left = EvalEquality();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '&' && _scanner.PeekNext() == '&') {
                _scanner.Read(); _scanner.Read();
                if (!_skipMode && !BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BuiltInOperators.BoolToT<T>(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BuiltInOperators.BoolToT<T>(BuiltInOperators.ConvertToBool(right));
            } else if (MatchKeyword("and")) {
                if (!_skipMode && !BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BuiltInOperators.BoolToT<T>(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BuiltInOperators.BoolToT<T>(BuiltInOperators.ConvertToBool(right));
            } else break;
        }
        return left;
    }

    private T EvalEquality() {
        var left = EvalRelational();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '=' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalRelational();
                left = BuiltInOperators.Equal(left, right);
            } else if (_scanner.Peek() == '!' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalRelational();
                left = BuiltInOperators.NotEqual(left, right);
            } else break;
        }
        return left;
    }

    private T EvalRelational() {
        var left = EvalBitwiseOr();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '<' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalBitwiseOr();
                left = BuiltInOperators.LessThanOrEqual(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalBitwiseOr();
                left = BuiltInOperators.GreaterThanOrEqual(left, right);
            } else if (_scanner.Peek() == '<' && _scanner.PeekNext() != '<') {
                _scanner.Read();
                var right = EvalBitwiseOr();
                left = BuiltInOperators.LessThan(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() != '>') {
                _scanner.Read();
                var right = EvalBitwiseOr();
                left = BuiltInOperators.GreaterThan(left, right);
            } else break;
        }
        return left;
    }

    private T EvalBitwiseOr() {
        var left = EvalBitwiseXor();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '|' && _scanner.PeekNext() != '|') {
                _scanner.Read();
                var right = EvalBitwiseXor();
                left = BuiltInOperators.BitwiseOr(left, right);
            } else break;
        }
        return left;
    }

    private T EvalBitwiseXor() {
        var left = EvalBitwiseAnd();
        while (true) {
            _scanner.SkipWhitespace();
            if (MatchKeyword("xor")) {
                var right = EvalBitwiseAnd();
                left = BuiltInOperators.BitwiseXor(left, right);
            } else break;
        }
        return left;
    }

    private T EvalBitwiseAnd() {
        var left = EvalShift();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '&' && _scanner.PeekNext() != '&') {
                _scanner.Read();
                var right = EvalShift();
                left = BuiltInOperators.BitwiseAnd(left, right);
            } else break;
        }
        return left;
    }

    private T EvalShift() {
        var left = EvalAdditive();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '<' && _scanner.PeekNext() == '<') {
                _scanner.Read(); _scanner.Read();
                var right = EvalAdditive();
                left = BuiltInOperators.LeftShift(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() == '>') {
                _scanner.Read(); _scanner.Read();
                var right = EvalAdditive();
                left = BuiltInOperators.RightShift(left, right);
            } else break;
        }
        return left;
    }

    private T EvalAdditive() {
        var left = EvalMultiplicative();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '+') {
                _scanner.Read();
                var right = EvalMultiplicative();
                left = BuiltInOperators.Add(left, right);
            } else if (_scanner.Peek() == '-') {
                _scanner.Read();
                var right = EvalMultiplicative();
                left = BuiltInOperators.Subtract(left, right);
            } else break;
        }
        return left;
    }

    private T EvalMultiplicative() {
        var left = EvalPower();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '*') {
                _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.Multiply(left, right);
            } else if (_scanner.Peek() == '/' && _scanner.PeekNext() == '/') {
                _scanner.Read(); _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.IntegerDivide(left, right);
            } else if (_scanner.Peek() == '/') {
                _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.Divide(left, right);
            } else if (_scanner.Peek() == '%') {
                _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : BuiltInOperators.Modulo(left, right);
            } else break;
        }
        return left;
    }

    private T EvalPower() {
        var left = EvalUnary();
        _scanner.SkipWhitespace();
        if (_scanner.Peek() == '^') {
            _scanner.Read();
            var right = EvalPower();
            return BuiltInOperators.CastPowerResult<T>(BuiltInOperators.Power(left, right));
        }
        return left;
    }

    private T EvalUnary() {
        _scanner.SkipWhitespace();
        if (_scanner.Peek() == '+') {
            _scanner.Read();
            return EvalUnary();
        }
        if (_scanner.Peek() == '-') {
            _scanner.Read();
            var operand = EvalUnary();
            return BuiltInOperators.Negate(operand);
        }
        if (MatchKeyword("not")) {
            var operand = EvalUnary();
            return BuiltInOperators.Not(operand);
        }
        if (_scanner.Peek() == '!' && _scanner.PeekNext() != '=') {
            _scanner.Read();
            var operand = EvalUnary();
            return BuiltInOperators.Not(operand);
        }
        if (_scanner.Peek() == '~') {
            _scanner.Read();
            var operand = EvalUnary();
            return BuiltInOperators.BitwiseNot(operand);
        }
        return EvalPrimary();
    }

    private T EvalPrimary() {
        _scanner.SkipWhitespace();
        var ch = _scanner.Peek();

        if (char.IsDigit(ch) || ch == '.') {
            return ReadNumber();
        }

        if (ch == '(') {
            if (++_depth > MaxDepth) throw new FastEvalException("表达式嵌套深度超过最大限制");
            _scanner.Read();
            var result = EvalExpression();
            _scanner.SkipWhitespace();
            if (_scanner.Peek() != ')') throw new FastEvalException("未闭合的括号", _scanner.Position);
            _scanner.Read();
            _depth--;
            return result;
        }

        if (FastScanner.IsIdentifierStart(ch)) {
            return EvalIdentifierOrFunction();
        }

        throw new FastEvalException($"意外的字符 '{ch}'", _scanner.Position);
    }

    private T EvalIdentifierOrFunction() {
        var identifierSpan = _scanner.ReadIdentifierSpan();
        _scanner.SkipWhitespace();

        if (_scanner.Peek() == '(') {
            return EvalFunctionCall(identifierSpan);
        }

        if (identifierSpan.SequenceEqual("true")) return BuiltInOperators.BoolToT<T>(true);
        if (identifierSpan.SequenceEqual("false")) return BuiltInOperators.BoolToT<T>(false);
        if (identifierSpan.SequenceEqual("NaN")) return BuiltInOperators.DoubleToT<T>(double.NaN);
        if (identifierSpan.SequenceEqual("INF")) return BuiltInOperators.DoubleToT<T>(double.PositiveInfinity);

        return LookupVariable(identifierSpan);
    }

    private T EvalFunctionCall(ReadOnlySpan<char> name) {
        _scanner.Read();

        var args = new List<T>();
        _scanner.SkipWhitespace();
        if (_scanner.Peek() != ')') {
            args.Add(EvalExpression());
            while (true) {
                _scanner.SkipWhitespace();
                if (_scanner.Peek() != ',') break;
                _scanner.Read();
                args.Add(EvalExpression());
            }
        }

        _scanner.SkipWhitespace();
        if (_scanner.Peek() != ')') throw new FastEvalException("函数调用未闭合", _scanner.Position);
        _scanner.Read();

        return FastEvaluator<T>.CallBuiltInFunction(name, args);
    }

    private T ReadNumber() {
        if (typeof(T) == typeof(double)) return BuiltInOperators.DoubleToT<T>(_scanner.ReadDouble());
        if (typeof(T) == typeof(long)) return BuiltInOperators.LongToT<T>(_scanner.ReadLong());
        throw new FastEvalException($"不支持的数值类型: {typeof(T).Name}");
    }

    private T LookupVariable(ReadOnlySpan<char> name) {
        if (_skipMode) return default;
        if (_variables != null) {
            foreach (var kv in _variables) {
                if (name.SequenceEqual(kv.Key)) return kv.Value;
            }
        }

        // 内置常量回退
        if (name.SequenceEqual("PI")) return BuiltInOperators.DoubleToT<T>(Math.PI);
        if (name.SequenceEqual("E")) return BuiltInOperators.DoubleToT<T>(Math.E);
        if (name.SequenceEqual("π")) return BuiltInOperators.DoubleToT<T>(Math.PI);

        throw new FastEvalException($"未定义的变量 '{name.ToString()}'");
    }

    private static T CallBuiltInFunction(ReadOnlySpan<char> name, List<T> args) {
        var nameStr = name.ToString();

        if (typeof(T) == typeof(double) && BuiltInFastFunctions.TryGetDoubleFunction(nameStr, out var doubleFunc)) {
            var doubleArgs = new double[args.Count];
            for (int i = 0; i < args.Count; i++) doubleArgs[i] = Convert.ToDouble(args[i]);
            return BuiltInOperators.DoubleToT<T>(doubleFunc(doubleArgs));
        }

        if (typeof(T) == typeof(long) && BuiltInFastFunctions.TryGetLongFunction(nameStr, out var longFunc)) {
            var longArgs = new long[args.Count];
            for (int i = 0; i < args.Count; i++) longArgs[i] = Convert.ToInt64(args[i]);
            return BuiltInOperators.LongToT<T>(longFunc(longArgs));
        }

        throw new FastEvalException($"未知函数 '{nameStr}'");
    }

    private bool MatchKeyword(string keyword) {
        _scanner.SkipWhitespace();
        var pos = _scanner.Position;
        var text = _scanner.Text;

        if (pos + keyword.Length > text.Length) return false;

        for (int i = 0; i < keyword.Length; i++) {
            if (char.ToLowerInvariant(text[pos + i]) != char.ToLowerInvariant(keyword[i])) return false;
        }

        if (pos + keyword.Length < text.Length && FastScanner.IsIdentifierPart(text[pos + keyword.Length])) return false;

        _scanner.Advance(keyword.Length);
        return true;
    }

}