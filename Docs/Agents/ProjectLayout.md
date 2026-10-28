# 项目目录与开发约定

## 当前目录

```text
Assets/
  Scenes/SlowRoadsRoadDemo.unity        唯一业务开发场景（保留原 GUID）
  Settings/                           Unity 模板 URP 全局设置，保持已有引用
  SlowRoads/
    Runtime/
      Debug/                          场景调试装配、观察者、相机、IRouteSource
      Configuration/                  Hills 生成配置类型
      Generation/Random/              Alea 随机数
      Generation/Heightfield/         Hills Pl 高度场与 tile 缓存
      Generation/Midline/             Se/B7、精链/属性、发布快照、米制索引与重放
      Generation/Road/                D/Nl 纯网格、版本/ready 消费、属性 overlay 与有限段构建
      Scene/Road/                     Unity Mesh 上传/LOD/释放、材质 SO 与有限预览数据，独立 RoadPresentation 程序集
      Legacy/Road/                    旧高度场/中线/路面预览、旧道路适配器
    Editor/Debug/                     调试 Inspector，无顶层菜单
    Settings/Debug/                    调试 ScriptableObject 实例
    Settings/Hills/                    Hills 高度场及道路材质配置实例
    Tests/EditMode/                    原版数值对照测试
    Art/Debug/Materials/               Cube/道路占位材质
    Art/Reference/SlowRoads/           临时参考素材，按原分类保存
      Models/ Textures/ Materials/ Prefabs/
    Rendering/Shaders/Legacy/          已有参考树 Shader（材质仍依赖）
    Rendering/Shaders/Road/            Hills 路面表面合成与 alpha clip
    Art/Debug/RoadPreview/             P4 生成的有限两段 Mesh 与路线资产（非新场景）
Docs/
  Agents/                             开发契约、阶段状态、整理与验证记录
  Assets/                             资产审计报告、统一清单、只读复查脚本
  ReverseEngineering/                 原版证据与历史差异审计
    RoadAudit/Fixtures/Heightfield/    从本地 app.asar 校验后生成的确定性样本
    RoadAudit/Fixtures/RoadMesh/       原 D/Nl 冻结输入/全数组/续状态及 schema
                                      路面材质来源身份与独立表面算术样本
```

SampleScene、模板教学资源及未接入的音频、字体、UI 图标已移除。原 SampleSceneProfile 被 PC/Mobile 管线引用，故保留并更名 SceneVolumeProfile，GUID 不变；已失效的输入配置注册已清除。参考模型/场景贴图以 Docs/Assets/HillsAssetManifest.json 为准。禁止重新导入车辆或其他场景资源。非运行资料放 Docs，不放 Assets 或 Resources。

## Debug 文件保留依据

- Runtime/Debug 的五个文件：HillsDebugSession 装配主场景；HillsDebugSettings 提供 SO 配置；IRouteSource 定义轨迹接口；RouteObserver 移动 Cube；ObserverCamera 跟随。这些不是废弃测试脚本。
- Editor/Debug/HillsDebugSessionEditor 提供仍在使用的暂停、单步、重置与状态显示。
- Settings/Debug 的配置和 Art/Debug 的两个材质由主场景/装配器直接引用。
- Legacy/Road 的四个文件仍是现有路线提供者，正式生成算法替换并验收后才能移除。
- Generation 的 Alea、Hills Pl 已通过 P0/P1 限定范围对照，独立程序集和 12 项 EditMode 测试保持隔离，尚未挂接主场景；`Settings/Hills` 的 SO 资产使用修正后的 style3/detail2 示例参数，验收记录统一见 HillsAgents.md，不能替代 `roadStyle` 判定。
- Generation/Road 的 D/Nl 通过 P4 数据验收；P4.1–P4.7 的 Scene/Road 只提供调用式上传器。Tests/EditMode/Road 与 RoadUnity 分别验证纯算法和真实 Unity 边界；后者是引用 Generation、RoadPresentation 与共用 fixture 读取器的实际测试程序集。
- P4 材质补充后，Scene/Road 还保存 HillsRoadMaterialSettings / HillsRoadFinitePreview 类型；Settings/Hills 保存实例，Materials/Road 保存 12 套临时参考材质。Editor/Debug 的 HillsRoadMaterialAuthoring 显式调用 P4 Builder/Uploader 生成 Art/Debug/RoadPreview 资产；运行时不引用 Docs。有限模式复用原 Debug 装配，不实现 P5 调度。
- 逆向摘录包含用于对照的其他场景片段，属于原始证据，不能按运行资产清理规则裁剪。仅保留当前权威资产清单、报告与复查脚本，旧截图和一次性验证快照不长期堆积。
- Library、Unity 自身 Temp 文件、Logs、UserSettings、Packages 是工具缓存/配置或依赖，不能在编辑器运行时当作无用业务代码清空。IDE 的 sln/csproj 为自动生成产物，清理后需要时由 Unity 重建。

