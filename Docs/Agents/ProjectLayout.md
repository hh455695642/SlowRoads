# 项目目录与开发约定

## 当前目录

```text
Assets/
  Scenes/SlowRoadsRoadDemo.unity        唯一业务开发场景（保留原 GUID）
  Settings/                           Unity 模板 URP 全局设置，保持已有引用
  SlowRoads/
    Runtime/
      Debug/                          场景调试装配、观察者、相机、IRouteSource
      Generation/Random/              Alea 随机数
      Generation/Heightfield/         Hills Pl 高度场与 tile 缓存
      Generation/Midline/             Se/B7、精链/属性、发布快照、米制索引与重放
      Generation/Road/                D/Nl 纯网格、版本/ready 消费、属性 overlay 与有限段构建
      Generation/Spatial/             Ca tile/segment 索引、原语义查询与显式退役
      Generation/Streaming/           Ni顺序分步、xi三对象轮转、发布/重放与驻留调度
      Scene/Road/                     三Mesh上传/LOD/释放、流式装配、唯一HillsSceneSettings
    Editor/Debug/                     调试 Inspector，无顶层菜单
    Settings/Hills/                    唯一HillsSceneSettings.asset（种子/道路/季节/观察/性能）
    Tests/EditMode/                    原版数值对照测试
    Art/Debug/Materials/               观察方块材质
    Art/Reference/SlowRoads/           临时参考素材，按原分类保存
      Models/ Textures/ Materials/ Prefabs/
    Rendering/Shaders/Legacy/          已有参考树 Shader（材质仍依赖）
    Rendering/Shaders/Road/            Hills 路面表面合成与 alpha clip
Docs/
  Agents/                             开发契约、阶段状态、整理与验证记录
  Assets/                             资产审计报告、统一清单、只读复查脚本
  ReverseEngineering/                 原版证据与历史差异审计
    RoadAudit/Fixtures/Heightfield/    从本地 app.asar 校验后生成的确定性样本
    RoadAudit/Fixtures/RoadMesh/       原 D/Nl 冻结输入/全数组/续状态及 schema
                                      路面材质来源身份与独立表面算术样本
    RoadAudit/Fixtures/Streaming/      原 Se/B7→Ca→xi 正常/快速输入 trace、负坐标探针及schema
```

SampleScene、模板教学资源及未接入的音频、字体、UI 图标已移除。原 SampleSceneProfile 被 PC/Mobile 管线引用，故保留并更名 SceneVolumeProfile，GUID 不变；已失效的输入配置注册已清除。参考模型/场景贴图以 Docs/Assets/HillsAssetManifest.json 为准。禁止重新导入车辆或其他场景资源。非运行资料放 Docs，不放 Assets 或 Resources。

## 当前场景与配置

- Runtime/Debug 保留主场景装配、IRouteSource、StreamingRoadRouteSource、RouteObserver、ObserverCamera；它们只消费持续道路，没有有限预览或备用分支。
- 唯一业务配置：Settings/Hills/HillsSceneSettings.asset。包含五种道路类型、单一seed、视距、季节、运动/相机、三族四季材质直接引用及实际调度/回收参数；保留原GUID。类型派生P3路缘宽度及P4几何/越野语义，不新增独立半宽或surface开关。选择类型后在原主场景重新生成。
- Editor/Debug 只保留 HillsDebugSessionEditor，提供中文设置、暂停/继续、单步和重新生成。烘焙/Provision、灰模、测试预览、性能诊断开关已删除。
- 已删除独立HillsDebugSettings、HillsRoadStreamingSettings（重命名并合并）、HillsRoadMaterialSettings、HillsHeightfieldSettings及旧实例；删除Runtime/Configuration、Settings/Debug、Art/Debug/RoadPreview空目录及预览专用占位路面材质。
- Rendering/Shaders/Legacy的树Shader仍被八套参考树材质使用。原版对照fixture与纯算法测试在Tests/Docs，不进入用户场景配置；旧P6场景探针按用户要求删除，历史结果保留在原报告。
- 运行程序集不读Docs；Generation保持无Unity引用，Scene/Road提供唯一配置类型和表现边界。没有新增Manager、兼容Settings或平行场景。

