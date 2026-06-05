using MathEval.Fast.Core;
using MathEval.Fast.Exceptions;
using MathEval.Fast.Operators;

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
        if (!BuiltInOperators.IsInteger(result))
            throw new FastEvalException($"结果 {result} 不是整数，无法转换为 long", expression);
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
        if (typeof(T) == typeof(double))
            return (T)(object)EvalDouble(expression, ConvertObjectVariables(variables));
        if (typeof(T) == typeof(long))
            return (T)(object)EvalLong(expression, ConvertObjectVariables(variables));
        if (typeof(T) == typeof(bool))
            return (T)(object)EvalBool(expression, variables);
        if (typeof(T) == typeof(int)) {
            var result = EvalLong(expression, ConvertObjectVariables(variables));
            if (result < int.MinValue || result > int.MaxValue)
                throw new FastEvalException($"结果 {result} 超出 int 范围");
            return (T)(object)(int)result;
        }
        if (typeof(T) == typeof(float))
            return (T)(object)(float)EvalDouble(expression, ConvertObjectVariables(variables));
        throw new FastEvalException($"不支持的类型: {typeof(T).Name}");
    }

    private static IReadOnlyDictionary<string, double>? ConvertObjectVariables(IReadOnlyDictionary<string, object>? variables) {
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
