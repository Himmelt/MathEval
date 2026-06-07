namespace MathEval.Fast.BuiltIn;

/// <summary>
/// FastEval 内置常量表，包含数学常数和特殊值
/// <br/>
/// 优化：Span 直接比较，零字符串分配
/// </summary>
internal static class BuiltInConstants {

    /// <summary>
    /// 尝试根据名称获取常量值，大小写敏感
    /// </summary>
    public static bool TryGetValue(string name, out double value) {
        return TryGetValue(name.AsSpan(), out value);
    }

    /// <summary>
    /// Span 版本常量查找，按长度分组快速匹配，零字符串分配
    /// </summary>
    public static bool TryGetValue(ReadOnlySpan<char> name, out double value) {
        switch (name.Length) {
            case 1:
                if (name[0] == 'E') { value = Math.E; return true; }
                if (name[0] == 'π') { value = Math.PI; return true; }
                break;
            case 2:
                if (name[0] == 'P' && name[1] == 'I') { value = Math.PI; return true; }
                if (name[0] == 'p' && name[1] == 'i') { value = Math.PI; return true; }
                break;
            case 3:
                if (name[0] == 'N' && name[1] == 'a' && name[2] == 'N') { value = double.NaN; return true; }
                if (name[0] == 'I' && name[1] == 'N' && name[2] == 'F') { value = double.PositiveInfinity; return true; }
                break;
            case 4:
                if (name[0] == 't' && name[1] == 'r' && name[2] == 'u' && name[3] == 'e') { value = 1.0; return true; }
                break;
            case 5:
                if (name[0] == 'f' && name[1] == 'a' && name[2] == 'l' && name[3] == 's' && name[4] == 'e') { value = 0.0; return true; }
                break;
        }
        value = 0;
        return false;
    }
}
