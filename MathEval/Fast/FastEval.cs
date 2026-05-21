using MathEval.Exceptions;

namespace MathEval.Fast;

/// <summary>
/// 无上下文快速求值器，为纯数值单次求值场景提供独立快速路径
/// </summary>
public static class FastEval {
    /// <summary>
    /// 求值表达式并返回 double 结果
    /// </summary>
    public static double EvalDouble(string expression, IReadOnlyDictionary<string, double>? variables = null) {
        return new FastEvaluator<double>(expression, variables).Evaluate();
    }

    /// <summary>
    /// 求值表达式并返回 long 结果
    /// </summary>
    public static long EvalLong(string expression, IReadOnlyDictionary<string, long>? variables = null) {
        return new FastEvaluator<long>(expression, variables).Evaluate();
    }

    /// <summary>
    /// 求值表达式并返回 bool 结果
    /// </summary>
    public static bool EvalBool(string expression, IReadOnlyDictionary<string, object>? variables = null) {
        Dictionary<string, double>? doubleVars = null;
        if (variables != null) {
            doubleVars = new Dictionary<string, double>(variables.Count);
            foreach (var kv in variables) {
                doubleVars[kv.Key] = kv.Value switch {
                    bool b => b ? 1.0 : 0.0,
                    long l => l,
                    double d => d,
                    _ => Convert.ToDouble(kv.Value)
                };
            }
        }
        var result = new FastEvaluator<double>(expression, doubleVars).Evaluate();
        return result != 0 && !double.IsNaN(result);
    }

    /// <summary>
    /// 求值表达式并返回指定类型结果
    /// </summary>
    public static T Eval<T>(string expression, IReadOnlyDictionary<string, object>? variables = null) where T : struct {
        if (typeof(T) == typeof(double))
            return (T)(object)EvalDouble(expression, ConvertVariables<double>(variables));
        if (typeof(T) == typeof(long))
            return (T)(object)EvalLong(expression, ConvertVariables<long>(variables));
        if (typeof(T) == typeof(bool))
            return (T)(object)EvalBool(expression, variables);
        throw new FastEvalException($"不支持的类型: {typeof(T).Name}");
    }

    private static IReadOnlyDictionary<string, TVar>? ConvertVariables<TVar>(IReadOnlyDictionary<string, object>? variables) where TVar : struct {
        if (variables == null) return null;
        var result = new Dictionary<string, TVar>(variables.Count);
        foreach (var kv in variables) {
            if (kv.Value is TVar typed)
                result[kv.Key] = typed;
            else if (kv.Value is bool b)
                result[kv.Key] = (TVar)Convert.ChangeType(b ? 1L : 0L, typeof(TVar));
            else
                result[kv.Key] = (TVar)Convert.ChangeType(kv.Value, typeof(TVar));
        }
        return result;
    }
}