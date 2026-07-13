---
alwaysApply: true
---

1. 在理解代码、定位 Bug、或进行修改前，优先使用 `codegraph_explore` MCP 工具进行代码探索，而不是依赖 Grep/SearchCodebase/Read 的循环搜索。
2. 调用 `codegraph_explore` 时，省略 `projectPath` 参数（自动使用当前会话的默认项目），`query` 使用自然语言描述要了解的功能、符号或文件。
3. `codegraph_explore` 返回的源码已等价于已读取（Read），不要重复使用 Read 工具读取已返回的文件内容。
4. 当需要了解多个不相关的模块时，可以并行发起多次 `codegraph_explore` 调用以提高效率。
