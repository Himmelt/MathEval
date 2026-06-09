using MathEval.Fast.BuiltIn;
using MathEval.Fast.Core;
using MathEval.Fast.Exceptions;

namespace MathEval.Fast.VM;

/// <summary>
/// 将表达式字符串编译为指令数组
/// <br/>
/// 复用 FastEvaluator 的扫描逻辑，但输出指令而非直接求值
/// 短路逻辑使用 JumpIfFalse + 回填实现
/// </summary>
internal class BytecodeCompiler(string expression) {

    private FastScanner _scanner = new(expression);
    private readonly List<Instruction> _instructions = [];
    private readonly string _expression = expression ?? throw new FastEvalException("表达式不能为 null");

    public Instruction[] Compile() {
        SkipWhitespace();
        if (IsAtEnd) throw new FastEvalException("表达式不能为空", _expression);

        CompileExpression();

        SkipWhitespace();
        if (!IsAtEnd) throw new FastEvalException($"意外的字符 '{Peek()}'，位置 {_scanner.Position}", _expression);

        return [.. _instructions];
    }

    #region Emit 辅助

    private void Emit(Instruction instruction) => _instructions.Add(instruction);

    private int EmitJumpPlaceholder(OpCode opCode) {
        var idx = _instructions.Count;
        _instructions.Add(new Instruction { OpCode = opCode, IntOperand = 0 });
        return idx;
    }

    private void PatchJump(int instructionIndex, int target) {
        var instr = _instructions[instructionIndex];
        instr.IntOperand = target;
        _instructions[instructionIndex] = instr;
    }

    #endregion

    #region Scanner（委托给 FastScanner）

    private bool IsAtEnd => _scanner.IsAtEnd;
    private char Peek() => _scanner.Peek();
    private char PeekNext() => _scanner.PeekNext();
    private char PeekNextNext() => _scanner.PeekNextNext();
    private char Read() => _scanner.Read();
    private void SkipWhitespace() => _scanner.SkipWhitespace();
    private ReadOnlySpan<char> ReadIdentifierSpan() => _scanner.ReadIdentifierSpan();
    private double ReadNumber() => _scanner.ReadNumber();

    #endregion

    #region 编译方法

    private void CompileExpression() => CompileConditional();

    private void CompileConditional() {
        CompileLogicalOr();
        SkipWhitespace();

        if (Peek() == '?') {
            Read();
            // condition is on stack
            var elseJump = EmitJumpPlaceholder(OpCode.JumpIfFalse);
            CompileExpression(); // true branch
            var endJump = EmitJumpPlaceholder(OpCode.Jump);
            PatchJump(elseJump, _instructions.Count);
            SkipWhitespace();
            if (Peek() != ':') throw new FastEvalException("三元运算符缺少 ':'", _expression);
            Read();
            CompileExpression(); // false branch
            PatchJump(endJump, _instructions.Count);
        }
    }

    private void CompileLogicalOr() {
        CompileLogicalAnd();
        while (true) {
            SkipWhitespace();
            if (Peek() == '|' && PeekNext() == '|') {
                Read(); Read();
                CompileLogicalOrRight();
            } else if (TryMatchKeyword("or")) {
                CompileLogicalOrRight();
            } else break;
        }
    }

    private void CompileLogicalOrRight() {
        // left is on stack; if truthy, short-circuit to 1.0
        var evalRightJump = EmitJumpPlaceholder(OpCode.JumpIfFalse);
        Emit(Instruction.Push(1.0));
        var endJump = EmitJumpPlaceholder(OpCode.Jump);
        PatchJump(evalRightJump, _instructions.Count);
        CompileLogicalAnd();
        // Convert right to bool: !!right
        Emit(Instruction.Op(OpCode.LogicalNot));
        Emit(Instruction.Op(OpCode.LogicalNot));
        PatchJump(endJump, _instructions.Count);
    }

    private void CompileLogicalAnd() {
        CompileEquality();
        while (true) {
            SkipWhitespace();
            if (Peek() == '&' && PeekNext() == '&') {
                Read(); Read();
                CompileLogicalAndRight();
            } else if (TryMatchKeyword("and")) {
                CompileLogicalAndRight();
            } else break;
        }
    }