## 清理状态（2026-10-28）

- 已删除 316 个未使用资源文件（不计 .meta，约 66.4 MiB）：未接入的音频、字体/许可证、UI 图标与 SampleScene。
- 已删除旧根目录算法总结、全量资产清单、两份旧验证文档、重复 JSON、一次性文本迁移脚本及可重新生成的 IDE 项目文件。有效资产来源与验证信息已合并到 HillsAssetManifest.json。
- 清理后验证：主场景帧 9728 → 14126，里程 295.2654 → 423.2325 米，路径窗口 152 节点，保持一台摄像机；资产序列化引用检查无缺失项，compilationFailed=false。已退出播放并恢复后台运行设置。
- 剩余待清理：`.workbuddy`、`Docs/Agents/Artifacts/HillsDebugPreview.png`、`Temp/BeforeHillsAssetAudit`、`Temp/BeforeHillsCleanup`、`Temp/HillsAssetAudit`。内容是旧截图和重复备份/暂存文件，约 416 MiB，不属于运行依赖。两次文件系统删除（批量递归、路径核验后的逐文件删除）均被自动审批策略拦截，未执行，不能视为已清理。不要误删 Unity 其余 Temp 文件。
- P1 Play 验证产生的 `Temp/HillsP1Verification.png` 已由 `.gitignore` 排除；单文件删除也被自动审批策略拦截，待清理。不要为清理它改动 Unity 其他 Temp 文件。

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
- Legacy 仅维持调试可运行，不添加生产功能、不宣称算法等价。正式替代实现验证通过后，替换适配器并删除失去引用的 Legacy 文件。
- 配置/材质使用序列化引用，不依赖硬编码 Resources.Load、全局 Find 或动态路径查找。新资源不要直接散落在 Assets/SlowRoads 根目录。
- ScriptableObject 类型与实例职责分离；Debug 专用类型允许留在 Runtime/Debug。字段改名用 FormerlySerializedAs，类型/程序集改名明确迁移策略。
- Legacy/Debug/Configuration 仍在默认程序集；Generation 保持 noEngineReferences 隔离，Scene/Road 单独使用 SlowRoads.RoadPresentation。只为已使用的边界添加 asmdef，禁止为目录形式拆出大量空程序集。

## agent 工作闭环

1. Coordinator 确认阶段、来源符号与文件写入范围，记录依赖是否已完成。
2. 实现 agent 先定义输入/输出、seed 调用顺序、坐标与所有权；有证据缺口时标明，不用近似填补后宣称等价。
3. Rendering agent 保留 CPU 数据语义，将实例剔除与提交独立实现，记录 RenderPassEvent、带宽、variant 和降级策略。
4. Verification agent 按同 seed 的参考输出对照，测试负坐标、tile/段边界、回退、回收及多次启动。视觉检查、数值对照、移动设备性能是不同验收项。
5. 每次交付更新状态与未完成项；迁移/删除先检查引用，保留 GUID，验证场景和 Prefab 无缺失脚本。禁止通过备份 C# 到 Assets 内保留重复可编译代码。

## 本次清理边界

删除旧树/道具生成菜单实现、两个场景创建菜单实现、旧 Demo/VehicleController/CameraRig、未使用草地实验脚本与 Shader、无引用且与 summer_grasses.png 哈希相同的 Resources 草贴图，以及 HillsAgentDebug 场景。

目录整理时保留已有树 Shader、材质和 Prefab；后续资产审计已进一步裁剪为 Hills 八种 OBJ 树的引用链，详情见 Docs/Assets/HillsAssetAudit.md。保留三个旧道路算法脚本，仅供当前调试框架使用。原版摘录与旧审计报告保持历史原貌。
