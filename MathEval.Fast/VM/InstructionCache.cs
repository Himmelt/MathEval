namespace MathEval.Fast.VM;

internal static class InstructionCache {
    private static LruCache<string, Instruction[]> _cache = new(256);

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
        // Recreate cache with new capacity; this clears existing entries
        _cache = new LruCache<string, Instruction[]>(capacity);
    }
}
