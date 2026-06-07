namespace MathEval.Fast.VM;

internal struct Instruction {
    public OpCode OpCode;
    public double DoubleOperand;  // PushConst value
    public string? StringOperand; // LoadVar variable name
    public int IntOperand;        // Jump target / function arg count
    public byte FunctionId;       // Built-in function ID for Call

    public static Instruction Push(double value) => new() { OpCode = OpCode.PushConst, DoubleOperand = value };
    public static Instruction LoadVar(string name) => new() { OpCode = OpCode.LoadVar, StringOperand = name };
    public static Instruction Op(OpCode op) => new() { OpCode = op };
    public static Instruction CallFunc(byte funcId, int argCount) => new() { OpCode = OpCode.Call, FunctionId = funcId, IntOperand = argCount };
    public static Instruction JumpIf(int target) => new() { OpCode = OpCode.JumpIfFalse, IntOperand = target };
    public static Instruction JumpTo(int target) => new() { OpCode = OpCode.Jump, IntOperand = target };
}
