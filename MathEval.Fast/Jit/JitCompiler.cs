using System.Reflection;
using System.Reflection.Emit;
using MathEval.Fast.BuiltIn;
using MathEval.Fast.Exceptions;
using MathEval.Fast.VM;
using VmOpCode = MathEval.Fast.VM.OpCode;

namespace MathEval.Fast.Jit;

/// <summary>
/// 将指令序列编译为原生委托（DynamicMethod + ILGenerator）
/// <br/>
/// 直接使用 IL 求值栈，避免手动管理栈数组
/// <br/>
/// 编译耗时约 2,000-5,000ns，编译后执行约 3-5ns
/// </summary>
internal static class JitCompiler {

    #region MethodInfo 缓存（编译期直接解析函数ID到具体方法，避免运行时传int）

    private static readonly MethodInfo _sin = typeof(Math).GetMethod("Sin", [typeof(double)])!;
    private static readonly MethodInfo _cos = typeof(Math).GetMethod("Cos", [typeof(double)])!;
    private static readonly MethodInfo _tan = typeof(Math).GetMethod("Tan", [typeof(double)])!;
    private static readonly MethodInfo _asin = typeof(Math).GetMethod("Asin", [typeof(double)])!;
    private static readonly MethodInfo _acos = typeof(Math).GetMethod("Acos", [typeof(double)])!;
    private static readonly MethodInfo _atan = typeof(Math).GetMethod("Atan", [typeof(double)])!;
    private static readonly MethodInfo _atan2 = typeof(Math).GetMethod("Atan2", [typeof(double), typeof(double)])!;
    private static readonly MethodInfo _exp = typeof(Math).GetMethod("Exp", [typeof(double)])!;
    private static readonly MethodInfo _pow = typeof(Math).GetMethod("Pow", [typeof(double), typeof(double)])!;
    private static readonly MethodInfo _log = typeof(Math).GetMethod("Log", [typeof(double)])!;
    private static readonly MethodInfo _logBase = typeof(Math).GetMethod("Log", [typeof(double), typeof(double)])!;
    private static readonly MethodInfo _log10 = typeof(Math).GetMethod("Log10", [typeof(double)])!;
    private static readonly MethodInfo _log2 = typeof(Math).GetMethod("Log2", [typeof(double)])!;
    private static readonly MethodInfo _abs = typeof(Math).GetMethod("Abs", [typeof(double)])!;
    private static readonly MethodInfo _sqrt = typeof(Math).GetMethod("Sqrt", [typeof(double)])!;
    private static readonly MethodInfo _sign = typeof(Math).GetMethod("Sign", [typeof(double)])!;
    private static readonly MethodInfo _ceiling = typeof(Math).GetMethod("Ceiling", [typeof(double)])!;
    private static readonly MethodInfo _floor = typeof(Math).GetMethod("Floor", [typeof(double)])!;
    private static readonly MethodInfo _truncate = typeof(Math).GetMethod("Truncate", [typeof(double)])!;
    private static readonly MethodInfo _round = typeof(Math).GetMethod("Round", [typeof(double)])!;
    private static readonly MethodInfo _max = typeof(Math).GetMethod("Max", [typeof(double), typeof(double)])!;
    private static readonly MethodInfo _min = typeof(Math).GetMethod("Min", [typeof(double), typeof(double)])!;
    private static readonly MethodInfo _roundDigits = ((Func<double, double, double>)RoundWithDigits).Method;

    #endregion

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
                case VmOpCode.PushConst:
                    il.Emit(System.Reflection.Emit.OpCodes.Ldc_R8, instr.DoubleOperand);
                    break;

                case VmOpCode.LoadVar:
                    EmitLoadVar(il, instr.StringOperand!);
                    break;

                case VmOpCode.Add: il.Emit(System.Reflection.Emit.OpCodes.Add); break;
                case VmOpCode.Sub: il.Emit(System.Reflection.Emit.OpCodes.Sub); break;
                case VmOpCode.Mul: il.Emit(System.Reflection.Emit.OpCodes.Mul); break;
                case VmOpCode.Div: il.Emit(System.Reflection.Emit.OpCodes.Div); break;
                case VmOpCode.Mod: il.Emit(System.Reflection.Emit.OpCodes.Rem); break;
                case VmOpCode.Negate: il.Emit(System.Reflection.Emit.OpCodes.Neg); break;

