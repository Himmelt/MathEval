using MathEval.Exceptions;

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
            if (ConvertToBool(condition)) {
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
                if (!_skipMode && ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BoolToT(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BoolToT(ConvertToBool(right));
            } else if (MatchKeyword("or")) {
                if (!_skipMode && ConvertToBool(left)) {
                    _skipMode = true;
                    EvalLogicalAnd();
                    _skipMode = false;
                    return BoolToT(true);
                }
                var right = EvalLogicalAnd();
                left = _skipMode ? default : BoolToT(ConvertToBool(right));
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
                if (!_skipMode && !ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BoolToT(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BoolToT(ConvertToBool(right));
            } else if (MatchKeyword("and")) {
                if (!_skipMode && !ConvertToBool(left)) {
                    _skipMode = true;
                    EvalEquality();
                    _skipMode = false;
                    return BoolToT(false);
                }
                var right = EvalEquality();
                left = _skipMode ? default : BoolToT(ConvertToBool(right));
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
                left = Equal(left, right);
            } else if (_scanner.Peek() == '!' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalRelational();
                left = NotEqual(left, right);
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
                left = LessThanOrEqual(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() == '=') {
                _scanner.Read(); _scanner.Read();
                var right = EvalBitwiseOr();
                left = GreaterThanOrEqual(left, right);
            } else if (_scanner.Peek() == '<' && _scanner.PeekNext() != '<') {
                _scanner.Read();
                var right = EvalBitwiseOr();
                left = LessThan(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() != '>') {
                _scanner.Read();
                var right = EvalBitwiseOr();
                left = GreaterThan(left, right);
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
                left = BitwiseOr(left, right);
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
                left = BitwiseXor(left, right);
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
                left = BitwiseAnd(left, right);
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
                left = LeftShift(left, right);
            } else if (_scanner.Peek() == '>' && _scanner.PeekNext() == '>') {
                _scanner.Read(); _scanner.Read();
                var right = EvalAdditive();
                left = RightShift(left, right);
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
                left = Add(left, right);
            } else if (_scanner.Peek() == '-') {
                _scanner.Read();
                var right = EvalMultiplicative();
                left = Subtract(left, right);
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
                left = _skipMode ? default : Multiply(left, right);
            } else if (_scanner.Peek() == '/' && _scanner.PeekNext() == '/') {
                _scanner.Read(); _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : IntegerDivide(left, right);
            } else if (_scanner.Peek() == '/') {
                _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : Divide(left, right);
            } else if (_scanner.Peek() == '%') {
                _scanner.Read();
                var right = EvalPower();
                left = _skipMode ? default : Modulo(left, right);
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
            return CastResult(Power(left, right));
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
            return Negate(operand);
        }
        if (MatchKeyword("not")) {
            var operand = EvalUnary();
            return Not(operand);
        }
        if (_scanner.Peek() == '!' && _scanner.PeekNext() != '=') {
            _scanner.Read();
            var operand = EvalUnary();
            return Not(operand);
        }
        if (_scanner.Peek() == '~') {
            _scanner.Read();
            var operand = EvalUnary();
            return BitwiseNot(operand);
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

        if (identifierSpan.SequenceEqual("true")) return BoolToT(true);
        if (identifierSpan.SequenceEqual("false")) return BoolToT(false);
        if (identifierSpan.SequenceEqual("NaN")) return DoubleToT(double.NaN);
        if (identifierSpan.SequenceEqual("INF")) return DoubleToT(double.PositiveInfinity);

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
        if (typeof(T) == typeof(double)) return DoubleToT(_scanner.ReadDouble());
        if (typeof(T) == typeof(long)) return LongToT(_scanner.ReadLong());
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
        if (name.SequenceEqual("PI")) return DoubleToT(Math.PI);
        if (name.SequenceEqual("E")) return DoubleToT(Math.E);
        if (name.SequenceEqual("π")) return DoubleToT(Math.PI);

        throw new FastEvalException($"未定义的变量 '{name.ToString()}'");
    }

    private static T CallBuiltInFunction(ReadOnlySpan<char> name, List<T> args) {
        var nameStr = name.ToString();

        if (typeof(T) == typeof(double) && BuiltInFastFunctions.TryGetDoubleFunction(nameStr, out var doubleFunc)) {
            var doubleArgs = new double[args.Count];
            for (int i = 0; i < args.Count; i++) doubleArgs[i] = Convert.ToDouble(args[i]);
            return DoubleToT(doubleFunc(doubleArgs));
        }

        if (typeof(T) == typeof(long) && BuiltInFastFunctions.TryGetLongFunction(nameStr, out var longFunc)) {
            var longArgs = new long[args.Count];
            for (int i = 0; i < args.Count; i++) longArgs[i] = Convert.ToInt64(args[i]);
            return LongToT(longFunc(longArgs));
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

    #region 类型转换

    private static bool ConvertToBool(T value) {
        if (typeof(T) == typeof(bool)) return (bool)(object)value;
        if (typeof(T) == typeof(long)) return (long)(object)value != 0;
        if (typeof(T) == typeof(double)) return (double)(object)value != 0 && !double.IsNaN((double)(object)value);
        throw new FastEvalException("无法转换为布尔类型");
    }

    private static T BoolToT(bool value) {
        if (typeof(T) == typeof(bool)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(value ? 1L : 0L);
        if (typeof(T) == typeof(double)) return (T)(object)(value ? 1.0 : 0.0);
        throw new FastEvalException("无法从布尔类型转换");
    }

    private static T DoubleToT(double value) {
        if (typeof(T) == typeof(double)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(long)value;
        throw new FastEvalException("无法从 double 类型转换");
    }

    private static T LongToT(long value) {
        if (typeof(T) == typeof(long)) return (T)(object)value;
        if (typeof(T) == typeof(double)) return (T)(object)(double)value;
        throw new FastEvalException("无法从 long 类型转换");
    }

    private static double ToDouble(T value) {
        if (typeof(T) == typeof(double)) return (double)(object)value;
        if (typeof(T) == typeof(long)) return (long)(object)value;
        if (typeof(T) == typeof(bool)) return (bool)(object)value ? 1.0 : 0.0;
        return Convert.ToDouble(value);
    }

    private static long ToLong(T value) {
        if (typeof(T) == typeof(long)) return (long)(object)value;
        if (typeof(T) == typeof(double)) return (long)(double)(object)value;
        if (typeof(T) == typeof(bool)) return (bool)(object)value ? 1L : 0L;
        return Convert.ToInt64(value);
    }

    #endregion

    #region 算术运算

    private static T Add(T left, T right) {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left + (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left + (long)(object)right);
        throw new FastEvalException("加法运算需要数值类型");
    }

    private static T Subtract(T left, T right) {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left - (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left - (long)(object)right);
        throw new FastEvalException("减法运算需要数值类型");
    }

    private static T Multiply(T left, T right) {
        if (typeof(T) == typeof(double)) return (T)(object)((double)(object)left * (double)(object)right);
        if (typeof(T) == typeof(long)) return (T)(object)checked((long)(object)left * (long)(object)right);
        throw new FastEvalException("乘法运算需要数值类型");
    }

    private static T Divide(T left, T right) {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)((double)(object)left / d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)(long)((double)(long)(object)left / r);
        }
        throw new FastEvalException("除法运算需要数值类型");
    }

    private static T IntegerDivide(T left, T right) {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)Math.Truncate((double)(object)left / d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)((long)(object)left / r);
        }
        throw new FastEvalException("整除运算需要数值类型");
    }

    private static T Modulo(T left, T right) {
        if (typeof(T) == typeof(double)) {
            var d = (double)(object)right;
            if (d == 0) throw new DivisionByZeroException();
            return (T)(object)((double)(object)left % d);
        }
        if (typeof(T) == typeof(long)) {
            var r = (long)(object)right;
            if (r == 0) throw new DivisionByZeroException();
            return (T)(object)((long)(object)left % r);
        }
        throw new FastEvalException("取模运算需要数值类型");
    }

    private static double Power(T left, T right) {
        double d1, d2;
        var isLong = false;
        if (typeof(T) == typeof(double)) {
            d1 = (double)(object)left;
            d2 = (double)(object)right;
        } else {
            d1 = (long)(object)left;
            d2 = (long)(object)right;
            isLong = true;
        }
        if (d1 < 0 && d2 != Math.Floor(d2))
            throw new FastEvalException("不能对负数求非整数次幂");
        if (isLong && d1 == 0 && d2 < 0)
            throw new FastEvalException("零不能求负数次幂");
        return Math.Pow(d1, d2);
    }

    private static T CastResult(double value) {
        if (typeof(T) == typeof(double)) return (T)(object)value;
        if (typeof(T) == typeof(long)) return (T)(object)(long)value;
        throw new FastEvalException("幂运算结果类型不匹配");
    }

    private static T Negate(T operand) {
        if (typeof(T) == typeof(double)) return (T)(object)(-(double)(object)operand);
        if (typeof(T) == typeof(long)) return (T)(object)checked(-(long)(object)operand);
        throw new FastEvalException("取负运算需要数值类型");
    }

    private static T Not(T operand) {
        if (typeof(T) == typeof(bool)) return (T)(object)(!(bool)(object)operand);
        if (typeof(T) == typeof(double)) return (T)(object)(ConvertToBool(operand) ? 0.0 : 1.0);
        if (typeof(T) == typeof(long)) return (T)(object)(ConvertToBool(operand) ? 0L : 1L);
        throw new FastEvalException("逻辑非运算需要布尔或数值类型");
    }

    private static T BitwiseNot(T operand) {
        return (T)(object)(~ToLong(operand));
    }

    private static T BitwiseOr(T left, T right) {
        return LongToT(ToLong(left) | ToLong(right));
    }

    private static T BitwiseAnd(T left, T right) {
        return LongToT(ToLong(left) & ToLong(right));
    }

    private static T BitwiseXor(T left, T right) {
        return LongToT(ToLong(left) ^ ToLong(right));
    }

    private static T LeftShift(T left, T right) {
        var l1 = ToLong(left);
        var l2 = ToLong(right);
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return LongToT(l1 << (int)l2);
    }

    private static T RightShift(T left, T right) {
        var l1 = ToLong(left);
        var l2 = ToLong(right);
        if (l2 < 0) throw new FastEvalException("移位量不能为负数");
        if (l2 >= 64) l2 %= 64;
        return LongToT(l1 >> (int)l2);
    }

    #endregion

    #region 比较运算

    private static T Equal(T left, T right) {
        if (typeof(T) == typeof(double)) {
            var d1 = (double)(object)left;
            var d2 = (double)(object)right;
            if (double.IsNaN(d1) || double.IsNaN(d2)) return BoolToT(false);
            return BoolToT(d1 == d2);
        }
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left == (long)(object)right);
        return BoolToT(Equals(left, right));
    }

    private static T NotEqual(T left, T right) {
        if (typeof(T) == typeof(double)) {
            var d1 = (double)(object)left;
            var d2 = (double)(object)right;
            if (double.IsNaN(d1) || double.IsNaN(d2)) return BoolToT(true);
            return BoolToT(d1 != d2);
        }
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left != (long)(object)right);
        return BoolToT(!Equals(left, right));
    }

    private static T LessThan(T left, T right) {
        if (typeof(T) == typeof(double)) return BoolToT((double)(object)left < (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left < (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    private static T LessThanOrEqual(T left, T right) {
        if (typeof(T) == typeof(double)) return BoolToT((double)(object)left <= (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left <= (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    private static T GreaterThan(T left, T right) {
        if (typeof(T) == typeof(double)) return BoolToT((double)(object)left > (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left > (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    private static T GreaterThanOrEqual(T left, T right) {
        if (typeof(T) == typeof(double)) return BoolToT((double)(object)left >= (double)(object)right);
        if (typeof(T) == typeof(long)) return BoolToT((long)(object)left >= (long)(object)right);
        throw new FastEvalException("比较运算需要数值类型");
    }

    #endregion
}