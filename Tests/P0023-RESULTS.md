# P-002.3 实际验证记录

日期：2026-10-22（原始 P-002.3 验证）；2026-10-23 补充编辑器易用性修正。环境：Windows、Godot **4.7.2-stable mono official**、.NET SDK 10.0.100（目标 net8.0）、Godot AI 4.3.0。原始代码基线：工作区 HEAD `cfac47e` + 当轮未提交修改；2026-10-23 基线为用户提交 `2b03179` + 当轮编辑器修正修改。本文只记录实际执行过的检查；用户试玩/编辑器验收另行进行。

> **2026-10-23 后续结果**：中文 Inspector 说明与平台真实子碰撞修正后，Godot 4.7.2 下 120/30 各 **354 项**通过（P-002.2/P-001 299 + P-002.3 55）；平台 Shape 在编辑器中改为 440 宽后运行时碰撞命中，已恢复 400。详见下方“0. 2026-10-23 编辑器易用性修正”。下方 339/46 记录保留为当轮历史结果。

## 0. 2026-10-23 编辑器易用性修正（当前结果）

实际命令：

```powershell
dotnet build NewGame.csproj -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 120
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 30
```

- 构建成功，0 警告、0 错误；120/30 各 **354 项**通过：

| 组 | 数量 |
|---|---:|
| P-001 运动/跳跃/发射/旅行 | 104 |
| 落地/切向/连续朝向 | 68 |
| 下砸 | 14 |
| 平台碰撞/破坏/穿越 + 子碰撞/中文说明元数据 | 26 |
| GravityLayer/固定区域/零重力/角色/摄像机 | 73 |
| P-002 路线 | 14 |
| P-002.3 GravityPlayground | 55 |

- 日志：`.godot/test-results/p0023-editor-final-{120,30}-console.log`、`p0022-*-{120,30}.log`、`p0023-playground-{120,30}.log`；`^ERROR:|^FAIL` 无匹配。
- **平台迁移**：`GravityPlayground` 的 11 个平台、`GravityLab` 的 `Deck`、`RouteLab` 的 5 个平台都使用真实 `CollisionShape2D` 子节点；平台节点不再有 `Size`。用户未提交的 Sphere 吸引半径 `138.84` 保留。
- **编辑器→运行时碰撞验证**：`StartDeck` 的 Shape 初始 `(400,16)`；在编辑器改为 `(440,16)` 并保存后运行，游戏内 Shape 读回 `(440,16)`，在旧范围外 `(-1710,-380)` 的物理查询命中 `StartDeck` 1 次；停止后恢复 `(400,16)` 并保存。场景最终值为 400。
- **中文说明验证**：`PlatformTests` 与 `PlaygroundTests` 通过 `GetPropertyList()` 断言四个组件都有 `UsageNote`、`ReadOnly` 存在、`Storage` 不存在，并检查中文玩法分组；`UsageNote` 不写入 `.tscn`。MCP 不能截取 Inspector dock，实际排版/字体/阅读体验仍需用户验收。
- **编辑器截图**：`.godot/test-results/p0023-editor-collision.png`、`p0023-editor-gravitylab-deck.png`、`p0023-editor-sphere.png`、`p0023-editor-platform-tree.png`、`p0023-editor-scene-tree.png`；能看到平台 → `CollisionShape2D` 的 Scene 树（截图布局较窄，平台名被截断，但层级与子节点可见）。
- 用户已在 P-002.3 整体试玩中反馈“相比之前有改善”；本次编辑器修正**尚未由用户操作验收**，不能把自动化/截图当作编辑器验收。

## 1. 环境与构建

| 项目 | 实际值 |
|---|---|
| Godot | `4.7.2-stable (official)`，编辑器 PID 22448 |
| Godot 控制台程序 | `D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe` |
| .NET SDK | `10.0.100`（同时安装 8.0.412 / 9.0.200） |
| 项目 SDK / 目标 | `Godot.NET.Sdk/4.7.2`、`net8.0`、`NewGame.dll` |
| project.godot | features `4.7 / C# / Forward Plus`，物理 120 Hz，默认 `RouteLab.tscn` |
| 插件 | `addons/godot_ai/plugin.cfg` 4.3.0，MCP 会话 `new-game@b6f481ef83a04d38`，ready |

构建命令与实际结果：

```powershell
dotnet build NewGame.csproj -p:UseSharedCompilation=false
# 已成功生成。0 个警告，0 个错误
# -> .godot/mono/temp/bin/Debug/NewGame.dll
```

`Tests/Run-P0023.ps1` 内部实际执行同样的构建；120/30 两次完整运行均 0 错误、0 警告。