## 清理状态（2026-10-28）

P6（2026-10-29）验收结果统一记录于 `Docs/ReverseEngineering/RoadAudit/P6_道路专项验收记录.md`；原 `p6-scene-probe.cs` 已在后续清退中删除，仅保留历史结果。主场景与生产目录未改写；道路功能/有限长跑已验证，路基混合求值及设备验收缺口尚未关闭。

- 已删除 316 个未使用资源文件（不计 .meta，约 66.4 MiB）：未接入的音频、字体/许可证、UI 图标与 SampleScene。
- 已删除旧根目录算法总结、全量资产清单、两份旧验证文档、重复 JSON、一次性文本迁移脚本及可重新生成的 IDE 项目文件。有效资产来源与验证信息已合并到 HillsAssetManifest.json。
- 清理后验证：主场景帧 9728 → 14126，里程 295.2654 → 423.2325 米，路径窗口 152 节点，保持一台摄像机；资产序列化引用检查无缺失项，compilationFailed=false。已退出播放并恢复后台运行设置。
- 剩余待清理：`.workbuddy`、`Docs/Agents/Artifacts/HillsDebugPreview.png`、`Temp/BeforeHillsAssetAudit`、`Temp/BeforeHillsCleanup`、`Temp/HillsAssetAudit`。内容是旧截图和重复备份/暂存文件，约 416 MiB，不属于运行依赖。两次文件系统删除（批量递归、路径核验后的逐文件删除）均被自动审批策略拦截，未执行，不能视为已清理。不要误删 Unity 其余 Temp 文件。
- P1 Play 验证产生的 `Temp/HillsP1Verification.png` 已由 `.gitignore` 排除；单文件删除也被自动审批策略拦截，待清理。不要为清理它改动 Unity 其他 Temp 文件。

## Legacy 清退状态与工程落点

- 当前（2026-10-29）正式道路数据源为 P5 `StreamingRoadRouteSource`。已删除 LegacyRoadRouteSource、NoiseHeightmap、RoadMidline（含 RoadNode/RoadGenConfig）、SlowRoadsRoad，以及 Debug SO 的 seed/road/aheadNodes/behindNodes/debugGizmos/roadMaterial/useStreaming；SO 原位重序列化并保留 GUID。后续按用户要求删除有限预览及其开关，唯一配置见上文。完整清退验证见 P5 记录第11节。
- 有限预览、材质烘焙及开发诊断入口已按用户本轮要求删除；自动回归在Tests中直接调用正式内核，不恢复面向用户的验收工具。
- 一旦新实现通过验收且引用迁移完毕，需在**同一开发批次**删除失去作用的旧类型、配置、资产和开关；不保留空壳适配器或注释备份。若有效引用或用户改动阻塞删除，应在交付中逐项列出，不得标记清退完成。
- 具体强制流程、Git 安全、来源证据保护、GUID 审计及回归门槛由 `HillsAgents.md` 的“旧实现清退与增量开发门禁”统一规定；本文件只记录实际目录与当前迁移状态，避免两处维护重复规则。

## 后续模块落点（实现时再建，禁止空类占位）