    private void CompileLogicalAndRight() {
        // left is on stack; if falsy, short-circuit to 0.0
        var falseJump = EmitJumpPlaceholder(OpCode.JumpIfFalse);
        CompileEquality();
        // Convert right to bool: !!right
        Emit(Instruction.Op(OpCode.LogicalNot));
        Emit(Instruction.Op(OpCode.LogicalNot));
        var endJump = EmitJumpPlaceholder(OpCode.Jump);
        PatchJump(falseJump, _instructions.Count);
        Emit(Instruction.Push(0.0));
        PatchJump(endJump, _instructions.Count);
    }

    private void CompileEquality() {
        CompileRelational();
        while (true) {
            SkipWhitespace();
            if (Peek() == '=' && PeekNext() == '=') {
                Read(); Read();
                CompileRelational();
                Emit(Instruction.Op(OpCode.Equal));
            } else if (Peek() == '!' && PeekNext() == '=') {
                Read(); Read();
                CompileRelational();
                Emit(Instruction.Op(OpCode.NotEqual));
            } else break;
        }
    }

    private void CompileRelational() {
        CompileBitwiseOr();
        while (true) {
            SkipWhitespace();
            if (Peek() == '<' && PeekNext() == '=') {
                Read(); Read();
                CompileBitwiseOr();
                Emit(Instruction.Op(OpCode.LessOrEqual));
            } else if (Peek() == '>' && PeekNext() == '=') {
                Read(); Read();
                CompileBitwiseOr();
                Emit(Instruction.Op(OpCode.GreaterOrEqual));
            } else if (Peek() == '<' && PeekNext() != '<') {
                Read();
                CompileBitwiseOr();
                Emit(Instruction.Op(OpCode.LessThan));
            } else if (Peek() == '>' && PeekNext() != '>') {
                Read();
                CompileBitwiseOr();
                Emit(Instruction.Op(OpCode.GreaterThan));
            } else break;
        }
    }

    private void CompileBitwiseOr() {
        CompileBitwiseXor();
        while (true) {
            SkipWhitespace();
            if (Peek() == '|' && PeekNext() != '|') {
                Read();
                CompileBitwiseXor();
                Emit(Instruction.Op(OpCode.BitwiseOr));
            } else break;
        }
    }

    private void CompileBitwiseXor() {
        CompileBitwiseAnd();
        while (true) {
            SkipWhitespace();
            if (TryMatchKeyword("xor")) {
                CompileBitwiseAnd();
                Emit(Instruction.Op(OpCode.BitwiseXor));
            } else break;
        }
    }

    private void CompileBitwiseAnd() {
        CompileShift();
        while (true) {
            SkipWhitespace();
            if (Peek() == '&' && PeekNext() != '&') {
                Read();
                CompileShift();
                Emit(Instruction.Op(OpCode.BitwiseAnd));
            } else break;
        }
    }

    private void CompileShift() {
        CompileAdditive();
        while (true) {
            SkipWhitespace();
            if (Peek() == '<' && PeekNext() == '<') {
                Read(); Read();
                CompileAdditive();
                Emit(Instruction.Op(OpCode.LeftShift));
            } else if (Peek() == '>' && PeekNext() == '>' && PeekNextNext() == '>') {
                Read(); Read(); Read();
                CompileAdditive();
                Emit(Instruction.Op(OpCode.UnsignedRightShift));
            } else if (Peek() == '>' && PeekNext() == '>') {
                Read(); Read();
                CompileAdditive();
                Emit(Instruction.Op(OpCode.RightShift));
            } else break;
        }
    }

    private void CompileAdditive() {
        CompileMultiplicative();
        while (true) {
            SkipWhitespace();
            if (Peek() == '+') {
                Read();
                CompileMultiplicative();
                Emit(Instruction.Op(OpCode.Add));
            } else if (Peek() == '-') {
                Read();
                CompileMultiplicative();
                Emit(Instruction.Op(OpCode.Sub));
            } else break;
        }
    }

    private void CompileMultiplicative() {
        CompilePower();
        while (true) {
            SkipWhitespace();
            if (Peek() == '*' && PeekNext() != '*') {
                Read();
                CompilePower();
                Emit(Instruction.Op(OpCode.Mul));
            } else if (Peek() == '/' && PeekNext() == '/') {
                Read(); Read();
                CompilePower();
                Emit(Instruction.Op(OpCode.IntDiv));
            } else if (Peek() == '/') {
                Read();
                CompilePower();
                Emit(Instruction.Op(OpCode.Div));
            } else if (Peek() == '%') {
                Read();
                CompilePower();
                Emit(Instruction.Op(OpCode.Mod));
            } else if (TryMatchKeyword("mod")) {
                CompilePower();
                Emit(Instruction.Op(OpCode.MathMod));
            } else break;
        }
    }

