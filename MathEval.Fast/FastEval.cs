using MathEval.Fast.BuiltIn;
using MathEval.Fast.Core;
using MathEval.Fast.Exceptions;

namespace MathEval.Fast;

/// <summary>
/// 无上下文快速求值器，为纯数值单次求值场景提供独立快速路径
/// <br/>
/// 内部统一使用 double 运算，仅在最外层按需转换为目标类型
/// </summary>
public static class FastEval {
    /// <summary>
    /// 求值表达式并返回 double 结果
    /// </summary>
    public static double EvalDouble(string expression, IReadOnlyDictionary<string, double>? variables = null) {
        return new FastEvaluator(expression, variables).Evaluate();
    }

    /// <summary>
    /// 求值表达式并返回 long 结果
    /// <br/>
    /// 如果结果不含小数部分，转换为 long；否则抛出异常
    /// </summary>
    public static long EvalLong(string expression, IReadOnlyDictionary<string, double>? variables = null) {
        var result = EvalDouble(expression, variables);
        if (!BuiltInOperators.IsInteger(result)) throw new FastEvalException($"结果 {result} 不是整数，无法转换为 long", expression);
        return (long)result;
    }

    /// <summary>
    /// 求值表达式并返回 bool 结果
    /// </summary>
    public static bool EvalBool(string expression, IReadOnlyDictionary<string, object>? variables = null) {
        var doubleVars = ConvertObjectVariables(variables);
        var result = EvalDouble(expression, doubleVars);
        return BuiltInOperators.ConvertToBool(result);
    }

    /// <summary>
    /// 求值表达式并返回指定类型结果
    /// </summary>
    public static T Eval<T>(string expression, IReadOnlyDictionary<string, object>? variables = null) where T : struct {
        var doubleVars = ConvertObjectVariables(variables);

        // 浮点类型
        if (typeof(T) == typeof(double)) return (T)(object)EvalDouble(expression, doubleVars);
        if (typeof(T) == typeof(float)) return (T)(object)(float)EvalDouble(expression, doubleVars);
        if (typeof(T) == typeof(decimal)) return (T)(object)(decimal)EvalDouble(expression, doubleVars);

        // bool 类型
        if (typeof(T) == typeof(bool)) return (T)(object)EvalBool(expression, variables);

        // 整数类型
        if (IsIntegerType<T>()) {
            var result = EvalDouble(expression, doubleVars);
            if (!BuiltInOperators.IsInteger(result)) throw new FastEvalException($"结果 {result} 不是整数，无法转换为 {typeof(T).Name}", expression);
            return ConvertIntegerWithOverflowCheck<T>(result, expression);
        }

        throw new FastEvalException($"不支持的类型: {typeof(T).Name}", expression);
    }

    private static bool IsIntegerType<T>() {
        return typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) ||
               typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
               typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
               typeof(T) == typeof(long) || typeof(T) == typeof(ulong);
    }

    private static T ConvertIntegerWithOverflowCheck<T>(double value, string expression) {
        try {
            return (T)Convert.ChangeType(value, typeof(T));
        } catch (OverflowException) {
            throw new FastEvalException($"结果 {value} 超出 {typeof(T).Name} 范围", expression);
        }
    }

    private static Dictionary<string, double>? ConvertObjectVariables(IReadOnlyDictionary<string, object>? variables) {
        if (variables == null) return null;
        var result = new Dictionary<string, double>(variables.Count);
        foreach (var kv in variables) {
            result[kv.Key] = kv.Value switch {
                bool b => b ? 1.0 : 0.0,
                long l => l,
                double d => d,
                _ => Convert.ToDouble(kv.Value)
            };
        }
        return result;
    }
}