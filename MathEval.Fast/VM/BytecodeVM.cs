using System.Buffers;
using MathEval.Fast.BuiltIn;
using MathEval.Fast.Exceptions;

namespace MathEval.Fast.VM;

/// <summary>
/// 字节码虚拟机，执行指令数组
/// <br/>
/// 使用预分配 double 栈，switch 分发指令，零 AST 开销
/// </summary>
internal static class BytecodeVM {

    public static double Execute(Instruction[] instructions, IReadOnlyDictionary<string, double>? variables) {
        var stack = ArrayPool<double>.Shared.Rent(64);
        try {
            int sp = 0;
            int ip = 0;

            while (ip < instructions.Length) {
                var instr = instructions[ip++];

                switch (instr.OpCode) {
                    case OpCode.PushConst:
                        EnsureStack(ref stack, sp);
                        stack[sp++] = instr.DoubleOperand;
                        break;

                    case OpCode.LoadVar:
                        EnsureStack(ref stack, sp);
                        if (variables == null || !variables.TryGetValue(instr.StringOperand!, out var varVal))
                            throw new FastEvalException($"未定义的变量 '{instr.StringOperand}'");
                        stack[sp++] = varVal;
                        break;

                    case OpCode.Add: { var r = stack[--sp]; stack[sp - 1] += r; break; }
                    case OpCode.Sub: { var r = stack[--sp]; stack[sp - 1] -= r; break; }
                    case OpCode.Mul: { var r = stack[--sp]; stack[sp - 1] *= r; break; }
                    case OpCode.Div: { var r = stack[--sp]; stack[sp - 1] /= r; break; }
                    case OpCode.IntDiv: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.IntegerDivide(stack[sp - 1], r); break; }
                    case OpCode.Mod: { var r = stack[--sp]; stack[sp - 1] %= r; break; }
                    case OpCode.MathMod: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.Modulo(stack[sp - 1], r); break; }
                    case OpCode.Pow: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.Power(stack[sp - 1], r); break; }

                    case OpCode.Negate: stack[sp - 1] = -stack[sp - 1]; break;
                    case OpCode.LogicalNot: stack[sp - 1] = BuiltInOperators.Not(stack[sp - 1]); break;
                    case OpCode.BitwiseNot: stack[sp - 1] = BuiltInOperators.BitwiseNot(stack[sp - 1]); break;

                    case OpCode.BitwiseOr: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.BitwiseOr(stack[sp - 1], r); break; }
                    case OpCode.BitwiseAnd: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.BitwiseAnd(stack[sp - 1], r); break; }
                    case OpCode.BitwiseXor: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.BitwiseXor(stack[sp - 1], r); break; }
                    case OpCode.LeftShift: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.LeftShift(stack[sp - 1], r); break; }
                    case OpCode.RightShift: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.RightShift(stack[sp - 1], r); break; }
                    case OpCode.UnsignedRightShift: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.UnsignedRightShift(stack[sp - 1], r); break; }

                    case OpCode.Equal: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.Equal(stack[sp - 1], r); break; }
                    case OpCode.NotEqual: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.NotEqual(stack[sp - 1], r); break; }
                    case OpCode.LessThan: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.LessThan(stack[sp - 1], r); break; }
                    case OpCode.LessOrEqual: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.LessThanOrEqual(stack[sp - 1], r); break; }
                    case OpCode.GreaterThan: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.GreaterThan(stack[sp - 1], r); break; }
                    case OpCode.GreaterOrEqual: { var r = stack[--sp]; stack[sp - 1] = BuiltInOperators.GreaterThanOrEqual(stack[sp - 1], r); break; }

                    case OpCode.LogicalAnd:
                    case OpCode.LogicalOr:
                        // These should not appear in compiled output; short-circuit is handled via jumps
                        throw new FastEvalException("内部错误：LogicalAnd/LogicalOr 不应由 VM 直接执行");

                    case OpCode.Call: {
                            var func = BuiltInFunctions.GetEvaluateById(instr.FunctionId);
                            var argCount = instr.IntOperand;
                            var args = new double[argCount];
                            for (int i = argCount - 1; i >= 0; i--) {
                                args[i] = stack[--sp];
                            }
                            stack[sp++] = func(args);
                            break;
                        }

                    case OpCode.JumpIfFalse: {
                            var val = stack[--sp];
                            if (!ConvertToBool(val)) ip = instr.IntOperand;
                            break;
                        }

                    case OpCode.Jump:
                        ip = instr.IntOperand;
                        break;

                    default:
                        throw new FastEvalException($"内部错误：未知的 OpCode {instr.OpCode}");
                }
            }

            return sp > 0 ? stack[0] : 0.0;
        } finally {
            ArrayPool<double>.Shared.Return(stack);
        }
    }

    private static bool ConvertToBool(double value) => value != 0 && !double.IsNaN(value);

    private static void EnsureStack(ref double[] stack, int sp) {
        if (sp >= stack.Length) {
            var newSize = stack.Length * 2;
            var newStack = ArrayPool<double>.Shared.Rent(newSize);
            stack.AsSpan().CopyTo(newStack);
            ArrayPool<double>.Shared.Return(stack);
            stack = newStack;
        }
    }
}