    private void CompilePower() {
        CompileUnary();
        SkipWhitespace();
        if (Peek() == '^' || (Peek() == '*' && PeekNext() == '*')) {
            Read();
            if (Peek() == '*') Read(); // 消耗第二个 *
            CompilePower(); // right-associative: recurse
            Emit(Instruction.Op(OpCode.Pow));
        }
    }

    private void CompileUnary() {
        SkipWhitespace();
        if (Peek() == '+') {
            Read();
            CompileUnary();
            return;
        }
        if (Peek() == '-') {
            Read();
            CompileUnary();
            Emit(Instruction.Op(OpCode.Negate));
            return;
        }
        if (Peek() == '!' && PeekNext() != '=') {
            Read();
            CompileUnary();
            Emit(Instruction.Op(OpCode.LogicalNot));
            return;
        }
        if (Peek() == '~') {
            Read();
            CompileUnary();
            Emit(Instruction.Op(OpCode.BitwiseNot));
            return;
        }
        // not 关键字在 CompilePrimary 的标识符解析中处理
        CompilePrimary();
    }

    private void CompilePrimary() {
        SkipWhitespace();
        var ch = Peek();

        if (char.IsDigit(ch) || ch == '.') {
            var value = ReadNumber();
            Emit(Instruction.Push(value));
            return;
        }

        if (ch == '(') {
            Read();
            CompileExpression();
            SkipWhitespace();
            if (Peek() != ')') throw new FastEvalException("未闭合的括号", _expression, _scanner.Position);
            Read();
            return;
        }

        if (FastScanner.IsIdentifierStart(ch)) {
            CompileIdentifierOrKeyword();
            return;
        }

        throw new FastEvalException($"意外的字符 '{ch}'", _expression, _scanner.Position);
    }

    /// <summary>
    /// 统一处理标识符：关键字(not) + 函数 + 常量 + 变量
    /// and/or/xor/mod 作为中缀运算符在各自的 Compile* 方法中处理
    /// </summary>
    private void CompileIdentifierOrKeyword() {
        var span = ReadIdentifierSpan();

        // not 关键字作为前缀一元运算符
        if (span.Length == 3 && FastScanner.EqualsLower(span, "not")) {
            CompileUnary();
            Emit(Instruction.Op(OpCode.LogicalNot));
            return;
        }

        // 函数调用
        SkipWhitespace();
        if (Peek() == '(') {
            CompileFunctionCall(span);
            return;
        }

        // 常量查找
        if (BuiltInConstants.TryGetValue(span, out var constValue)) {
            Emit(Instruction.Push(constValue));
            return;
        }

        // 变量加载
        Emit(Instruction.LoadVar(span.ToString()));
    }

    private void CompileFunctionCall(ReadOnlySpan<char> name) {
        Read(); // 消耗 '('

        int argCount = 0;
        SkipWhitespace();
        if (Peek() != ')') {
            CompileExpression();
            argCount++;
            while (true) {
                SkipWhitespace();
                if (Peek() != ',') break;
                Read();
                CompileExpression();
                argCount++;
            }
        }

        SkipWhitespace();
        if (Peek() != ')') throw new FastEvalException("函数调用未闭合", _expression, _scanner.Position);
        Read();

        // 查找函数 ID
        if (!FunctionTable.TryGetId(name, out var funcId)) {
            throw new FastEvalException($"未知函数 '{name}'", _expression);
        }

        Emit(Instruction.CallFunc(funcId, argCount));
    }

    #endregion

    #region Span 比较

    private static bool EqualsLower(ReadOnlySpan<char> span, string keyword)
        => FastScanner.EqualsLower(span, keyword);

    /// <summary>
    /// 尝试在当前位置匹配关键字（大小写不敏感），匹配成功则推进位置
    /// </summary>
    private bool TryMatchKeyword(string keyword) => _scanner.TryMatchKeyword(keyword);

    #endregion

    #region 函数 ID 映射表

    /// <summary>
    /// 内置函数名称到 byte ID 的映射，供编译器和 VM 共用
    /// </summary>
    internal static class FunctionTable {
        private static readonly Dictionary<string, byte> _nameToId = new(StringComparer.OrdinalIgnoreCase) {
            ["sin"] = 0,
            ["cos"] = 1,
            ["tan"] = 2,
            ["asin"] = 3,
            ["acos"] = 4,
            ["atan"] = 5,
            ["atan2"] = 6,
            ["exp"] = 7,
            ["pow"] = 8,
            ["ln"] = 9,
            ["lg"] = 10,
            ["log"] = 11,
            ["log2"] = 12,
            ["log10"] = 13,
            ["abs"] = 14,
            ["sqrt"] = 15,
            ["sign"] = 16,
            ["ceil"] = 17,
            ["floor"] = 18,
            ["trunc"] = 19,
            ["round"] = 20,
            ["max"] = 21,
            ["min"] = 22,
        };

