namespace MathEval.Fast.VM;

internal static class InstructionCache {
    private static readonly LruCache<string, Instruction[]> _cache = new(256);

    public static Instruction[] GetOrCompile(string expression) {
        if (_cache.TryGet(expression, out var instructions) && instructions != null) {
            return instructions;
        }
        var compiler = new BytecodeCompiler(expression);
        instructions = compiler.Compile();
        _cache.Set(expression, instructions);
        return instructions;
    }

    public static void Clear() => _cache.Clear();

    public static void SetCapacity(int capacity) {
        // _cache 为 readonly，无法重建。清空缓存以释放条目。
        // 如需改变容量，应在初始化时设置。
        _cache.Clear();
    }
}
