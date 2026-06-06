using MathEval.Fast.BuiltIn;
using MathEval.Fast.Exceptions;

namespace MathEval.Fast.Core;

/// <summary>
/// 递归求值器，边扫描边求值，零 AST 中间层
/// <br/>
/// 内部统一使用 double 运算，仅在最终返回时按需转换类型
/// </summary>
internal sealed class FastEvaluator(string expression, IReadOnlyDictionary<string, double>? variables = null) {

    private bool _skipMode;
    private readonly FastScanner _scanner = new(expression);

    public double Evaluate() {
        _scanner.SkipWhitespace();
        if (_scanner.IsAtEnd) throw new FastEvalException("表达式不能为空", expression);

        var result = EvalExpression();

        _scanner.SkipWhitespace();
        if (!_scanner.IsAtEnd) throw new FastEvalException($"意外的字符 '{_scanner.Peek()}'，位置 {_scanner.Position}", expression);

        return result;
    }

    private double EvalExpression() => EvalConditional();

    private double EvalConditional() {
        var condition = EvalLogicalOr();
        _scanner.SkipWhitespace();

        if (_scanner.Peek() == '?') {
            _scanner.Read();
            if (BuiltInOperators.ConvertToBool(condition)) {
                var trueValue = EvalExpression();
                _scanner.SkipWhitespace();
                if (_scanner.Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'", expression);
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
                if (_scanner.Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'", expression);
                _scanner.Read();
                var falseValue = EvalExpression();
                return falseValue;
            }
        }

        return condition;
    }

    private double EvalLogicalOr() {
        var left = EvalLogicalAnd();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '|' && _scanner.PeekNext() == '|') {
                _scanner.Read(); _scanner.Read();
                if (!_skipMode && BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BuiltInOperators.BoolToDouble(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BuiltInOperators.BoolToDouble(BuiltInOperators.ConvertToBool(right));
            } else if (MatchKeyword("or")) {
                if (!_skipMode && BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BuiltInOperators.BoolToDouble(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BuiltInOperators.BoolToDouble(BuiltInOperators.ConvertToBool(right));
            } else break;
        }
        return left;
    }

    private double EvalLogicalAnd() {
        var left = EvalEquality();
        while (true) {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() == '&' && _scanner.PeekNext() == '&') {
                _scanner.Read(); _scanner.Read();
                if (!_skipMode && !BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BuiltInOperators.BoolToDouble(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BuiltInOperators.BoolToDouble(BuiltInOperators.ConvertToBool(right));
            } else if (MatchKeyword("and")) {
                if (!_skipMode && !BuiltInOperators.ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BuiltInOperators.BoolToDouble(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BuiltInOperators.BoolToDouble(BuiltInOperators.ConvertToBool(right));
            } else break;
        }
        return left;
    }

    private double EvalEquality() {
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

    private double EvalRelational() {
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

    private double EvalBitwiseOr() {
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

    private double EvalBitwiseXor() {
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

    private double EvalBitwiseAnd() {
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

    private double EvalShift() {
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

    private double EvalAdditive() {
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

    private double EvalMultiplicative() {
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

    private double EvalPower() {
        var left = EvalUnary();
        _scanner.SkipWhitespace();
        if (_scanner.Peek() == '^') {
            _scanner.Read();
            var right = EvalPower();
            return _skipMode ? default : BuiltInOperators.Power(left, right);
        }
        return left;
    }

    private double EvalUnary() {
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

    private double EvalPrimary() {
        _scanner.SkipWhitespace();
        var ch = _scanner.Peek();

        if (char.IsDigit(ch) || ch == '.') return _scanner.ReadNumber();

        if (ch == '(') {
            _scanner.Read();
            var result = EvalExpression();
            _scanner.SkipWhitespace();
            if (_scanner.Peek() != ')') throw new FastEvalException("未闭合的括号", expression, _scanner.Position);
            _scanner.Read();
            return result;
        }

        if (FastScanner.IsIdentifierStart(ch)) return EvalIdentifierOrFunction();

        throw new FastEvalException($"意外的字符 '{ch}'", expression, _scanner.Position);
    }

    private double EvalIdentifierOrFunction() {
        var identifierSpan = _scanner.ReadIdentifierSpan();
        _scanner.SkipWhitespace();

        if (_scanner.Peek() == '(') return EvalFunctionCall(identifierSpan.ToString());

        if (BuiltInConstants.TryGetValue(identifierSpan.ToString(), out var constValue)) return constValue;

        return LookupVariable(identifierSpan.ToString());
    }

    private double EvalFunctionCall(string name) {
        _scanner.Read();

        var args = new List<double>();
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
        if (_scanner.Peek() != ')') throw new FastEvalException("函数调用未闭合", expression, _scanner.Position);
        _scanner.Read();

        return CallBuiltInFunction(name, args);
    }

    private double LookupVariable(string name) {
        if (_skipMode) return default;
        if (variables != null && variables.TryGetValue(name, out var value)) return value;
        throw new FastEvalException($"未定义的变量 '{name}'", expression);
    }

    private double CallBuiltInFunction(string name, List<double> args) {
        if (BuiltInFunctions.TryGetFunction(name, out var func)) return func([.. args]);
        throw new FastEvalException($"未知函数 '{name}'", expression);
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