| 内容 | 目录 | 责任 agent |
|---|---|---|
| Seed/Alea、高度查询与缓存 | Runtime/Generation/Random、Heightfield | Random & Heightfield |
| 粗/精路径、道路附属属性 | Runtime/Generation/Midline | Midline |
| 路径索引、预算队列、分块生命周期 | Runtime/Generation/Spatial、Streaming | Spatial Index & Streaming |
| 路面/远景/近路几何 | Runtime/Generation/Road、Terrain | Terrain & Road Geometry |
| 树群、农田、墙、桥、路边物件 | Runtime/Generation/Contents/具体模块 | Hills Contents |
| 非调试装配与状态 | Runtime/Scene | Coordinator |
| Feature/Pass/实例 GPU 管理代码 | Rendering/Features、Passes、Instances | URP Rendering |
| HLSL/Compute | Rendering/Shaders/具体对象、Compute | URP Rendering |
| 配置类型 / 配置实例 | Runtime/Configuration / Settings/Hills | 对应模块所有者 |
| 自有资产（S2） | Art/Owned/Models、Textures、Materials、Prefabs | Assets |
| 编辑器工具 | Editor/具体模块 | 对应模块所有者 |
| 算法对照测试 | Tests/EditMode/具体模块（独立测试程序集） | Debug & Verification |
| 生命周期/播放测试 | Tests/PlayMode（独立测试程序集） | Debug & Verification |

## 依赖与命名

- Runtime 不引用 UnityEditor、Editor 工具或测试程序集；Editor 可引用 Runtime。生成算法不引用 Cube、相机、输入和车辆。
- 新模块以 SlowRoads 命名空间为根，按职责子命名空间分组。已有类型迁移仅移动文件保留类型名和 GUID，避免无关序列化迁移。
- 一个可挂载 MonoBehaviour / ScriptableObject 或自定义 Inspector 一个同名文件；接口与旧适配器分开。
- Legacy/Road 不得增加生产能力或新调用方，也不宣称原版等价。P5 已是默认正式实现；迁移其旧诊断能力和序列化引用后，必须删除不再必要的 Legacy 类型、配置与入口；存在阻塞则记录原因及解除条件，不能永久并行维护。
- 配置/材质使用序列化引用，不依赖硬编码 Resources.Load、全局 Find 或动态路径查找。新资源不要直接散落在 Assets/SlowRoads 根目录。
- ScriptableObject 类型与实例职责分离；Debug 专用类型允许留在 Runtime/Debug。字段改名用 FormerlySerializedAs，类型/程序集改名明确迁移策略。
- Debug 仍在默认程序集；Generation 保持 noEngineReferences 隔离，Scene/Road 单独使用 SlowRoads.RoadPresentation。只为已使用的边界添加 asmdef，禁止为目录形式拆出大量空程序集。

## agent 工作闭环

1. Coordinator 确认阶段、来源符号与文件写入范围，记录依赖是否已完成。
2. 实现 agent 先定义输入/输出、seed 调用顺序、坐标与所有权；有证据缺口时标明，不用近似填补后宣称等价。
3. Rendering agent 保留 CPU 数据语义，将实例剔除与提交独立实现，记录 RenderPassEvent、带宽、variant 和降级策略。
4. Verification agent 按同 seed 的参考输出对照，测试负坐标、tile/段边界、回退、回收及多次启动。视觉检查、数值对照、移动设备性能是不同验收项。
5. 每次交付必须核查本批次替代范围内的 Legacy，先迁移引用、保留有效 GUID，再实际删除已无用途的旧实现，验证编译、相关测试、场景和 Prefab 无缺失脚本；未清理项必须报告阻塞。禁止通过备份 C# 到 Assets 内保留重复可编译代码。完整门禁见 `HillsAgents.md`。

## 本次清理边界

删除旧树/道具生成菜单实现、两个场景创建菜单实现、旧 Demo/VehicleController/CameraRig、未使用草地实验脚本与 Shader、无引用且与 summer_grasses.png 哈希相同的 Resources 草贴图，以及 HillsAgentDebug 场景。

目录整理时保留已有树 Shader、材质和 Prefab；后续资产审计已进一步裁剪为 Hills 八种 OBJ 树的引用链，详情见 Docs/Assets/HillsAssetAudit.md。在 2026-10-28 的清理批次中曾保留三个旧道路算法脚本供当时调试使用；自 P5 默认接管后，这些脚本及旧适配入口应列为下一次清退审计候选，不代表永久保留。原版摘录与历史审计报告保持证据原貌。
