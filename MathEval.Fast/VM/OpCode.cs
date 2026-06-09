namespace MathEval.Fast.VM;

internal enum OpCode : byte {
    PushConst,     // Push constant double value
    LoadVar,       // Load variable by name
    Add, Sub, Mul, Div, IntDiv, Mod, MathMod, Pow,
    Negate, LogicalNot, BitwiseNot,
    BitwiseOr, BitwiseAnd, BitwiseXor, LeftShift, RightShift, UnsignedRightShift,
    Equal, NotEqual, LessThan, LessOrEqual, GreaterThan, GreaterOrEqual,
    LogicalAnd, LogicalOr,
    Call,          // Call built-in function (FunctionId + ArgCount)
    JumpIfFalse,   // Conditional jump (IntOperand = target)
    Jump,          // Unconditional jump (IntOperand = target)
}
