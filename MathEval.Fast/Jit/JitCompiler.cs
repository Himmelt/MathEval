using MathEval.Fast.BuiltIn;
using MathEval.Fast.Exceptions;
using MathEval.Fast.VM;
using System.Reflection;
using System.Reflection.Emit;

namespace MathEval.Fast.Jit;

/// <summary>
/// 将指令序列编译为原生委托（DynamicMethod + ILGenerator）
/// <br/>
/// 直接使用 IL 求值栈，避免手动管理栈数组
/// <br/>
/// 编译耗时约 2,000-5,000ns，编译后执行约 3-5ns
/// </summary>
internal static class JitCompiler {

    /// <summary>
    /// 将指令序列编译为原生委托
    /// </summary>
    public static Func<IReadOnlyDictionary<string, double>?, double> Compile(Instruction[] instructions) {
        var method = new DynamicMethod(
            "FastEval_Jit",
            typeof(double),
            [typeof(IReadOnlyDictionary<string, double>)],
            typeof(JitCompiler).Module,
            skipVisibility: true
        );

        var il = method.GetILGenerator();

        // 为跳转指令预创建标签（含末尾位置，跳转目标可能等于 instructions.Length）
        var labels = new Label[instructions.Length + 1];
        for (int i = 0; i <= instructions.Length; i++) {
            labels[i] = il.DefineLabel();
        }

        // 逐条编译指令
        for (int ip = 0; ip < instructions.Length; ip++) {
            var instr = instructions[ip];

            il.MarkLabel(labels[ip]);

            switch (instr.OpCode) {
                case OpCode.PushConst:
                    il.Emit(OpCodes.Ldc_R8, instr.DoubleOperand);
                    break;

                case OpCode.LoadVar:
                    EmitLoadVar(il, instr.StringOperand!);
                    break;

                case OpCode.Add: il.Emit(OpCodes.Add); break;
                case OpCode.Sub: il.Emit(OpCodes.Sub); break;
                case OpCode.Mul: il.Emit(OpCodes.Mul); break;
                case OpCode.Div: il.Emit(OpCodes.Div); break;
                case OpCode.Mod: il.Emit(OpCodes.Rem); break;
                case OpCode.Negate: il.Emit(OpCodes.Neg); break;

                case OpCode.IntDiv: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.IntegerDivide).Method); break;
                case OpCode.MathMod: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Modulo).Method); break;
                case OpCode.Pow: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Power).Method); break;

                case OpCode.LogicalNot: EmitUnaryCall(il, ((Func<double, double>)BuiltInOperators.Not).Method); break;
                case OpCode.BitwiseNot: EmitUnaryCall(il, ((Func<double, double>)BuiltInOperators.BitwiseNot).Method); break;

                case OpCode.BitwiseOr: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseOr).Method); break;
                case OpCode.BitwiseAnd: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseAnd).Method); break;
                case OpCode.BitwiseXor: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseXor).Method); break;
                case OpCode.LeftShift: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LeftShift).Method); break;
                case OpCode.RightShift: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.RightShift).Method); break;
                case OpCode.UnsignedRightShift: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.UnsignedRightShift).Method); break;

                case OpCode.Equal: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Equal).Method); break;
                case OpCode.NotEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.NotEqual).Method); break;
                case OpCode.LessThan: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LessThan).Method); break;
                case OpCode.LessOrEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LessThanOrEqual).Method); break;
                case OpCode.GreaterThan: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.GreaterThan).Method); break;
                case OpCode.GreaterOrEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.GreaterThanOrEqual).Method); break;

                case OpCode.Call:
                    EmitCall(il, instr.FunctionId, instr.IntOperand);
                    break;

                case OpCode.JumpIfFalse:
                    EmitJumpIfFalse(il, labels[instr.IntOperand]);
                    break;

                case OpCode.Jump:
                    il.Emit(OpCodes.Br, labels[instr.IntOperand]);
                    break;

                default:
                    throw new FastEvalException($"JIT 不支持的 OpCode: {instr.OpCode}");
            }
        }

        // 标记末尾标签（跳转目标 = instructions.Length 时跳到这里）
        il.MarkLabel(labels[instructions.Length]);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<IReadOnlyDictionary<string, double>?, double>>();
    }

    #region IL 发射辅助

    private static void EmitLoadVar(ILGenerator il, string varName) {
        var foundLabel = il.DefineLabel();
        var throwLabel = il.DefineLabel();
        var valLocal = il.DeclareLocal(typeof(double));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Brfalse_S, throwLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, varName);
        il.Emit(OpCodes.Ldloca, valLocal);
        il.Emit(OpCodes.Callvirt, typeof(IReadOnlyDictionary<string, double>).GetMethod("TryGetValue")!);
        il.Emit(OpCodes.Brtrue_S, foundLabel);

        il.MarkLabel(throwLabel);
        il.Emit(OpCodes.Ldstr, varName);
        il.Emit(OpCodes.Call, typeof(JitCompiler).GetMethod(nameof(ThrowUndefinedVariable), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(OpCodes.Throw);

        il.MarkLabel(foundLabel);
        il.Emit(OpCodes.Ldloc, valLocal);
    }

    private static void EmitBinaryCall(ILGenerator il, MethodInfo method) {
        il.Emit(OpCodes.Call, method);
    }

    private static void EmitUnaryCall(ILGenerator il, MethodInfo method) {
        il.Emit(OpCodes.Call, method);
    }

    /// <summary>
    /// 从 BuiltInFunctions 统一定义源获取 MethodInfo 并发射 IL 调用
    /// <br/>
    /// 编译期解析函数定义，直接发射对具体 Math 方法的 Call 指令
    /// </summary>
    private static void EmitCall(ILGenerator il, byte functionId, int argCount) {
        var def = BuiltInFunctions.GetById(functionId);

        // 链式调用模式（max/min）：argCount >= 2 时链式调用 JitMethod2
        if (def.MaxArgs == int.MaxValue && def.JitMethod2 != null) {
            for (int i = 1; i < argCount; i++) il.Emit(OpCodes.Call, def.JitMethod2);
            return;
        }

        // 双参数路径
        if (argCount == 2 && def.JitMethod2 != null) {
            il.Emit(OpCodes.Call, def.JitMethod2);
            return;
        }

        // 单参数路径
        if (argCount == 1 && def.JitMethod1 != null) {
            il.Emit(OpCodes.Call, def.JitMethod1);
            return;
        }

        throw new FastEvalException($"JIT 不支持函数 '{def.Name}' 的 {argCount} 参数调用路径");
    }

    private static void EmitJumpIfFalse(ILGenerator il, Label targetLabel) {
        il.Emit(OpCodes.Call, typeof(JitCompiler).GetMethod(nameof(ConvertToBool), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(OpCodes.Brfalse, targetLabel);
    }

    private static bool ConvertToBool(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    /// 辅助方法：构造 FastEvalException（避免主构造函数反射问题）
    /// </summary>
    private static FastEvalException ThrowUndefinedVariable(string varName) => new($"未定义的变量 '{varName}'");

    #endregion
}
