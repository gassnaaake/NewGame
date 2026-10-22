# Planet Lab

Godot 4 .NET + C# 的 2D 星球平台冒险原型。技术项目名仍为 `NewGame`。

## 项目文档

- [AGENTS.md](AGENTS.md)：所有 AI 共用的开发与协作规范。
- [PROJECT_STATE.md](PROJECT_STATE.md)：唯一当前状态汇总，包含实现、验证、人工反馈、限制与授权边界。
- [P0022.md](P0022.md)：GravityLayer、角色/摄像机朝向和新对照场景的试玩说明。
- [P002.md](P002.md)：P-002 下砸路线的历史说明；空中朝向和 J 时机已被 P-002.2 更新。
- [Tests/P0022-RESULTS.md](Tests/P0022-RESULTS.md)、[Tests/P002-RESULTS.md](Tests/P002-RESULTS.md)、[Tests/RESULTS.md](Tests/RESULTS.md)：实际验证记录。
- [docs/P001.md](docs/P001.md)：原 README 的 P-001 历史说明；其中旧行为不能当作当前状态。

## 启动

使用 Godot **4.7.2 .NET** 导入 `project.godot`，构建后 F5 运行默认场景。需要支持 net8.0 的 .NET SDK。当前工作区配置已切换至 4.7.2，但迁移后的完整构建/回归尚未在本次验证；历史 4.6.1 测试不能代替。环境证据见 [PROJECT_STATE.md](PROJECT_STATE.md)。

- P-002.2 对照场景：打开 [GravityLab.tscn](Scenes/GravityLab.tscn) 后按 **F6**；按 [P0022.md](P0022.md) 做 A/B/C 实验。
- P-002 原路线：F5 仍运行 [RouteLab.tscn](Scenes/RouteLab.tscn)；起跳后**立即按 J**，不要等待。
- 原双星球地图：打开 [Lab.tscn](Scenes/Lab.tscn) 后按 F6。

A/D 移动、Space 跳跃、空中 J 下砸、R 重置；鼠标反向拖拽为接地调试发射，Esc 取消瞄准。具体试玩见 [P0022.md](P0022.md)，构建与测试命令统一见 [AGENTS.md](AGENTS.md)。

## 各 AI 工具如何接入

所有客户端遵守同一份 [AGENTS.md](AGENTS.md) 和 [PROJECT_STATE.md](PROJECT_STATE.md)。从仓库根目录打开项目，接入后必须实际核验读取结果，不能仅凭客户端名称推断规则已加载。

| 工具 | 本仓库入口 | 加载确认 |
|---|---|---|
| Codex / 支持 AGENTS.md 的客户端 | 根目录 `AGENTS.md`，再按要求读取状态 | 核对本会话指令来源与实际文件，检查是否有覆盖规则 |
| Claude Code | `CLAUDE.md` 以 `@` 引用两份统一文档 | 核对客户端是否实际展开引用；未展开时显式读取 |
| Gemini CLI | `GEMINI.md` 以 `@` 引用两份统一文档 | 同上，不以文件存在等同于已加载 |
| DeepSeek Harness / 其他外部 Agent | 使用客户端支持的项目指令机制引用两份统一文档；或在任务入口明确要求读取 | 本仓库尚未核实其具体版本、默认入口名称及自动加载机制，不假设统一存在 `DEEPSEEK.md` |

无本地文件读取能力的聊天工具需人工提供两份文档及任务材料，不能声称已检查仓库。新增兼容入口前先确认实际客户端机制；只添加轻量引用，不复制规范。本次不配置客户端密钥或连接设置。

## Godot AI MCP 协作入口

本项目已启用 Godot AI v4.3.0，实际连接与测试证据见 [PROJECT_STATE.md](PROJECT_STATE.md) 的“环境、MCP 与迁移验证”。完整双端职责、会话写入协调、读回验证及未保存修改保护统一见 [AGENTS.md](AGENTS.md) 的“Godot AI MCP 双端协作”和“多 AI 协作”。外部客户端负责 C# / .NET / Git / 测试，MCP 负责实际编辑器交互；实际能力以当前连接的工具为准。

## 新 AI 第一次接手的核验

可以直接发送以下请求，不需要另填需求模板：

> 请先读取 AGENTS.md 和 PROJECT_STATE.md，再检查 Git 状态、project.godot 和本次任务相关代码。暂不修改文件。请报告：实际读取的文件路径、当前分支与提交、已有未提交修改、默认场景、当前引擎/SDK/插件版本及迁移验证边界、最近自动化验证依据、用户已验收与尚未验收的区别，以及本次授权边界。涉及 MCP 时核对会话项目路径、当前场景和可用工具，保护未保存修改。如文档与实际不符，请列出差异。

核对它是否引用了实际文件与提交，是否清楚区分“历史测试通过”和“本次执行通过”，是否能说明为什么还需要用户试玩。仅回答“已读取”不足以确认接手成功。发现未加载时显式要求打开两份文件；不要生成另一份完整规则副本。玩法变化的回归验证按 AGENTS.md 执行，接手检查本身无需修改项目。
