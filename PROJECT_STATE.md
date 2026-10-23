# Planet Lab：项目状态

最后核对：2026-10-23（P-002.3 试玩反馈：中文 Inspector 说明、平台碰撞体可编辑性修正）。本文件是唯一当前状态汇总；规范见 [AGENTS.md](AGENTS.md)，阶段说明见 [P0023.md](P0023.md)、[P0022.md](P0022.md)、[P002.md](P002.md)，实际验证记录见 [Tests/P0023-RESULTS.md](Tests/P0023-RESULTS.md)。

当前基线：分支 `master`、HEAD `2b03179`（用户已提交 `af80ce8 P002-3`、`2b03179 p002-3.2`）。工作区仍有未提交的 `.gitignore`、用户对 `Scenes/GravityPlayground.tscn` 中 Sphere 吸引半径的修改（应为 `138.84`），以及本轮中文说明/平台碰撞/测试/文档修改。**本轮没有创建提交**；不能把工作区等同于已验证提交。

## 项目概况与进度

目标及当前配置为 Godot **4.7.2 .NET**、C#、net8.0、Godot AI 4.3.0 的 2D 星球平台银河城核心机制原型。重点是不同地表朝向、局部重力与真实跨星球旅行，不是正式关卡制作。

- P-001 双星球原型已完成；用户曾亲自试玩“基本功能正常”，同时指出落地反弹。
- P-002 已完成下砸、特殊平台与跨星球路线；用户试玩后指出平台起跳被径向重力带偏、落地轻微反弹、空中不转向而落地/镜头大幅旋转。
- P-002.2 已完成 GravityLayer、固定方向区域、连续身体朝向与独立摄像机响应；历史 4.6.1 自动化 293 项通过，**尚未由用户逐项试玩验收**。
- P-002.3 已实现并提交：Godot 4.7.2 构建/回归、编辑器 `[Tool]` 预览、`GravityPlayground` 可玩灰盒。用户试玩后反馈“整体体验相比之前有改善”，但编辑器参数组织/中文说明和平台碰撞体编辑不符合关卡策划习惯。
- **P-002.3 编辑器易用性修正（2026-10-23 本轮）**：
  - `GravityRegion`、`Planet`、`DashPlatform`、`GoalZone` 增加中文只读使用说明与中文玩法分组；常用参数前置，外观/标签/调试后置；
  - `DashPlatform` 改为使用场景中真实 `CollisionShape2D` 子节点的 `RectangleShape2D.Size` 作为唯一碰撞尺寸；移除 `Size` 导出和运行时隐藏 `AddChild`；
  - 三个场景共 17 个手工平台已迁移为真实碰撞子节点；用户 Sphere 吸引半径修改保留；
  - 4.7.2 下 120/30 显示步频各 **354 项**检查通过；编辑器内改 Shape 尺寸后的运行时碰撞已验证；
  - **尚未由用户进行编辑器操作验收**。
- P-003、花生、贯通通道、战斗、正式剧情仍未获得开发授权；本轮未开始。

## 已实现功能及位置