## 2. 完整回归命令与结果

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 120
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 30
```

两组实际执行成功，物理频率均为 120 Hz。每组 **339 项**检查：

| 测试组 | 数量 | 场景 |
|---|---:|---|
| P-001 运动、跳跃、拖拽、双向旅行回归 | 104 | `Lab.tscn` |
| 落地、切向保留、稳定停留、连续空中朝向 | 68 | `Lab.tscn` |
| 下砸状态、方向、持续飞行、J 输入、薄墙 | 14 | `Lab.tscn` |
| 易碎/穿越/普通平台、重叠恢复、多层扫掠 | 20 | `Lab.tscn` |
| GravityLayer、固定区域、零重力、角色/摄像机 | 73 | `GravityLab.tscn` |
| P-002 路线：正常跳跃→倾斜平台→立即下砸→胶囊 | 14 | `RouteLab.tscn` |
| P-002.3 灰盒关卡 | 46 | `GravityPlayground.tscn` |

日志（Git 忽略）：

- `.godot/test-results/p0022-self-test-{120,30}.log`
- `.godot/test-results/p0022-stage-a-{120,30}.log`
- `.godot/test-results/p0022-stage-b-{120,30}.log`
- `.godot/test-results/p0022-platform-tests-{120,30}.log`
- `.godot/test-results/p0022-gravity-tests-{120,30}.log`
- `.godot/test-results/p0022-stage-c-{120,30}.log`
- `.godot/test-results/p0023-playground-{120,30}.log`
- `.godot/test-results/p0023-final-{120,30}-console.log`

对上述当前日志执行 `Select-String '^ERROR:|^FAIL'` 无匹配；每组日志末尾均为对应 PASS 汇总。

## 3. GravityPlayground 路线验证

`PlaygroundTests.cs` 在真实场景与真实物理循环中运行。路线从出生点开始，只使用 `TestAxis`（左右）、`TestJump`（跳）和一次真实 `TryBeginDash()`；路线过程中没有 `ResetAt`、没有位置写入、没有传送、没有目标星球识别。`ResetAt` 只用于出生重置和定点探针。

实际路线事件（120 FPS 日志摘录）：

```text
ROUTE frame=276 support=Step1 pos=(-1129.9473, -440.0669)
ROUTE frame=432 support=Step2 pos=(-869.94794, -480.0669)
ROUTE frame=588 support=FloatDeck pos=(-609.94476, -520.06635)
ROUTE frame=733 support=IslandDeck pos=(-368.2778, -560.0664)
ROUTE frame=864 support=SumDeck pos=(-149.9449, -600.067)
ROUTE frame=1203 support=BridgeDeck pos=(429.91016, -620.0669)
ROUTE frame=1403 support=LaunchRamp pos=(810.03265, -626.16815)
ROUTE frame=1520 ramp departure bodyDown=(0.25875872, 0.965942) pos=(997.88806, -681.3914)
PASS: ramp takeoff keeps the body-down aimed at the capsule
PASS: scripted input reaches the capsule goal without relocation
PASS: goal area records the arrival
PASS: the player finishes grounded on the capsule planet
```

- 同层叠加区实际选择到两个 L0 贡献者，合计 `(300,550)`，Applied 也是 `(300,550)`，HUD 速度约 626。
- L1 浮空区、L0 岛、L0 桥、倾斜发射台的选择与方向均按场景值生效。
- 发射台起跳后 `bodyDown` 与 `(0.2588,0.9659)` 点积 > 0.98；下砸直线击中胶囊，终点 Area 触发。
- 终点后 `PendingCollisionRestores == 0`，发射台 `DashPass` 的碰撞例外已恢复。
- 路线中摄像机最大单物理帧变化 0.42°（120 FPS）/ 1.44°（30 FPS），未出现单帧跳变。

## 4. 逐帧落地探针

平甲板场景：从 START DECK 上方 42 px 以 300 px/s 下落；球面场景从球顶上方下落。打印字段包含 x/y、速度、支撑法线、外向速度、State、SupportBody、物理 Up、BodyUp、CameraRotation。

平甲板实际摘录：

```text
CONTACT flat deck support=StartDeck pos=(-1500, -400.0034) layer=0
LANDING flat deck f=10 x=-1500.00 y=-400.003 v=(0, 0) n=(0.00, -1.00) outward=0.000 state=Normal support=StartDeck up=(-0.00, -1.00) bodyUp=(-0.00, -1.00) cam=-0.00
LANDING flat deck f=11 x=-1500.00 y=-400.028 v=(0, 0) n=(0.00, -1.00) outward=0.000 ...
LANDING flat deck f=22 x=-1500.00 y=-400.032 v=(0, 0) n=(0.00, -1.00) outward=0.000 ...
```

球面实际摘录：

```text
CONTACT sphere support=Sphere pos=(-1500, -92.012764) layer=2
LANDING sphere f=28 x=-1500.00 y=-92.013 v=(0, 0) n=(0.00, -1.00) outward=0.000 state=Normal support=Sphere up=(-0.00, -1.00) bodyUp=(-0.00, -1.00) cam=-0.00
LANDING sphere f=40 x=-1500.00 y=-92.032 v=(0, 0) n=(0.00, -1.00) outward=0.000 ...
```

- 接触后 13 帧内 `velocity=(0,0)`、`outward=0.000`，位置只从 -400.003 稳定到 -400.032（0.029 px），没有持续外向位移。
- 这只能说明**投影后**没有可观察的反弹；探针没有在 `MoveAndSlide()` 之后、法线投影之前埋点，不能证明接触前没有一帧外向速度。用户主观“轻微反弹”仍需试玩判断，不能据此关闭问题。

## 5. 编辑器与 MCP 检查

实际使用的 MCP 工具：

- `session_manage(op="list")`：项目路径 `E:/goDotProject/new-game/`、Godot 4.7.2、插件/服务端 4.3.0、ready。
- `editor_state`：当前场景、ready/playing/stopped 状态。
- `scene_manage(create)`、`batch_execute`/`node_create`/`attach_script`/`set_property`：创建并保存 `GravityPlayground.tscn`。
- `node_get_properties`：读回区域、星球、平台、HUD、Goal 的属性；确认 `GravityLayer`、`Size`、`Direction`、`GravityStrength`、`Kind`、脚本路径等。
- `editor_screenshot(source="viewport_2d")`：查看 `GravityLab` 编辑器预览；矩形、方向箭头、星球方向采样、影响边界均可见。
- `project_run(mode="custom")`、`editor_screenshot(source="game")`、`editor_manage(game_eval)`、`project_manage(stop)`：实际渲染截图与分区观察；游戏日志无 ERROR。
- 运行中把 `StartFlat_L0` 的 `GravityStrength` 从 550 改为 300（`game_eval` 设置导出属性）后，HUD 的 Active regions 立即变为 `(0,300)`，`Gravity raw/applied` 变为 `(0,300)`，区域标签变为 `g 300`；截图 `p0023-gravity300.png`。这是运行时实例修改，没有保存回场景；停止后编辑器读回场景值仍为 550。它证明导出属性确实驱动实际重力，而不仅是 Inspector 显示。

编辑器截图证据（Git 忽略）：

- `.godot/test-results/p0023-editor-gravitylab.png`（原始 2D 编辑器视口）
- `.godot/test-results/p0023-editor-gravitylab-crop.png`（裁剪/放大后的区域与星球箭头）

游戏渲染截图证据（用 `game_eval` 定位到分区后截图，只用于视觉记录，不是通关证明）：

- `.godot/test-results/p0023-spawn.png`
- `.godot/test-results/p0023-float.png`
- `.godot/test-results/p0023-sum.png`
- `.godot/test-results/p0023-ramp.png`
- `.godot/test-results/p0023-goal.png`
- `.godot/test-results/p0023-feather.png`
- `.godot/test-results/p0023-gravity300.png`（运行时把 START 区强度改为 300 的 HUD 证据）

## 6. 本轮结论与未验证（2026-10-22 历史）

> 本节是 2026-10-22 当轮结论，保留为历史。2026-10-23 当前结果见第 0 节（354 项）；用户之后已试玩 P-002.3 整体路线并反馈改善，但本轮编辑器修正仍待用户操作验收。

已实际确认：

- Godot 4.7.2 .NET 可构建并加载 C# 程序集；`Run-P0023.ps1` 的 339 项检查在 120/30 显示步频通过。
- 新场景可启动、可运行，从出生点用现有移动/跳跃/下砸到达胶囊终点；同层叠加、L0/L1 优先级、倾斜重力、近失重和 DashPass 发射台均按配置生效。
- `[Tool]` 预览在编辑器中实际绘制；属性可在 Inspector 修改；MCP 可读回场景与节点状态。
- 逐帧落地探针未复现持续真实法线反弹；摄像机在路线中无单帧跳变。

未确认/未完成：

- **未由用户亲自试玩**：手感、路线难度、落点容错、镜头舒适度、落地反弹主观感受均未验收。
- 未在接触前/投影前埋点；不能排除一帧外向速度或位置解穿透。
- 旧 `RouteLab` 平台没有 L0；旧路线的起跳漂移没有被本轮修改。
- 没有验证所有编辑器参数组合、所有区域重叠布局、极薄区域高速穿越或长时间远离原点。
- 截图中的分区定位使用了 `game_eval`，不能当作真实通关操作证据；真实通关证据是 `PLAYGROUND PASS: 46 checks` 的脚本输入路线。
