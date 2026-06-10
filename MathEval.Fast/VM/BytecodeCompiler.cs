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

        // 查找函数定义并校验参数数量
        if (!BuiltInFunctions.TryGetId(name, out var funcId)) {
            throw new FastEvalException($"未知函数 '{name}'", _expression);
        }
        var def = BuiltInFunctions.GetById(funcId);
        if (argCount < def.MinArgs || argCount > def.MaxArgs) {
            var maxLabel = def.MaxArgs == int.MaxValue ? "N" : def.MaxArgs.ToString();
            throw new FastEvalException($"函数 {def.Name} 需要 {def.MinArgs}-{maxLabel} 个参数，但提供了 {argCount} 个", _expression);
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

}