| 系统 | 当前实现 | 位置 |
|---|---|---|
| 默认/测试地图 | 默认仍为 P-002 `RouteLab.tscn`；P-002.2 对照 `GravityLab.tscn`；P-002.3 可玩灰盒 `GravityPlayground.tscn` | `project.godot`、`Scenes/` |
| 可玩灰盒 | START 平直 L0 平台 → L1 弱重力 → L0 岛 → 同层下/右叠加 → 桥 → 倾斜发射台 → 下砸跨空间 → 胶囊 GOAL；R 重开；越界/失去重力自动重开；羽毛区 g=40 | `Scenes/GravityPlayground.tscn`、`Scripts/PlaygroundLevel.cs`、`Scripts/GoalZone.cs` |
| 编辑器预览 | `[Tool]` 绘制重力区域范围/方向/名称/强度；星球吸引边界/影响范围/方向采样箭头；平台外观严格按真实 `CollisionShape2D` 子节点的 Shape/Transform 绘制；参数/变换后刷新；多区域同时显示 | `Scripts/GravityRegion.cs`、`Scripts/Planet.cs`、`Scripts/DashPlatform.cs` |
| 策划中文说明 | 四个组件均有只读多行 `UsageNote` 中文说明 + 中文玩法分组；说明控制什么、单位、改变后的效果；不写入场景 | `Scripts/EditorHelp.cs`、各策划组件 |
| 平台碰撞可编辑 | 手摆平台使用真实、可保存的 `CollisionShape2D` 子节点；`RectangleShape2D.Size` 是唯一尺寸来源；平台提供只读派生 `Size` 供测试/绘制，不再导出 | `Scripts/DashPlatform.cs`、各 `.tscn` 平台节点 |
| GravityLayer | 非负整数、0 最高；每物理帧按位置收集有效源；只取最小 Layer；同层向量相加；较低优先级不参与；无源零重力 | `Scripts/GravitySource.cs`、`Scripts/GravitySelector.cs` |
| 固定方向重力区域 | 矩形局部范围、局部方向随区域旋转、Layer/强度/颜色可配置；无碰撞体，与地形解耦 | `Scripts/GravityRegion.cs` |
| 轮廓吸引重力 | 球形/胶囊吸引边界纳入统一 Layer；每星球 Layer、强度、范围可配；实体与吸引形状可分离 | `Scripts/Planet.cs`、`Scripts/SurfaceGeometry.cs` |
| 普通运动 | A/D 切向加减速，Space 沿实际支撑法线跳跃，空中保留世界速度和少量机身左右控制，高速可自然离地 | `Scripts/Player.cs` |
| 落地处理 | 首次建立支撑/支撑体切换时去掉法线速度、保留切向分量；不永久贴地 | `Scripts/Player.cs` |
| 身体与镜头 | 接地快速贴合支撑法线；空中连续转向有效重力；无/近零重力保持稳定；下砸锁定；摄像机独立较慢平滑 | `Scripts/Player.cs`、`Scripts/FollowCamera.cs` |
| 直线下砸 | 空中 J 发动，锁定发动瞬间身体朝下方向，禁用普通重力/方向输入，无时间/距离上限；结束不瞬时改视觉朝向 | `Scripts/Player.cs` |
| 实验平台 | 普通实体、易碎、穿越平台；穿越例外完全离开后恢复；R 重载恢复；三种行为保持不变，但碰撞来源改为场景子节点 | `Scripts/DashPlatform.cs`、`Scripts/Player.cs` |
| 实际旅行路线 | 正常跳跃到倾斜平台，起跳后下砸穿过特殊平台及无重力空间，撞到胶囊后恢复操作；没有目标识别或传送 | `Scenes/RouteLab.tscn`、`Scripts/RouteTests.cs` |
| 新关卡完成确认 | Goal Area 只检测 Player；到达显示 COMPLETE/GOAL REACHED；R 重开 | `Scripts/GoalZone.cs`、`Scripts/PlaygroundLevel.cs` |
| 调试与重置 | 所有有效区域/Layer/向量、最高优先级层、同层和、Applied、速度、接地、支撑法线、身体/摄像机角度；R 重载 | `Scripts/Lab.cs`、`Scripts/Player.cs` |

默认主场景仍为 `res://Scenes/RouteLab.tscn`；P-002.3 场景用 F6 打开。原 `Lab.tscn` 也使用当前控制器，不是冻结的 P-001 行为版本。

## 验证状态与证据

### 2026-10-23 Godot 4.7.2 实际执行（本轮）

