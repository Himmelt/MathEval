using MathEval.Fast.VM;

namespace MathEval.Fast.Jit;

/// <summary>
/// JIT 编译结果缓存，LRU 策略
/// </summary>
internal static class JitCache {
    private static readonly LruCache<string, CompiledEntry> _cache = new(64);

    private class CompiledEntry {
        public Instruction[] Instructions = [];
        public Func<IReadOnlyDictionary<string, double>?, double>? CompiledFunc;
    }

    /// <summary>
    /// 获取或编译指令序列
    /// </summary>
    public static Instruction[] GetOrCompileInstructions(string expression) {
        if (_cache.TryGet(expression, out var entry) && entry != null) {
            return entry.Instructions;
        }

        var instructions = InstructionCache.GetOrCompile(expression);
        var newEntry = new CompiledEntry { Instructions = instructions };
        _cache.Set(expression, newEntry);
        return instructions;
    }

    /// <summary>
    /// 获取或 JIT 编译表达式为原生委托
    /// </summary>
    public static Func<IReadOnlyDictionary<string, double>?, double> GetOrCompileJit(string expression) {
        if (_cache.TryGet(expression, out var entry) && entry != null) {
            if (entry.CompiledFunc != null) return entry.CompiledFunc;
        }

        var instructions = InstructionCache.GetOrCompile(expression);
        var compiledFunc = JitCompiler.Compile(instructions);

        if (entry != null) {
            entry.CompiledFunc = compiledFunc;
        } else {
            _cache.Set(expression, new CompiledEntry { Instructions = instructions, CompiledFunc = compiledFunc });
        }

        return compiledFunc;
    }

    public static void Clear() => _cache.Clear();
}
