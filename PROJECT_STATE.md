# Planet Lab：项目状态

最后核对：2026-10-22（P-002.3 工程维护、编辑器预览、可玩灰盒与 Godot 4.7.2 回归）。本文件是唯一当前状态汇总；规范见 [AGENTS.md](AGENTS.md)，阶段说明见 [P0023.md](P0023.md)、[P0022.md](P0022.md)、[P002.md](P002.md)，实际验证记录见 [Tests/P0023-RESULTS.md](Tests/P0023-RESULTS.md)。

本轮开始时分支 `master`、HEAD `cfac47e`。工作区已有用户保留的 `NewGame.csproj`、`project.godot`、`Scenes/GravityLab.tscn` 修改，以及未跟踪的 `addons/`、`.idea/`、`NewGame.csproj.old`；这些不属于本轮成果。本轮在其上新增/修改了 P-002.3 代码、场景、测试和文档，**尚未提交**；不能把工作区等同于历史测试基线。

## 项目概况与进度

目标及当前配置为 Godot **4.7.2 .NET**、C#、net8.0、Godot AI 4.3.0 的 2D 星球平台银河城核心机制原型。重点是不同地表朝向、局部重力与真实跨星球旅行，不是正式关卡制作。

- P-001 双星球原型已完成；用户曾亲自试玩“基本功能正常”，同时指出落地反弹。
- P-002 已完成下砸、特殊平台与跨星球路线；用户试玩后指出平台起跳被径向重力带偏、落地轻微反弹、空中不转向而落地/镜头大幅旋转。
- P-002.2 已完成 GravityLayer、固定方向区域、连续身体朝向与独立摄像机响应；历史 4.6.1 自动化 293 项通过，**尚未由用户试玩验收**。
- **P-002.3 本轮完成**：
  - Godot 4.7.2 .NET 实际构建、程序集加载、完整回归通过；
  - `GravityRegion` / `Planet` / `DashPlatform` 的 `[Tool]` 编辑器预览与 Inspector 分组；
  - 新可玩灰盒 [Scenes/GravityPlayground.tscn](Scenes/GravityPlayground.tscn)，可从 START 实走到胶囊终点；
  - 4.7.2 下 120/30 显示步频各 **339 项**检查通过；
  - **尚未由用户亲自试玩验收**。
- P-003、花生、贯通通道、战斗、正式剧情仍未获得开发授权；本轮未开始。

## 已实现功能及位置

| 系统 | 当前实现 | 位置 |
|---|---|---|
| 默认/测试地图 | 默认仍为 P-002 `RouteLab.tscn`；P-002.2 对照 `GravityLab.tscn`；P-002.3 可玩灰盒 `GravityPlayground.tscn` | `project.godot`、`Scenes/` |
| 可玩灰盒 | START 平直 L0 平台 → L1 弱重力 → L0 岛 → 同层下/右叠加 → 桥 → 倾斜发射台 → 下砸跨空间 → 胶囊 GOAL；R 重开；越界/失去重力自动重开；羽毛区 g=40 | `Scenes/GravityPlayground.tscn`、`Scripts/PlaygroundLevel.cs`、`Scripts/GoalZone.cs` |
| 编辑器预览 | `[Tool]` 绘制重力区域范围/方向/名称/强度；星球吸引边界/影响范围/方向采样箭头；平台碰撞足迹/名称/起跳标记；参数/变换后重绘；多区域同时显示 | `Scripts/GravityRegion.cs`、`Scripts/Planet.cs`、`Scripts/DashPlatform.cs` |
| GravityLayer | 非负整数、0 最高；每物理帧按位置收集有效源；只取最小 Layer；同层向量相加；较低优先级不参与；无源零重力 | `Scripts/GravitySource.cs`、`Scripts/GravitySelector.cs` |
| 固定方向重力区域 | 矩形局部范围、局部方向随区域旋转、Layer/强度/颜色可配置；无碰撞体，与地形解耦 | `Scripts/GravityRegion.cs` |
| 轮廓吸引重力 | 球形/胶囊吸引边界纳入统一 Layer；每星球 Layer、强度、范围可配；实体与吸引形状可分离 | `Scripts/Planet.cs`、`Scripts/SurfaceGeometry.cs` |
| 普通运动 | A/D 切向加减速，Space 沿实际支撑法线跳跃，空中保留世界速度和少量机身左右控制，高速可自然离地 | `Scripts/Player.cs` |
| 落地处理 | 首次建立支撑/支撑体切换时去掉法线速度、保留切向分量；不永久贴地 | `Scripts/Player.cs` |
| 身体与镜头 | 接地快速贴合支撑法线；空中连续转向有效重力；无/近零重力保持稳定；下砸锁定；摄像机独立较慢平滑 | `Scripts/Player.cs`、`Scripts/FollowCamera.cs` |
| 直线下砸 | 空中 J 发动，锁定发动瞬间身体朝下方向，禁用普通重力/方向输入，无时间/距离上限；结束不瞬时改视觉朝向 | `Scripts/Player.cs` |
| 实验平台 | 普通实体、易碎、穿越平台；穿越例外完全离开后恢复；R 重载恢复；发射台使用 DashPass 避免起跳瞬间被台面端部挡住 | `Scripts/DashPlatform.cs`、`Scripts/Player.cs` |
| 实际旅行路线 | 正常跳跃到倾斜平台，起跳后下砸穿过特殊平台及无重力空间，撞到胶囊后恢复操作；没有目标识别或传送 | `Scenes/RouteLab.tscn`、`Scripts/RouteTests.cs` |
| 新关卡完成确认 | Goal Area 只检测 Player；到达显示 COMPLETE/GOAL REACHED；R 重开 | `Scripts/GoalZone.cs`、`Scripts/PlaygroundLevel.cs` |
| 调试与重置 | 所有有效区域/Layer/向量、最高优先级层、同层和、Applied、速度、接地、支撑法线、身体/摄像机角度；R 重载 | `Scripts/Lab.cs`、`Scripts/Player.cs` |