环境：Godot 4.7.2-stable mono official、.NET SDK 10.0.100、net8.0；Godot 控制台 `D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。

```powershell
dotnet build NewGame.csproj -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 120
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 30
```

- 构建成功，**0 警告、0 错误**，输出 `.godot/mono/temp/bin/Debug/NewGame.dll`；编辑器实际加载中文说明和新的平台逻辑。
- 两个显示步频均通过 **354 项**，物理频率 120 Hz：

| 测试组 | 数量 | 说明 |
|---|---:|---|
| P-001 运动、跳跃、拖拽、双向旅行回归 | 104 | 球/胶囊绕行、发射、旅行、重置 |
| 落地、切向保留、稳定停留、连续空中朝向 | 68 | 16 组斜落地 + 重力朝向检查 |
| 下砸状态、方向、持续飞行、J 输入、薄墙 | 14 | 既有下砸回归 |
| 易碎/穿越/普通平台、重叠恢复、多层扫掠 | 26 | 原 20 + 6 项真实子碰撞/中文说明元数据 |
| GravityLayer、固定区域、零重力、角色/摄像机 | 73 | P-002.2 专项 |
| P-002 路线 | 14 | 正常跳跃→倾斜平台→立即下砸→胶囊 |
| P-002.3 GravityPlayground | 55 | 原 46 + 9 项平台子碰撞/中文说明/资源唯一性 |

- 日志：`.godot/test-results/p0022-*-{120,30}.log`、`p0023-playground-{120,30}.log`、`p0023-editor-final-{120,30}-console.log`（Git 忽略）；`^ERROR:|^FAIL` 无匹配。
- 详细记录见 [Tests/P0023-RESULTS.md](Tests/P0023-RESULTS.md)。

### 平台碰撞与中文说明的编辑器检查

- MCP 读回 `GravityPlayground` 的 11 个平台、`GravityLab` 的 `Deck`、`RouteLab` 的 5 个平台均拥有真实 `CollisionShape2D` 子节点；平台节点不再有 `Size` 导出/序列化。
- `StartDeck` 初始 Shape `RectangleShape2D.Size=(400,16)`；在编辑器内存改为 `(440,16)` 保存后运行，游戏内 Shape 读回 `(440,16)`，在旧范围外 `(-1710,-380)` 的物理查询命中 `StartDeck` 1 次；随后恢复 `(400,16)` 并保存。证明“编辑器改 Shape → 运行时碰撞变化”。
- 用户未提交的 Sphere 吸引半径 `138.84` 在迁移后仍保留（`resource_manage load` 读回）。
- 四个组件均有 `UsageNote` 中文只读字段；测试读取 `GetPropertyList()` 断言 `ReadOnly` 存在、`Storage` 不存在、中文玩法分组存在。实际 Inspector 排版/字体显示需用户操作验收（MCP 只能读属性元数据）。
- 编辑器截图（Git 忽略）：`.godot/test-results/p0023-editor-collision.png`、`p0023-editor-gravitylab-deck.png`、`p0023-editor-sphere.png`；能看到 Scene 树中的平台 → CollisionShape2D。
- 运行时截图（旧 P-002.3 场景证据仍有效）：`.godot/test-results/p0023-{spawn,float,sum,ramp,goal,feather}.png`；截图中的分区定位使用 `game_eval`，不是通关操作证据。

### 历史自动化（2026-10-22，Godot 4.7.2）

上一版 P-002.3 在 4.7.2 下 120/30 各 **339 项**通过（当时平台 20 项、Playground 46 项）；日志 `p0023-final-{120,30}-console.log`。该结果保留为历史基线，不代表本轮中文说明/平台碰撞修改已验证；本轮以 354 项为准。

### 历史自动化（2026-10-21，Godot 4.6.1）

代码提交 `204cb28` 上曾实际执行 293 项通过（104+68+14+20+73+14），显示步频 120/30，物理 120 Hz；详细记录见 [Tests/P0022-RESULTS.md](Tests/P0022-RESULTS.md)。这是历史证据，不能代替当前 4.7.2 结果。

### 人工验收与未验证项

- **P-001 用户反馈**：亲自试玩“基本功能正常”，同时指出落地反弹；不是逐项验收。
- **P-002 用户反馈**：平台起跳被径向重力带偏、落地轻微反弹、空中不转向而落地/镜头大幅旋转。
- **P-002.2 未验收**：GravityLayer、局部平台跳跃、固定重力/优先覆盖、同层零合力、角色/摄像机旋转、旧路线即时 J 都只有自动化证据。
- **P-002.3 用户试玩**：用户已试玩并反馈“整体体验相比之前有改善”，但同时指出编辑器仍不符合关卡策划习惯；本轮修正尚未由用户验收。
- **落地反弹**：旧探针在平甲板/球面接触后 13 帧记录到速度 0、外向法线 0、位置稳定；但探针读的是投影后状态，未在投影前埋点，不能宣布已解决。
- **摄像机突然旋转**：Godot 4.7.2 C# `Vector2.AngleTo` 经运行时探针确认有符号；旧 `FollowCamera` 数学正确，本轮未改相机逻辑；路线最大单物理帧变化 0.42°（120）/1.44°（30）。仍需试玩判断目标切换/边界/下砸结束/渲染卡顿。
- **本轮未验证**：旧外部场景若仍只有 `Size`、没有 `CollisionShape2D` 子节点，需要手工迁移；本仓库三个场景已迁移。Inspector 中文排版、字体和实际阅读体验需用户操作验收。

## 已确认设计规则

### GravityLayer（P-002.2 已实现，历史/本轮自动化通过，待试玩）

- 游戏物理以玩家直觉、环境可读性和规则一致性优先，不以真实物理模拟优先。
- 每个重力区域有自定义非负整数 `GravityLayer`，不是 Godot `collision_layer`，也不是 `Area2D.priority` 的照搬。
- 数值越小优先级越高，0 最高。
- 按角色当前实际位置实时建立有效区域集合；不依赖 Trigger 回调、最后进入区域或最后来源。
- 只取最小 Layer 的所有区域；同 Layer 重力向量相加；较低优先级（数值更大的）Layer 完全不参与。
- 最小 Layer 合力为零/近零时不回退较低优先级层，按零重力处理；不归一化零向量。
- 无有效重力区域时为零重力，保留惯性。
- 重力区域与实体地形碰撞解耦，可与地形形状不同，可相互重叠；重叠不报错、不互斥。
- 固定方向区域使用局部矩形范围与局部方向；球/胶囊轮廓吸引重力纳入同一规则。
- 第一轮只实现固定方向 + 轮廓吸引；目标点、专用零重力预设、任意多边形未实现。

### 角色、摄像机与下砸（P-002.2 已实现，历史/本轮自动化通过，待试玩）

- 接地时角色快速贴合实际支撑面；普通空中快速但连续适应当前有效重力。
- 重力切换时人物较快转向，摄像机独立、较慢平滑；两者响应分别可配置。
- 无/近零重力时身体与摄像机保持稳定，不根据不可靠微小合力旋转。
- 下砸仍锁定发动瞬间的身体脚下方向，期间忽略普通重力；结束不瞬时跳转视觉朝向。
- 视觉朝向不旋转角色节点或圆形碰撞体；物理 `UpDirection` 与视觉 `_visualUp` 分离。
- 空中左右控制继续使用身体朝向，因此平滑转向会同时影响操控方向；这是 P-002.2 接受的取舍。

### P-002.3 灰盒关卡配置规则（已实现，待试玩）

- START/STEP、BRIDGE 等普通平台用 L0 固定向下区域覆盖“平台 + 完整跳跃空间”，避免星球径向重力把普通跳跃拉偏；区域边界不要放在起跳/落点正中。
- L1 浮空区（260）与 L0 岛（550）用于观察“低 Layer 硬覆盖高 Layer”；L0 岛不是规则特例。
- SUM 区两个 L0 区域（向下 550 + 向右 300）重叠，合力 `(300,550)`；不同 Layer 不参与。
- LAUNCH 区 Direction 与倾斜发射台法线相反，使起跳后身体脚下方向指向胶囊；发射台使用现有 `DashPass` 平台，普通行走仍碰撞，下砸可穿过台面，方向仍由 `TryBeginDash` 锁定。
- 跨星球段不使用瞬移、隐藏传送、目标星球识别或新移动能力；下砸是唯一位移机制，且落地由胶囊实体碰撞接管。
- 终点是只检测 `Player` 的 Area；到达显示明确完成状态；R 始终可重开。失去全部重力 >4 秒或越过场景边界会自动重开，测试模式关闭该自动重开。
- FEATHER 区是可选近失重实验（L0 g=40），顶棚防止满跳飘出；不属于通关必经路径。

### 平台碰撞与策划说明（2026-10-23 长期规则）

- 手工摆放的平台必须在场景中有真实、可保存的 `CollisionShape2D` 子节点；`RectangleShape2D.Size` 是唯一碰撞尺寸来源。平台的 `Size` 只是只读派生值，不导出、不序列化。
- 平台外观按子碰撞的 Shape/Transform 绘制；普通、易碎、下砸穿越三种机制保持不变；不再运行时隐藏 `AddChild` 碰撞。
- 每个平台使用独立 Shape2D；复制平台后若共享资源，应在 Shape 菜单选择 Make Unique。编辑器会警告同父节点下的共享 Shape。
- 普通手工平台不得默认使用隐藏生成碰撞；如果将来需要程序生成，必须明确命名/标记为生成对象，并提供编辑器预览。
- 策划组件使用中文 `[ExportGroup]` 分组；最常用玩法参数前置，外观/标签/调试后置。
- 当前 Godot 4.7.2 C# 绑定未发现可用的 `[Tooltip]` 特性；统一使用 `[Export(PropertyHint.MultilineText)]` 的中文 `UsageNote`，由 `_ValidateProperty` 设置 `PropertyUsageFlags.ReadOnly` 并移除 `Storage`，保证可读、不可编辑、不写入场景。
- 说明必须写清“控制什么、单位、改变后的效果”；保留既有 C# 标识符和序列化属性名，不把标识符翻译成中文。

## 当前关键技术决策

- `GravitySelector` 是唯一 Layer 策略入口；`GravitySelection` 每帧复用，保留全部有效区域并单独列出参与同层求和的贡献。
- 重力源列表在场景初始化时从 `gravity_sources` 组收集；静态场景足够，运行时动态增删区域需显式 `RefreshGravitySources()`。
- 固定区域用 `Node2D` + 纯矩形包含，不创建 `Area2D`/碰撞体；方向相对区域自身。
- `Player` 用 `GravityDeadzone`（默认 1）把近零合力应用为零；原始合力和 Applied 都暴露给 HUD。
- 角色与摄像机分离：接地响应 24、空中响应 12，摄像机响应 5；均可在 Inspector 调整。
- 落地投影保留首次接地，并在支撑体变化时再次执行；不在弯曲胶囊每帧用探测法线投影，避免破坏既有绕行。
- P-002 路线的下砸起跳时机仍为**离地后立即按 J**；P-002.3 发射台通过重力方向对齐 + DashPass，让立即 J 和稍晚 J 都可工作。
- `PlaygroundLevel` 继承 `Lab`，复用 HUD/R 重置/测试入口，只增加 Goal 确认、越界/失重重开和 HUD 标题；`Lab.HudTitle` 让不同场景复用同一 HUD。
- `DashPlatform` 以子 `CollisionShape2D` 的 `RectangleShape2D.Size` 为唯一尺寸；`Size` getter 只读；`_Draw`/`OverlapsPlayer` 使用子 Shape 的变换；编辑器 0.2s 轮询刷新预览，不订阅 Shape.Changed（避免重复连接/替换资源断连）。
- 测试入口统一解析 Godot 4.7.2：显式 `-Godot` > `GODOT_EXE` > 已知 4.7.2 路径；不再默认 4.6.1。`Run-P0023.ps1` = P-002.2 六组回归（299 项）+ 新场景 55 项，共 354 项。

## 已知问题与限制

- 落地轻微反弹未被自动化复现为持续真实法线速度；未在投影前埋点，原始接触速度仍未知。等待用户在新场景复核，不能宣称已解决。
- 摄像机突然旋转未被本轮复现；角度符号数学已排除，当前候选是接地/空中目标切换、区域硬边界、下砸结束朝向与新支撑法线差异、渲染 delta 无上限。新场景的 L0 对齐与发射台对齐降低了目标跳变，但仍需试玩。
- 普通平台起跳漂移在**新场景**由 L0 区域解决；旧 `RouteLab` 的 Step1/Step2/LaunchPad 仍没有 L0，仍会漂。本轮没有改旧场景。
- 本轮平台迁移只覆盖仓库中的 `GravityPlayground.tscn`、`GravityLab.tscn`、`RouteLab.tscn`；任何外部旧场景若仍有 `Size` 属性而没有子碰撞，需要按新流程手工迁移。
- 平台 Shape 资源共享是编辑器操作风险；仓库场景已使用独立子资源，代码在编辑器中对同父平台的共享 Shape 发警告，但不会自动复制资源。
- 羽毛区的 g=40、顶棚高度、发射台角度/位置和桥宽度是灰盒调参值，需要试玩确认。
- 固定区域只有矩形；不支持任意轮廓、目标点重力、专用零重力预设或薄区域 swept 查询。
- 重力源列表静态缓存；运行时动态添加/删除区域需刷新，当前测试辅助会显式调用。
- 胶囊绕行仍有约 2 个物理帧的接触空隙（历史日志 2396/2400 帧接地，最大间隙约 0.107 px）；未添加土狼时间或输入缓冲。
- 下砸无时间/距离上限，错过目标需 R；极远坐标精度未验证。
- 自动测试使用 TestMode/脚本输入，不能代替用户手感验收；截图中的分区定位使用 `game_eval`，不是通关操作证据。
- 中文 Inspector 的元数据已由 `GetPropertyList()` 断言（存在、只读、无 Storage、中文分组），但实际排版/字体/阅读体验需要用户在 Inspector 中验收；MCP 不能截取 Inspector dock。
- 本轮工作区未提交；用户已有的 `.gitignore` 修改和 Sphere 半径修改均保留。

## 环境、MCP 与迁移验证

2026-10-23 实际核对：

| 项目 | 当前证据与边界 |
|---|---|
| 项目配置 | `NewGame.csproj` 为 `Godot.NET.Sdk/4.7.2`、`net8.0`；`project.godot` features 为 4.7 / C# / Forward Plus，默认仍 `RouteLab`，物理 120 Hz |
| 已安装插件 | `addons/godot_ai/plugin.cfg` 声明 4.3.0；`project.godot` 已启用该插件及 `_mcp_game_helper` autoload；插件源码已随用户提交纳入 Git |
| 实际连接 | MCP 返回项目 `E:/goDotProject/new-game/`、Godot `4.7.2-stable (official)`、插件/服务端 4.3.0，ready；会话 `new-game@b6f481ef83a04d38`；实际执行场景/节点写入、属性读回、截图、运行/停止 |
| 编译与程序集 | `dotnet build NewGame.csproj -p:UseSharedCompilation=false` 成功，0 警告 0 错误；编辑器可读回中文说明字段和新的平台逻辑；运行场景加载新程序集并实际执行 |
| 自动化 | 4.7.2 下 120/30 显示步频各 354 项通过；日志见上 |
| 迁移程度 | 构建、程序集加载、场景运行、自动化、编辑器检查均已实际通过；**用户编辑器操作验收未完成** |
| 测试入口 | `Tests/Resolve-Godot.ps1` 统一解析 4.7.2；`Run-Tests.ps1`、`Run-P002.ps1` 已去掉 4.6.1 默认；`Run-P0023.ps1` 为当前完整入口（354 项）。本机 PowerShell 默认脚本策略会阻止 `.ps1`，需用 `-ExecutionPolicy Bypass` |
| 插件版本管理 | `addons/godot_ai/` 已随 `af80ce8` 纳入版本管理；`addons/.godot_ai_update/` 是本地更新缓存，已被其自带 `.gitignore` 忽略；`.idea/`、`NewGame.csproj.old` 已加入根 `.gitignore`。未删除用户文件 |

## 本轮改动与 Git 状态

用户已提交的基线：

- `af80ce8 P002-3`：P-002.3 主成果、插件、文档、测试；
- `2b03179 p002-3.2`：`GravityPlayground.tscn` 更新；
- 合并提交 `d00b1de`、`3f2c38c` 等已在历史中。`README.md` 当前为空（用户提交 `3f2c38c Delete README.md content`），本轮未覆盖或恢复；入口以 AGENTS.md / PROJECT_STATE.md 为准。

本轮未提交修改：

- 新增：`Scripts/EditorHelp.cs`（中文只读说明辅助）；其 `.uid` 由 Godot 生成。
- 修改：`Scripts/DashPlatform.cs`（真实子碰撞、无运行时生成、中文说明）、`Scripts/GravityRegion.cs`、`Scripts/Planet.cs`、`Scripts/GoalZone.cs`（中文说明/分组/中文注释）、`Scripts/PlatformTests.cs`、`Scripts/PlaygroundTests.cs`（子碰撞/中文元数据检查）、`Scripts/Player.cs`、`Scripts/GravitySource.cs`、`Scripts/GravitySelector.cs`、`Scripts/SurfaceGeometry.cs`、`Scripts/FollowCamera.cs`、`Scripts/PlaygroundLevel.cs`（仅中文注释/说明，逻辑未改）。
- 修改：`Scenes/GravityPlayground.tscn`（11 个平台子碰撞，保留用户 Sphere 半径 138.84，移除平台 Size）、`Scenes/GravityLab.tscn`（Deck 子碰撞，移除 Size）、`Scenes/RouteLab.tscn`（5 个平台子碰撞，移除 Size）。
- 修改：`AGENTS.md`、`P0023.md`、`P0022.md`、`PROJECT_STATE.md`、`Tests/P0023-RESULTS.md`、`Tests/P0022-RESULTS.md`。
- 用户未提交的 `.gitignore` 修改保持原样。
- 没有提交；没有执行 reset/clean/checkout/stash 等会丢弃修改的 Git 操作。

**本轮没有提交。** 建议用户完成编辑器验收后再提交；提交前区分用户既有的 `.gitignore`/Sphere 半径修改与本轮代码/场景/文档成果。

## 下一步

1. 用户在 Godot 编辑器中验收：
   - 选中 `World/Terrain` 的平台，确认 Scene 树有真实 `CollisionShape2D`；改子 Shape 后运行，确认碰撞变化；
   - 选中重力区域/星球/终点/平台，确认中文分组和只读 `UsageNote` 可读；
   - 按需要新建普通平台（见 P0023.md 的操作示例）。
2. 按编辑器验收反馈调整中文措辞/分组或平台默认结构；不重新引入运行时隐藏碰撞。
3. 落地反弹/摄像机手感仍待试玩，不因本轮编辑器修正而标记为已解决。
4. P-003、花生、贯通通道、战斗、正式剧情需要新的明确授权；本轮完成即停止。