                case VmOpCode.IntDiv: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.IntegerDivide).Method); break;
                case VmOpCode.MathMod: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Modulo).Method); break;
                case VmOpCode.Pow: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Power).Method); break;

                case VmOpCode.LogicalNot: EmitUnaryCall(il, ((Func<double, double>)BuiltInOperators.Not).Method); break;
                case VmOpCode.BitwiseNot: EmitUnaryCall(il, ((Func<double, double>)BuiltInOperators.BitwiseNot).Method); break;

                case VmOpCode.BitwiseOr: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseOr).Method); break;
                case VmOpCode.BitwiseAnd: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseAnd).Method); break;
                case VmOpCode.BitwiseXor: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.BitwiseXor).Method); break;
                case VmOpCode.LeftShift: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LeftShift).Method); break;
                case VmOpCode.RightShift: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.RightShift).Method); break;

                case VmOpCode.Equal: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.Equal).Method); break;
                case VmOpCode.NotEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.NotEqual).Method); break;
                case VmOpCode.LessThan: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LessThan).Method); break;
                case VmOpCode.LessOrEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.LessThanOrEqual).Method); break;
                case VmOpCode.GreaterThan: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.GreaterThan).Method); break;
                case VmOpCode.GreaterOrEqual: EmitBinaryCall(il, ((Func<double, double, double>)BuiltInOperators.GreaterThanOrEqual).Method); break;

                case VmOpCode.Call:
                    EmitCall(il, instr.FunctionId, instr.IntOperand);
                    break;

                case VmOpCode.JumpIfFalse:
                    EmitJumpIfFalse(il, labels[instr.IntOperand]);
                    break;

                case VmOpCode.Jump:
                    il.Emit(System.Reflection.Emit.OpCodes.Br, labels[instr.IntOperand]);
                    break;

                default:
                    throw new FastEvalException($"JIT 不支持的 OpCode: {instr.OpCode}");
            }
        }

        // 标记末尾标签（跳转目标 = instructions.Length 时跳到这里）
        il.MarkLabel(labels[instructions.Length]);
        il.Emit(System.Reflection.Emit.OpCodes.Ret);

        return method.CreateDelegate<Func<IReadOnlyDictionary<string, double>?, double>>();
    }

    #region IL 发射辅助

    private static void EmitLoadVar(ILGenerator il, string varName) {
        var foundLabel = il.DefineLabel();
        var throwLabel = il.DefineLabel();
        var valLocal = il.DeclareLocal(typeof(double));

        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il.Emit(System.Reflection.Emit.OpCodes.Brfalse_S, throwLabel);

        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il.Emit(System.Reflection.Emit.OpCodes.Ldstr, varName);
        il.Emit(System.Reflection.Emit.OpCodes.Ldloca, valLocal);
        il.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(IReadOnlyDictionary<string, double>).GetMethod("TryGetValue")!);
        il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, foundLabel);

        il.MarkLabel(throwLabel);
        il.Emit(System.Reflection.Emit.OpCodes.Ldstr, varName);
        il.Emit(System.Reflection.Emit.OpCodes.Call, typeof(JitCompiler).GetMethod(nameof(ThrowUndefinedVariable), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(System.Reflection.Emit.OpCodes.Throw);

        il.MarkLabel(foundLabel);
        il.Emit(System.Reflection.Emit.OpCodes.Ldloc, valLocal);
    }

    private static void EmitBinaryCall(ILGenerator il, MethodInfo method) {
        il.Emit(System.Reflection.Emit.OpCodes.Call, method);
    }

    private static void EmitUnaryCall(ILGenerator il, MethodInfo method) {
        il.Emit(System.Reflection.Emit.OpCodes.Call, method);
    }

    /// <summary>
    /// 编译期解析函数ID，直接发射对具体 Math 方法的 Call 指令
    /// <br/>
    /// 关键：避免在 DynamicMethod 中使用 Ldc_I4（.NET 10 下 Ldc_I4 + Call 会触发 InvalidProgramException）
    /// </summary>
    private static void EmitCall(ILGenerator il, byte functionId, int argCount) {
        switch (functionId) {
            // 单参数 Math 方法
            case 0:  il.Emit(System.Reflection.Emit.OpCodes.Call, _sin); break;      // sin
            case 1:  il.Emit(System.Reflection.Emit.OpCodes.Call, _cos); break;      // cos
            case 2:  il.Emit(System.Reflection.Emit.OpCodes.Call, _tan); break;      // tan
            case 3:  il.Emit(System.Reflection.Emit.OpCodes.Call, _asin); break;     // asin
            case 4:  il.Emit(System.Reflection.Emit.OpCodes.Call, _acos); break;     // acos
            case 5:  il.Emit(System.Reflection.Emit.OpCodes.Call, _atan); break;     // atan
            case 7:  il.Emit(System.Reflection.Emit.OpCodes.Call, _exp); break;      // exp
            case 9:  il.Emit(System.Reflection.Emit.OpCodes.Call, _log); break;      // ln
            case 10: il.Emit(System.Reflection.Emit.OpCodes.Call, _log10); break;    // lg
            case 12: il.Emit(System.Reflection.Emit.OpCodes.Call, _log2); break;     // log2
            case 13: il.Emit(System.Reflection.Emit.OpCodes.Call, _log10); break;    // log10
            case 14: il.Emit(System.Reflection.Emit.OpCodes.Call, _abs); break;      // abs
            case 15: il.Emit(System.Reflection.Emit.OpCodes.Call, _sqrt); break;     // sqrt
            case 16: // sign → Math.Sign(double) 返回 int，需 Conv_R8 转为 double
                il.Emit(System.Reflection.Emit.OpCodes.Call, _sign);
                il.Emit(System.Reflection.Emit.OpCodes.Conv_R8);
                break;
            case 17: il.Emit(System.Reflection.Emit.OpCodes.Call, _ceiling); break;  // ceil
            case 18: il.Emit(System.Reflection.Emit.OpCodes.Call, _floor); break;    // floor
            case 19: il.Emit(System.Reflection.Emit.OpCodes.Call, _truncate); break; // trunc

            // 双参数 Math 方法
            case 6:  il.Emit(System.Reflection.Emit.OpCodes.Call, _atan2); break;    // atan2
            case 8:  il.Emit(System.Reflection.Emit.OpCodes.Call, _pow); break;      // pow

            // 可变参数函数
            case 11: // log: 1参数=自然对数, 2参数=指定底数
                if (argCount == 1)
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _log);
                else
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _logBase);
                break;
            case 20: // round: 1参数=四舍五入, 2参数=指定小数位
                if (argCount == 1)
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _round);
                else
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _roundDigits);
                break;
            case 21: // max: 链式调用 Math.Max（满足交换律和结合律）
                for (int i = 1; i < argCount; i++)
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _max);
                break;
            case 22: // min: 链式调用 Math.Min（满足交换律和结合律）
                for (int i = 1; i < argCount; i++)
                    il.Emit(System.Reflection.Emit.OpCodes.Call, _min);
                break;

            default:
                throw new FastEvalException($"JIT 不支持的函数 ID: {functionId}");
        }
    }

    /// <summary>
    /// 辅助方法：Math.Round 的 double,double 重载（避免 IL 中需要 Conv_I4 转换）
    /// </summary>
    private static double RoundWithDigits(double value, double digits) => Math.Round(value, (int)digits);

    private static void EmitJumpIfFalse(ILGenerator il, Label targetLabel) {
        il.Emit(System.Reflection.Emit.OpCodes.Call, typeof(JitCompiler).GetMethod(nameof(ConvertToBool), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(System.Reflection.Emit.OpCodes.Brfalse, targetLabel);
    }

    private static bool ConvertToBool(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    /// 辅助方法：构造 FastEvalException（避免主构造函数反射问题）
    /// </summary>
    private static FastEvalException ThrowUndefinedVariable(string varName) =>
        new($"未定义的变量 '{varName}'");

    #endregion
}
