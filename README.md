# Planet Lab

Godot 4 .NET + C# 的 2D 星球平台冒险原型。技术项目名仍为 `NewGame`。

## 项目文档

- [AGENTS.md](AGENTS.md)：所有 AI 共用的开发与协作规范。
- [PROJECT_STATE.md](PROJECT_STATE.md)：唯一当前状态汇总，包含实现、验证、人工反馈、限制与授权边界。
- [P002.md](P002.md)：下砸路线试玩步骤、参数及技术说明。
- [Tests/P002-RESULTS.md](Tests/P002-RESULTS.md)、[Tests/RESULTS.md](Tests/RESULTS.md)：历史实际验证记录。
- [docs/P001.md](docs/P001.md)：原 README 的 P-001 历史说明；其中旧行为不能当作当前状态。

## 启动

使用 Godot **4.6.1 .NET** 导入 `project.godot`，构建后 F5 运行默认场景。需要支持 net8.0 的 .NET SDK。原双星球地图可打开 `Scenes/Lab.tscn` 后 F6 运行。

A/D 移动、Space 跳跃、空中 J 下砸、R 重置；鼠标反向拖拽为接地调试发射，Esc 取消瞄准。具体路线见 P002.md，构建与测试命令统一见 AGENTS.md。

## 各 AI 工具如何接入

以下按 2026-10-20 查阅的官方文档整理；尚未在每款客户端中实际启动验证。工具版本、用户级规则与组织设置可能影响加载。从项目根目录启动或打开本项目，随后执行下方接手检查。

| 工具 | 本仓库入口 | 加载确认及注意事项 |
|---|---|---|
| Codex | 原生读取 `AGENTS.md`，按其中要求读取状态文件 | 新会话检查指令来源；同目录 `AGENTS.override.md` 可优先于 AGENTS.md，个人级指令也可能参与。见 [官方说明](https://developers.openai.com/codex/guides/agents-md/) |
| Cursor | 原生支持根目录 `AGENTS.md` | 不需要额外 `.cursor/rules` 副本；让 Agent 实际读取状态并报告依据。见 [官方说明](https://cursor.com/docs/rules) |
| Claude Code | `CLAUDE.md` 用 `@` 导入两份统一文档 | 新版在条件满足时也能原生读 AGENTS.md；保留导入入口兼容旧版及不同加载设置。用 `/context` 查看 Memory files，再核对实际内容。见 [官方说明](https://code.claude.com/docs/en/memory) |
| Gemini CLI | `GEMINI.md` 用 `@` 导入两份统一文档 | 默认无需改 settings；用 `/memory show` 检查，文档更新后可 `/memory reload`。见 [官方说明](https://geminicli.com/docs/cli/gemini-md/) |
| DeepSeek 模型 | 取决于承载模型的工具 | 官方提供通过 Claude Code 等工具接入的说明，因此按所用客户端加载规范；不假设存在统一的 DEEPSEEK.md。见 [官方接入说明](https://api-docs.deepseek.com/guides/coding_agents/) |

没有读取本地文件能力的聊天工具，需要人工提供 AGENTS.md、PROJECT_STATE.md 和相关代码材料，不能声称已检查仓库。本仓库不安装插件、不配置模型密钥或 MCP；两个兼容文件只保留导入引用。

## 新 AI 第一次接手的核验

可以直接发送以下请求，不需要另填需求模板：

> 请先读取 AGENTS.md 和 PROJECT_STATE.md，再检查 Git 状态、project.godot 和本次任务相关代码。暂不修改文件。请报告：实际读取的文件路径、当前分支与提交、已有未提交修改、默认场景、最近自动化验证依据、用户已验收与尚未验收的区别，以及本次授权边界。如文档与实际不符，请列出差异。

核对它是否引用了实际文件与提交，是否清楚区分“历史测试通过”和“本次执行通过”，是否能说明为什么还需要用户试玩。仅回答“已读取”不足以确认接手成功。发现未加载时显式要求打开两份文件；不要生成另一份完整规则副本。玩法变化的回归验证按 AGENTS.md 执行，接手检查本身无需修改项目。