默认主场景仍为 `res://Scenes/RouteLab.tscn`；P-002.3 场景用 F6 打开。原 `Lab.tscn` 也使用当前控制器，不是冻结的 P-001 行为版本。

## 验证状态与证据

### 2026-10-22 Godot 4.7.2 实际执行

环境：Godot 4.7.2-stable mono official、.NET SDK 10.0.100、net8.0；Godot 控制台 `D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。

```powershell
dotnet build NewGame.csproj -p:UseSharedCompilation=false
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 120
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-P0023.ps1 -RenderFps 30
```

- 构建成功，**0 警告、0 错误**，输出 `.godot/mono/temp/bin/Debug/NewGame.dll`；编辑器实际加载新程序集，MCP 可读回新脚本的导出属性。
- 两个显示步频均通过 **339 项**，物理频率 120 Hz：

| 测试组 | 数量 | 说明 |
|---|---:|---|
| P-001 运动、跳跃、拖拽、双向旅行回归 | 104 | 球/胶囊绕行、发射、旅行、重置 |
| 落地、切向保留、稳定停留、连续空中朝向 | 68 | 16 组斜落地 + 重力朝向检查 |
| 下砸状态、方向、持续飞行、J 输入、薄墙 | 14 | 既有下砸回归 |
| 易碎/穿越/普通平台、重叠恢复、多层扫掠 | 20 | 既有平台回归 |
| GravityLayer、固定区域、零重力、角色/摄像机 | 73 | P-002.2 专项 |
| P-002 路线 | 14 | 正常跳跃→倾斜平台→立即下砸→胶囊 |
| P-002.3 GravityPlayground | 46 | 新灰盒区域、落地探针、完整路线、终点 |

- 日志：`.godot/test-results/p0022-*-{120,30}.log`、`p0023-playground-{120,30}.log`、`p0023-final-{120,30}-console.log`（Git 忽略）；`^ERROR:|^FAIL` 无匹配。
- 详细记录见 [Tests/P0023-RESULTS.md](Tests/P0023-RESULTS.md)。

### 编辑器与场景检查

- MCP 会话实际返回项目 `E:/goDotProject/new-game/`、Godot 4.7.2、插件/服务端 4.3.0、ready；实际执行了场景创建、节点创建/改属性/挂脚本/保存、属性读回、2D 编辑器截图、运行/截图/停止。
- `GravityLab` 的 2D 编辑器截图实际显示：区域矩形、半透明范围、方向箭头、区域名/强度（`ShowLabel` 仍尊重原开关）、星球吸引边界/影响范围/方向采样箭头。
- `GravityPlayground` 实际运行截图显示：START/STEP、L1 浮空区、L0 岛、SUM 两个贡献者与 `(300,550)`、倾斜发射台、GOAL、羽毛区；HUD 的 Layer/来源/向量/Applied 与实际配置一致。
- 实际运行中把 `StartFlat_L0.GravityStrength` 从 550 改为 300 后，HUD 的 Active regions/raw/applied 立即变为 `(0,300)`，区域标签变为 `g 300`；停止后编辑器读回场景值仍为 550。证明导出参数实际驱动重力，不只是 Inspector 显示。
- 编辑器截图：`.godot/test-results/p0023-editor-gravitylab.png`、`p0023-editor-gravitylab-crop.png`；游戏截图：`p0023-{spawn,float,sum,ramp,goal,feather}.png`、`p0023-gravity300.png`（均 Git 忽略）。

### 历史自动化（2026-10-21，Godot 4.6.1）

代码提交 `204cb28` 上曾实际执行 293 项通过（104+68+14+20+73+14），显示步频 120/30，物理 120 Hz；详细记录见 [Tests/P0022-RESULTS.md](Tests/P0022-RESULTS.md)。这是历史证据，不能代替 4.7.2 本轮结果。

### 人工验收与未验证项

- **P-001 用户反馈**：亲自试玩“基本功能正常”，同时指出落地反弹；不是逐项验收。
- **P-002 用户反馈**：平台起跳被径向重力带偏、落地轻微反弹、空中不转向而落地/镜头大幅旋转。
- **P-002.2 未验收**：GravityLayer、局部平台跳跃、固定重力/优先覆盖、同层零合力、角色/摄像机旋转、旧路线即时 J 都只有自动化证据。
- **P-002.3 未验收**：新场景路线、分区难度、倾斜发射台时机、羽毛区、落地反弹和镜头舒适度都需要用户亲自试玩。
- **落地反弹**：新场景逐帧探针在平甲板/球面接触后 13 帧记录到速度 0、外向法线 0、位置稳定；但探针读的是投影后状态，未在投影前埋点，不能宣布已解决。
- **摄像机突然旋转**：核对 Godot 4.7.2 C# `Vector2.AngleTo` 为有符号 `atan2(cross,dot)`（运行时探针 Up→Left=-90°、Up→Right=+90°），旧 `FollowCamera` 数学正确，本轮未发现角度符号缺陷、未改该文件；新场景路线中最大单物理帧相机变化 0.42°（120）/1.44°（30），发射台 -15° 连续收敛。仍需试玩判断目标切换/区域边界/下砸结束/渲染卡顿是否造成主观突然感。
- **未充分验证**：任意摄像机/身体响应参数、所有区域重叠布局、极薄区域高速穿越、复杂编辑器组合、长时间远离原点、接触前逐帧速度。

## 已确认设计规则

### GravityLayer（P-002.2 已实现，历史/本轮自动化通过，待试玩）

- 游戏物理以玩家直觉、环境可读性和规则一致性优先，不以真实物理模拟优先。
- 每个重力区域有自定义非负整数 `GravityLayer`，不是 Godot `collision_layer`，也不是 `Area2D.priority` 的照搬。
- 数值越小优先级越高，0 最高。
- 按角色当前实际位置实时建立有效区域集合；不依赖 Trigger 回调、最后进入区域或最后来源。
- 只取最小 Layer 的所有区域；同 Layer 重力向量相加；较低优先级（数值更大）的 Layer 完全不参与。
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

### P-002.3 灰盒关卡配置规则（本轮实现，待试玩）

- START/STEP、BRIDGE 等普通平台用 L0 固定向下区域覆盖“平台 + 完整跳跃空间”，避免星球径向重力把普通跳跃拉偏；区域边界不要放在起跳/落点正中。
- L1 浮空区（260）与 L0 岛（550）用于观察“低 Layer 硬覆盖高 Layer”；L0 岛不是规则特例。
- SUM 区两个 L0 区域（向下 550 + 向右 300）重叠，合力 `(300,550)`；不同 Layer 不参与。
- LAUNCH 区 Direction 与倾斜发射台法线相反，使起跳后身体脚下方向指向胶囊；发射台使用现有 `DashPass` 平台，普通行走仍碰撞，下砸可穿过台面，方向仍由 `TryBeginDash` 锁定。
- 跨星球段不使用瞬移、隐藏传送、目标星球识别或新移动能力；下砸是唯一位移机制，且落地由胶囊实体碰撞接管。
- 终点是只检测 `Player` 的 Area；到达显示明确完成状态；R 始终可重开。失去全部重力 >4 秒或越过场景边界会自动重开，测试模式关闭该自动重开。
- FEATHER 区是可选近失重实验（L0 g=40），顶棚防止满跳飘出；不属于通关必经路径。

### 编辑器可编辑性与预览（P-002.3 已实现，编辑器实测）

- 关键参数在 Inspector 直接编辑；`GravityRegion`、`Planet`、`DashPlatform` 均有 `ExportGroup`，既有属性名保持。
- 重力类型由节点类别表达（`GravityRegion` = 固定方向矩形，`Planet` = 球形/胶囊轮廓），没有额外的 Type 枚举；不把底层算法拆成多余工具。
- `GravityRegion` 的 `[Tool]` 预览与 `TryGetGravity` 使用同一 `Size` 钳制与半矩形判定；`Planet` 方向箭头直接采样 `GravityAt`；`DashPlatform` 编辑器矩形与运行时 `RectangleShape2D(Size)` 一致。
- 预览会随属性 setter、节点变换和（星球）吸引 Shape 变化重绘；编辑器不生成碰撞/子节点，不把运行时分组写进场景。
- `GravityRegion` 不再强制 `ZIndex=-1`，区域按树序绘制在星球之上；若放到星球之前则会一起被盖住。
- 允许通过移动/旋转/参数调整；平台保持单位缩放，星球用 Shape 尺寸而非节点缩放。

## 当前关键技术决策

- `GravitySelector` 是唯一 Layer 策略入口；`GravitySelection` 每帧复用，保留全部有效区域并单独列出参与同层求和的贡献。
- 重力源列表在场景初始化时从 `gravity_sources` 组收集；静态场景足够，运行时动态增删区域需显式 `RefreshGravitySources()`。
- 固定区域用 `Node2D` + 纯矩形包含，不创建 `Area2D`/碰撞体；方向相对区域自身。
- `Player` 用 `GravityDeadzone`（默认 1）把近零合力应用为零；原始合力和 Applied 都暴露给 HUD。
- 角色与摄像机分离：接地响应 24、空中响应 12，摄像机响应 5；均可在 Inspector 调整。
- 落地投影保留首次接地，并在支撑体变化时再次执行；不在弯曲胶囊每帧用探测法线投影，避免破坏既有绕行。
- P-002 路线的下砸起跳时机仍为**离地后立即按 J**；P-002.3 发射台通过重力方向对齐 + DashPass，让立即 J 和稍晚 J 都可工作。
- `PlaygroundLevel` 继承 `Lab`，复用 HUD/R 重置/测试入口，只增加 Goal 确认、越界/失重重开和 HUD 标题；`Lab.HudTitle` 让不同场景复用同一 HUD。
- 测试入口统一解析 Godot 4.7.2：显式 `-Godot` > `GODOT_EXE` > 已知 4.7.2 路径；不再默认 4.6.1。`Run-P0023.ps1` = P-002.2 六组回归 + 新场景 46 项。

## 已知问题与限制

- 落地轻微反弹未被自动化复现为持续真实法线速度；未在投影前埋点，原始接触速度仍未知。等待用户在新场景复核，不能宣称已解决。
- 摄像机突然旋转未被本轮复现；角度符号数学已排除，当前候选是接地/空中目标切换、区域硬边界、下砸结束朝向与新支撑法线差异、渲染 delta 无上限。新场景的 L0 对齐与发射台对齐降低了目标跳变，但仍需试玩。
- 普通平台起跳漂移在**新场景**由 L0 区域解决；旧 `RouteLab` 的 Step1/Step2/LaunchPad 仍没有 L0，仍会漂。本轮没有改旧场景。
- 羽毛区的 g=40、顶棚高度、发射台角度/位置和桥宽度是灰盒调参值，需要试玩确认。
- 固定区域只有矩形；不支持任意轮廓、目标点重力、专用零重力预设或薄区域 swept 查询。
- 重力源列表静态缓存；运行时动态添加/删除区域需刷新，当前测试辅助会显式调用。
- 胶囊绕行仍有约 2 个物理帧的接触空隙（历史日志 2396/2400 帧接地，最大间隙约 0.107 px）；未添加土狼时间或输入缓冲。
- 下砸无时间/距离上限，错过目标需 R；极远坐标精度未验证。
- 自动测试使用 TestMode/脚本输入，不能代替用户手感验收；截图中的分区定位使用 `game_eval`，不是通关操作证据。
- `addons/` 仍是未跟踪状态；本轮未提交任何内容。插件更新缓存 `addons/.godot_ai_update/` 由插件自带 `.gitignore`（`*`）忽略；插件源码是否随项目提交需用户决定。

## 环境、MCP 与迁移验证

2026-10-22 实际核对：

| 项目 | 当前证据与边界 |
|---|---|
| 项目配置 | `NewGame.csproj` 为 `Godot.NET.Sdk/4.7.2`、`net8.0`；`project.godot` features 为 4.7 / C# / Forward Plus，默认仍 `RouteLab`，物理 120 Hz |
| 已安装插件 | `addons/godot_ai/plugin.cfg` 声明 4.3.0；`project.godot` 已启用该插件及 `_mcp_game_helper` autoload；addons 未跟踪 |
| 实际连接 | MCP 返回项目 `E:/goDotProject/new-game/`、Godot `4.7.2-stable (official)`、插件/服务端 4.3.0，ready；会话 `new-game@b6f481ef83a04d38`；本轮实际执行场景/节点写入、属性读回、截图、运行/停止 |
| 编译与程序集 | `dotnet build NewGame.csproj -p:UseSharedCompilation=false` 成功，0 警告 0 错误；编辑器可读回新 C# 脚本导出属性；运行场景加载新程序集并实际执行 |
| 自动化 | 4.7.2 下 120/30 显示步频各 339 项通过；日志见上 |
| 迁移程度 | 构建、程序集加载、场景运行、自动化、编辑器检查均已实际通过；**用户试玩验收未完成** |
| 测试入口 | `Tests/Resolve-Godot.ps1` 统一解析 4.7.2；`Run-Tests.ps1`、`Run-P002.ps1` 已去掉 4.6.1 默认；新增 `Run-P0023.ps1`。本机 PowerShell 默认脚本策略会阻止 `.ps1`，需用 `-ExecutionPolicy Bypass`；这是环境限制，不是项目错误 |
| 插件版本管理 | `addons/godot_ai/` 是恢复开发环境所需的插件源码（含 LICENSE/uid）；`addons/.godot_ai_update/` 是本地更新缓存，已被其自带 `.gitignore` 忽略；`.idea/`、`NewGame.csproj.old` 已加入根 `.gitignore`。未提交、未删除用户文件 |

## 本轮改动与 Git 状态

本轮新增/修改（未提交）：

- 新增：`Scenes/GravityPlayground.tscn`、`Scripts/GoalZone.cs`、`Scripts/PlaygroundLevel.cs`、`Scripts/PlaygroundTests.cs`、`Tests/Resolve-Godot.ps1`、`Tests/Run-P0023.ps1`、`Tests/P0023-RESULTS.md`、`P0023.md`。
- 修改：`Scripts/GravityRegion.cs`、`Scripts/Planet.cs`、`Scripts/DashPlatform.cs`（`[Tool]` 预览/Inspector）、`Scripts/Lab.cs`（`HudTitle`）、`Tests/Run-Tests.ps1`、`Tests/Run-P002.ps1`（Godot 路径解析）、`.gitignore`（`.idea/`、`.vs/`、`NewGame.csproj.old`）、`README.md`、`AGENTS.md`、`P0022.md`、`Tests/P0022-RESULTS.md`、`Tests/RESULTS.md`、`PROJECT_STATE.md`。
- 生成了新脚本的 `.uid` 文件（`GoalZone.cs.uid`、`PlaygroundLevel.cs.uid`、`PlaygroundTests.cs.uid`），应随脚本纳入版本管理。

用户保留的既有改动（不属于本轮）：

- `NewGame.csproj`（4.7.2）、`project.godot`（4.7/插件/autoload）、`Scenes/GravityLab.tscn`（4.7 序列化 + `ShowLabel=false` 等）、未跟踪的 `addons/`、`.idea/`、`NewGame.csproj.old`。
- 本轮没有保存/覆盖 `GravityLab.tscn`，没有清理这些内容。`GravityLab` 的 RegionA 当前读回仍为 `GravityStrength=550`、`ShowLabel=false`，与磁盘一致；此前聊天提到的未保存 790 不在当前编辑器中。

**没有提交。** 建议用户试玩验收后再决定提交分组：① 4.7.2 迁移 + 插件 + 测试入口；② P-002.3 编辑器预览 + 新场景 + 测试；③ 文档。提交前先区分用户既有迁移改动与 P-002.3 成果。

## 下一步

1. 用户亲自试玩 [Scenes/GravityPlayground.tscn](Scenes/GravityPlayground.tscn)，重点判断普通平台跳跃、落地、镜头旋转、倾斜发射台时机、路线难度和终点确认。
2. 按试玩反馈调场景参数/区域边界；若落地或镜头问题可复现，再按逐帧证据定位，不盲目调阻尼/速度。
3. P-002.3 手感验收前，不把落地反弹/摄像机问题标记为已解决；旧 `RouteLab` 的漂移仍作为已知问题。
4. P-003、花生、贯通通道、战斗、正式剧情需要新的明确授权；本轮完成即停止。