        private static readonly Func<double[], double>[] _functions = [
            args => Math.Sin(args[0]),                    // 0: sin
            args => Math.Cos(args[0]),                    // 1: cos
            args => Math.Tan(args[0]),                    // 2: tan
            args => Math.Asin(args[0]),                   // 3: asin
            args => Math.Acos(args[0]),                   // 4: acos
            args => Math.Atan(args[0]),                   // 5: atan
            args => Math.Atan2(args[0], args[1]),         // 6: atan2
            args => Math.Exp(args[0]),                    // 7: exp
            args => Math.Pow(args[0], args[1]),           // 8: pow
            args => Math.Log(args[0]),                    // 9: ln
            args => Math.Log10(args[0]),                  // 10: lg
            args => args.Length == 1 ? Math.Log(args[0]) : Math.Log(args[0], args[1]), // 11: log
            args => Math.Log2(args[0]),                   // 12: log2
            args => Math.Log10(args[0]),                  // 13: log10
            args => Math.Abs(args[0]),                    // 14: abs
            args => Math.Sqrt(args[0]),                   // 15: sqrt
            args => Math.Sign(args[0]),                   // 16: sign
            args => Math.Ceiling(args[0]),                // 17: ceil
            args => Math.Floor(args[0]),                  // 18: floor
            args => Math.Truncate(args[0]),               // 19: trunc
            args => args.Length == 1 ? Math.Round(args[0]) : Math.Round(args[0], (int)args[1]), // 20: round
            args => args.Max(),                           // 21: max
            args => args.Min(),                           // 22: min
        ];

        public static bool TryGetId(ReadOnlySpan<char> name, out byte id) {
            // 按长度分组快速匹配，与 BuiltInFunctions 一致
            switch (name.Length) {
                case 2:
                    if (EqualsLower(name, "ln")) { id = 9; return true; }
                    if (EqualsLower(name, "lg")) { id = 10; return true; }
                    break;
                case 3:
                    if (EqualsLower(name, "sin")) { id = 0; return true; }
                    if (EqualsLower(name, "cos")) { id = 1; return true; }
                    if (EqualsLower(name, "tan")) { id = 2; return true; }
                    if (EqualsLower(name, "exp")) { id = 7; return true; }
                    if (EqualsLower(name, "pow")) { id = 8; return true; }
                    if (EqualsLower(name, "abs")) { id = 14; return true; }
                    if (EqualsLower(name, "log")) { id = 11; return true; }
                    if (EqualsLower(name, "max")) { id = 21; return true; }
                    if (EqualsLower(name, "min")) { id = 22; return true; }
                    break;
                case 4:
                    if (EqualsLower(name, "asin")) { id = 3; return true; }
                    if (EqualsLower(name, "acos")) { id = 4; return true; }
                    if (EqualsLower(name, "atan")) { id = 5; return true; }
                    if (EqualsLower(name, "sqrt")) { id = 15; return true; }
                    if (EqualsLower(name, "sign")) { id = 16; return true; }
                    if (EqualsLower(name, "ceil")) { id = 17; return true; }
                    if (EqualsLower(name, "log2")) { id = 12; return true; }
                    break;
                case 5:
                    if (EqualsLower(name, "atan2")) { id = 6; return true; }
                    if (EqualsLower(name, "floor")) { id = 18; return true; }
                    if (EqualsLower(name, "trunc")) { id = 19; return true; }
                    if (EqualsLower(name, "round")) { id = 20; return true; }
                    if (EqualsLower(name, "log10")) { id = 13; return true; }
                    break;
            }
            id = 0;
            return false;
        }

        public static bool TryGetId(string name, out byte id) => _nameToId.TryGetValue(name, out id);

        public static Func<double[], double> GetFunction(byte id) => _functions[id];

        private static bool EqualsLower(ReadOnlySpan<char> span, string keyword) {
            if (span.Length != keyword.Length) return false;
            for (int i = 0; i < keyword.Length; i++) {
                if (char.ToLowerInvariant(span[i]) != char.ToLowerInvariant(keyword[i])) return false;
            }
            return true;
        }
    }

    #endregion
}
