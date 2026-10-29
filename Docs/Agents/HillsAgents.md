# Hills agent 结构与阶段验收

此文件定义开发 agent 的职责和交接契约；不是运行时 AI、NavMesh agent 或车辆控制系统。当前唯一运行时为 HillsDebugSession → RoadStreamingPresentation / RoadStreamingSession → StreamingRoadRouteSource → RouteObserver → ObserverCamera，唯一业务 SO 为 `Assets/SlowRoads/Settings/Hills/HillsSceneSettings.asset`。2026-10-29 按用户要求删除有限预览/烘焙、灰模、独立高度场及材质/观察者/流式分散配置；只保留一个种子入口。Legacy 道路已清退；树 Shader 仍被八套参考材质引用。

唯一开发场景为 `Assets/Scenes/SlowRoadsRoadDemo.unity`；目录与协作边界见 `ProjectLayout.md`。不再通过菜单创建演示场景。

原版参考安装文件现在位于项目根目录 `Slow Roads/`，源码从 `Slow Roads/resources/app.asar` 读取；该目录被 `.gitignore` 排除，不得提交。`SourceAtlas` 与 `source-manifest.json` 继续保留行号和哈希证据；清单中的旧 OneDrive 路径仅记录当时的提取来源。新的对照程序以本地 archive 为准，不依赖旧 `extracted_correct` 目录。

## 分工（按依赖顺序执行）

| Agent 职责 | 原版对应 | 输出与边界 |
|---|---|---|
| Coordinator / Parity | Ni.initialise / updateLive | 固定 seed、阶段门禁、作业依赖、验收记录；不实现全能生成器 |
| Random & Heightfield | Rand / Alea；Dev oe；Hills Pl | 原版随机序列、tile 缓存、径向核高度查询；不得用现有 fBm 当等价实现 |
| Midline | Dev Se；Settings B7 | 粗节点推进、目标/回退、桥梁规划、延迟平滑、精采样；只输出路径与附属标记 |
| Spatial Index & Streaming | Ca；Ma / si；xi | 道路 tile 索引、远近场调度、三段道路复用、分步预算和释放 |
| Terrain & Road Geometry | Dt / i7；U / s0；D / Nl | 远景 tile、近路 cell/chunk、道路投影与路基混合、道路网格；不改基础高度场 |
| Hills Contents | Wa；Ra/Ea/Ka；G7/J9 | 树密度与群组、农田裁接、墙体、桥梁实体、路边物件；不扩大场景类型 |
| URP Rendering & Assets | 已生成几何、实例和参考素材 | 独立 Feature、Pass、GPU 剔除/LOD、材质与临时美术绑定；替换资产不改变生成随机序列 |
| Debug & Verification | 原 vehicleIndex 的生成进度语义 | 观察者里程/粗精节点映射、Cube、跟随相机、seed 对照、帧与内存验证；不实例化 VehicleController |

## 契约与扩展顺序

1. Seed 必须明确随机流所有权；生成随机流与美术变体随机流在原版证据支持的边界隔离，不能随意改变调用顺序。
2. 高度查询使用世界 XZ / Y；粗/精节点保存原版索引、关联和附属标记，平面侧向与表面法线不可混用。
3. 调度以观察者路径进度作为输入，几何/实例系统不能反向依赖 Cube。长距离精度、浮动原点和分块生命周期在正式 streaming 契约实现时统一解决。
4. 唯一 `IRouteSource` 提供 Generation / TrySample(double)，RouteObserver 只在 Ready 后提交里程。原 EnsureAhead / Sample、IIncrementalRouteSource、有限路线终点钳制随预览分支删除；不得恢复无调用方的兼容接口。
5. 观察者接口已与 Legacy 解耦；路径可控与地标约束仅在 S3 加入，不预先改变 S1 输出。
6. 各职责提交：来源符号/行号、输入输出、确定性、资源所有权、开关、性能预算、验证与未完成项。

## 旧实现清退与增量开发门禁（长期有效）

本节适用于 P6、S1c/S1d、S2/S3 及后续所有开发批次，是功能验收后的**强制收尾工作**；不得将其解读为授权立即批量删除尚未迁移的资源。当前默认道路源为 P5；`Runtime/Legacy/Road` 及其旧配置、模式开关已在用户授权的清退任务中删除。有限诊断已因用户显式要求删除；原版对照执行器及正式观察框架仍按实际用途保留，不恢复用户不需要的验收入口。

1. **唯一正式运行链路**：同一职责原则上只保留一套经过验证的生产实现。新模块验收通过后，不得长期维持旧算法、旧 SO 配置、旧运行入口、临时适配层与新实现并行；禁止新增 Legacy 调用方、兼容包装层或备用分支。确实需要的诊断能力应迁移到已验收的正式模块，而不是永久保留旧生产算法。
2. **每批次清退流程**：先确认新实现及其可替代范围已验收；列出旧代码/字段/SO/Prefab/场景/材质/Editor 工具/测试等候选；标记“正在使用／可迁移／可删除／证据资产／受阻”；先迁移有效调用方与序列化引用，再删除确认废弃项，最后执行受影响范围的编译、自动测试及 Play 冒烟验证。不靠注释、禁用组件、Old/Backup 重命名或将重复 C# 留在 Assets 中替代删除。
3. **引用审计不止于文本搜索**：检查 C#/asmdef、接口注册、反射及字符串入口，Scene/Prefab/Variant/SO 的序列化引用与 `.meta` GUID，Editor/测试/Build Settings，以及确实使用的 Resources、StreamingAssets、Addressables 等加载链。动态调用或证据不足的项目标记 UNVERIFIED；不能把“搜索不到类型名”当成无引用的证明。
4. **Unity 迁移安全**：已有有效资产优先原位更新并保留 GUID；字段更名使用适合的序列化迁移策略；变更脚本类型或程序集时核查实际挂载和引用。通过 Unity 编辑器 API 或受控方式删除资产与对应 `.meta`，不得留下 Missing Script、丢失 SO 参数或无效构建入口。当前IRouteSource仅保留Generation/TrySample；删除无调用方的旧接口，保持观察者背压安全约束。
5. **保护不可替代的证据**：`app.asar` 的本地来源链、SourceAtlas、manifest、原符号/`Lxxxxx`、fixture/schema、独立参考执行器、正式回归测试与阶段验收记录属于证据资产；不得按“无运行时引用”清理。旧测试只有在其职责已被正式测试覆盖且证据不再需要时才能删除。不得修改原版期望值、扩大容差或改变随机数消费顺序来促成清理通过。
6. **保持增量与 Git 安全**：开始前查看 Git 状态；尊重用户未提交和未跟踪文件，不擅自 reset、clean、覆盖、删除改动归属不明的文件，不自动提交或推送。优先在现有模块做最小修改，不引入重复 Manager/Adapter/Wrapper、平行框架或无调用方的空抽象。对无法安全删除的项记录阻塞及实际引用，禁止假装已清理。
7. **清理后必须验收**：至少覆盖受影响的编译/asmdef、相关数值与确定性回归、Scene/Prefab/SO/GUID 引用、主场景 Play，以及适用的构建入口。若改动 Streaming/P2–P4 数据链，须验证同 seed、同配置、同逻辑输入在不同分步预算下稳定提交的道路数据一致；比较完整已提交历史，而非不同帧的瞬时缓存。已有效验收且未受影响的 P0–P4 不重复全量复测。无法实际执行的检查标记 UNVERIFIED。
8. **完成门槛与记录**：功能通过且所有**可安全删除**的被替代实现已实际删除，才算本批次完整交付；尚有有效引用、证据用途或用户改动阻塞的文件不得强删，列出文件/引用/原因/解除条件，并将清退部分明确记为未完成。交付简报列出新增与修改、删除、迁移引用、运行的测试及剩余阻塞；仅在架构或阶段状态发生实际变化时更新本文件与 `ProjectLayout.md`，不重复创建清理报告。

## 阶段

- S0：调试基础设施已具备；唯一主场景为持续道路，无有限预览/烘焙/灰模入口。提供暂停/单步/重置、Cube 与相机跟随，使用唯一 HillsSceneSettings。
- S1a：P0/P1 已通过下述限定范围的原版对照与 Editor 回归；未接入中线。移动设备性能与 getXZLayer 不在本次已验收范围。
- S1b：P2/P3的R01–R09已通过style0/style3及限定自然/合成用例，包含粗精线、桥/回退、属性、版本化快照、米制查询与边界重放；93/93结果见 `Docs/ReverseEngineering/RoadAudit/P2P3_开发实施计划.md`。P5 已接入下述固定配置的空间索引及持续道路。
- P4 道路网格：D/Nl 纯内核与 Unity 上传边界已通过 20/20 测试，P2/P3 回归 93/93。20 组原版输入、60 次构建在预算 1/7/64 下逐位匹配；真实 P3 的 style0/2000 步、style3/4100 步到 Nl 联合通过。主场景临时 URP 剔除/两段接缝验证后已清理，当时常驻数据源仍为 Legacy；现已由下述 P5 接管。完整范围、CPU 基线和限制见 `Docs/ReverseEngineering/RoadAudit/P4_道路网格开发实施计划.md` 文首。
- P5 持续道路：2026-10-29 已将默认配置切为 seed=a/style3/Standard/detail2 的三 Mesh 流式源；原 xi/Ca 联合 96000 tick、200 次发布在预算1/7/64及不规则预算下通过，主场景推进6148米并完成原点平移。最终 EditMode 144/144（P0/P1 12、P2/P3 93、P4网格20、材质8、P5 11）。当前范围及 UNVERIFIED 项统一见 `Docs/ReverseEngineering/RoadAudit/P5_流式调度开发实施计划.md` 第9节；其他seed/detail及异常重启完整联合oracle未覆盖，不能宣称完整场景复刻。
- P6 道路功能补齐（2026-10-29）：第9节A–F功能目标已实施；Ll正式查询、五类型两桥逐点、42组独立原版联合矩阵及恢复/生命周期回归通过。最终EditMode全集368/368，主场景五类型、桥2435、原点迁移与退出释放通过。原版精链后重启在B7.updateMarkings的空节点错误独立保留；用户明确授权生产流式会话只在代际边界重置标记状态，兼容参考与原版错误样本分开记录。完整证据见 `Docs/ReverseEngineering/RoadAudit/P6_道路专项验收记录.md` 第10节。Android/iOS性能按用户要求延期，不阻塞本批功能结项；不代表整个S1场景完成。
- T0/T1（2026-10-29）：复用P6的Ll，新增原$u精投影、有序cell候选与U完整高度组合、P5有界粗精快照查询。12组/12,960原版样本，正反序25,920次比较；新增定向8/8、受影响道路回归通过。窗口严格同Generation/Revision及驻留有效，未生成tile/chunk网格；完整合同与实际范围见 `Docs/ReverseEngineering/TerrainAudit/T0T1_路基地形混合开发实施计划.md` §12。
- S1c：地形、路基、道路、树群、农田、墙/桥/路边物件完整复刻；临时参考资产绑定。
- S1d：固定观察点图像、生成计数、生命周期、Android/iOS GPU 帧耗时与带宽验收；通过后才能进入 S2。
- S2：自有美术替换与材质/LOD 验证，检查所有临时素材引用已经清除。
- S3：路径可控、自定义地标等需求另行验收。

## P0/P1 道路生成基础验收（2026-10-28）

- 权威输入：Slow Roads/resources/app.asar，package.version=1.0.2。heightfield-reference.cjs 校验清单中 Dev、Hills、Settings、Rand 四个 chunk 的 SHA-256，直接提取并执行 archive 原始代码，不再执行 SourceAtlas 文本。提取标记须唯一且有序；执行片段另存 SHA-256、符号与摘录范围到 schema 2 fixture。--self-test 验证错误哈希、缺失/重复/反向标记会失败。参考摘录保持只读，不提交安装包或提取副本。
- 来源：Alea/Mash 对应 Dev Z（L00147–L00216）；缓存对应 oe（L00025–L00133）；高度查询对应 Hills Pl（L06516–L06829）。参考执行仅适配 window、导入别名与导出入口，不启动原游戏、车辆或绘制系统。
- 配置修正：之前标作 style0/style3 的 fixture 和 SO 错将 squared/temper 设为 false，旧 5 项测试结果及性能记录不能作为原版样式验收。现在从 archive 配置字面量提取 style0（Hills L01575–L01587）与 style3（L01698–L01709），两者 squared/temper 均为 true；合并顺序为 seed → roadStyle.heightmap → graphics.maxCached（Settings L50112–L50113，Hills L05979、L06895–L06896）。本次原版样式选 graphics detail=2，maxCached=20（Hills L01810），不是硬容量上限。
- SO：HillsHeightfieldSettings 类型与资产默认采用 style3/detail2/seed=a，squared=true、temper=true、maxCached=20。资产 GUID c000a17070ab05048b92923ab786bc02 保留；当时 P1 验收期间主场景仍使用 Legacy；当前已由正式道路接管，示例高度场SO后续因不被生产读取而删除。default、synthetic-linear、synthetic-repeated 在 fixture 中标明原默认配置或合成覆盖用途。
- 数值契约：tileSize=3000，世界 XZ 输入、Y 输出；坐标、控制点与随机状态使用 double。tile seed 为 seed#curLayer#tileX#tileZ，curLayer 按 tileX 正负选择；repeated 只用 seed。数字拼接采用 invariant culture；即使 tileScaleRandom=0 仍消费一次随机数；保留 x-major/z-minor 累加顺序和 temper 前层依赖。配置快照复制输入数组；切层由后续中线驱动，释放用 Dispose，重配创建新实例。
- 缓存修正：非负整数键按升序、负字符串键按插入顺序，匹配 JS 对象枚举；覆盖 tileX/tileZ<-100 后的 +100 键边界。保留先增 count、再裁剪、再生成语义；命中刷新年龄、两侧独立切层、force、spiralise=false 和恢复旧层均有逐操作对照。裁剪直接遍历持久键表，不再分配临时 List。显式诊断快照/控制点副本会分配，普通查询不调用它们。
- 覆盖：7 个随机 seed（a、1337、0、-17、空字符串、中文、代理对字符），各 1024 输出，分别在 1/17/257/1024 处保存和恢复状态；20 组高度场，2900 个高度值，19140 个完整控制点（76560 个字段），46 步缓存状态（成员/顺序/年龄/layer/count），径向核内/边界/外。另验证倒序查询、不同配置实例交错、重复 Dispose、重建、全局随机状态不变、SO 与配置快照、区域设置、fixture schema/hash/样式身份拒绝篡改。测试按 id/seed 选用例，不依赖数组位置。
- 结果：12/12 EditMode 测试通过；性能计量修订后该项单独复测通过。高度最大绝对误差 2.8421709430404007e-14、最大相对误差 2.1918206472708562e-16；控制点最大绝对误差 1.4210854715202004e-14。容差保持 1e-9 + 1e-12 × |参考值|，未放宽。seed=a、XZ=(-500,-800) 的固定回归为 style0=100.15434953512572、style3=89.54214451936353。参考再生成只读比对及提取自检通过。
- CPU：Windows 11、Intel i7-14700K、Unity Editor 6000.3.22f1、RTX 3060 / Direct3D12；纯算法测量与渲染分辨率无关。修正后的 style3/detail2/seed=a：热缓存 10000 查询 5.748 ms、0 次 GC.Alloc；64 个跨 tile 冷查询 16.785 ms、2519 次 GC.Alloc、留存 20 tile；强制裁剪无临时分配。本机 Mono 的 GC.GetAllocatedBytesForCurrentThread 对 1 MiB 哨兵也返回 0，不能当成有效字节统计；已改用经分配哨兵校准的当前线程 ProfilerRecorder，冷查询分配字节数不可得。这是单次 Editor CPU 基线，无 GPU 提交，不代表 Android/iOS 帧预算。
- 主场景回归：GUID c323176c3ca002848be289a8e08ab141 保留；唯一启用 Build 场景、1 个编辑态根对象；missing scripts、失效序列化引用、缺失 Shader 均为 0。Play 帧 1410→5459、里程 54.79889→199.828125 m；暂停到帧 5577 里程不变，单步到 200.828125 m（+1 m），两次固定 seed 重置起点与首节点一致、里程归零、摄像机始终 1。进入任务前为 Play/未暂停/Run In Background=false，验证后恢复该状态；运行期里程因重启而重置。
- P2 扩展点：HillsHeightfieldConfig 是不可变输入；GetMidlineXZ/GetXZ 输出 double 高度；IncrementLayer/SetLayers 接收中线状态；WarmTile/PruneCache/Dispose 由流式调度管理；几何与渲染边界再转换精度。当前 Int32 tile/cache-key 域显式拒绝溢出，正式长距离/浮动原点契约在 streaming 阶段统一设计。
- 明确限制：空 layerResolutions 在原版构造中抛 TypeError（未初始化字段），C# 明确抛 ArgumentException，双方行为均有测试；Pl.getXZLayer 未移植，archive chunks 未发现点调用，原方法还依赖共享 temper scratch，不宣称等价。S1b–S1d、完整渲染、Android/iOS 真机性能尚未验收。本批次未增加 Shader、keyword、RenderPass 或 RT。

复跑命令（项目根目录）：

    node Docs/ReverseEngineering/RoadAudit/heightfield-reference.cjs --self-test
    node Docs/ReverseEngineering/RoadAudit/heightfield-reference.cjs
    unity status --format json
    unity command 'test run' --project-path E:/UnityProject/Unity6/SlowRoads --caller plugin --skill unity-cli --mode editor --filter SlowRoads.Generation.Tests --filter_type assembly --format json

fixture 仅在审查原版来源与配置差异后显式加 --write 更新；普通测试不会写期望值。测试输出误差统计，CPU 测试记录计量校准和 GC.Alloc 次数。

## P2/P3交接状态（2026-10-28）

`Runtime/Generation/Midline` 已接通原Se/B7完整逻辑update与预算让出。私有密度源随head层变化；`MidlineReadView`只在完成边界按需复制，P4消费者检查generation/revision及独立ready界。B7可回写旧标线属性，必须处理AttributeDirtyFrom，不能假设所有已读属性永久冻结。`MidlineDistanceIndex`是米制折线查询扩展，不是原道路UV算法；checkpoint只保证完成update边界的确定性重放。

该轮93/93是P2/P3纯数据验收，没有把新路径源接入主场景。当轮唯一场景仍使用 Legacy/Cube/相机；此处为 P2/P3 交接时的历史状态，后续 P5 已接管默认路径。P4 几何与上传验证见下文，P6 的 Android/iOS 实测仍未完成。原版Math由Node/V8参考执行器验证，不外推原Electron和全部GPU/CPU平台逐位一致。

## P4 交接状态（2026-10-28）

`Generation/Road` 保持无 Unity 引用，显式配置、generation/revision、ready 界、phase 和段对象 paint 续状态。一个 Advance 单位为一个粗区间，最多十个精步。Nl 的尾点标线及派生 bool 回写隔离在 P4 overlay，不修改 P3；相邻段必须按原顺序共享 overlay 并传递 phase。需要重建时恢复段前 paint/overlay 状态；dirty−1 是最早保守影响区间，本轮重建整个有限段。Cancel 停止旧任务，新 generation 清旧 paint 约束。

`Scene/Road` 的 `SlowRoads.RoadPresentation` 仅依赖 Generation 和 Unity。上传器检查生产者当前代际，冻结发布元数据，复用 GPU/CPU 容量，支持 enabled/visible、有效索引 LOD 与幂等 Dispose。七通道位于 TEXCOORD1/2。P4.1–P4.7 当时使用 URP/Unlit；2026-10-29 的 P4.8–P4.13 已加入专用路面 Shader 和 12 套参考材质，详见下段，不能继续声称当前新增 Shader/variant 为 0。

P4 材质补充：`road-material-reference.cjs` 校验当前 archive 五模块并抽取 ue/绑定/Fresnel/shadow 证据，独立表面算术样本 12 个；RoadMaterialP4 8/8、RoadMeshP4 20/20 通过。Shader 消费七通道，alphaTest=.75，保留标线顺序与季节覆盖；Forward/DepthOnly 两 Pass、Instancing、无自定义 keyword，SRP Batcher code=0。17 张已有纹理使用 BC7/ASTC 6×6；桌面全部贴图 Editor 留存约 59.4 MB（原未压缩约 235.6 MB）。URP 主光/SH 为适配，全场景光照/雾与移动设备性能未验收。

P4 材质补充验收时，Debug SO 的 `useFiniteRoadPreview=true`：两段 natural-style3/seed=a 样本经实际 P4 Builder/Uploader 烘焙成资产 Mesh，53 个路线样本，约 80.33223 m 后自动暂停。运行时不加载 Docs、fixture 或测试程序集。`HillsRoadMaterialSettings` 控制季节、surface、显式灰模；`HillsRoadMaterialAuthoring.Bake` 在现有 Inspector 中提供有限段重建，没有平行场景或生成菜单。这是当时的材质诊断记录。后续已按用户要求删除有限预览、烘焙、旧材质SO及全部模式开关，统一使用HillsSceneSettings。

P5 已接管固定配置的空间索引、三段轮转、窗口化快照/邻域保留、相位失效传播、场景数据源切换和长距离浮动原点。2026-10-29 专项复验 PASS：11 个定向测试通过，A–F（1/10/100、动态切换、暂停单步、同 seed 重置）的完整发布历史逐值一致；主场景背压与连续重置通过。范围为 style3 / seed=a / detail2 / Standard，证据见 P5 计划第10节；具备 P6 前置条件，本轮未自动执行 P6。当前完整快照复制会分配，不得每帧调用；热构建/上传不分配的测量不能代替整个 streaming 的性能结论。P6 继续负责移动设备 GPU/带宽验收；地形、路基、桥结构与内容生成仍未完成。

## 使用调试框架

直接打开 `Assets/Scenes/SlowRoadsRoadDemo.unity`。配置资产为 `Assets/SlowRoads/Settings/Hills/HillsSceneSettings.asset`。旧 Slow Roads 菜单、创建器及有限预览烘焙已经移除；不要重新引入开发验收按钮、示例配置或自动覆盖场景的生成器。

进入 Play，选择 `Hills Scene`：道路类型可选宽越野、越野、单线窄路、单线宽路、标准公路，点击“应用道路设置并重新生成”生效；保留暂停/继续、单步与里程状态。唯一 SO 控制 roadType/seed/detail、季节、移动/相机、预算与驻留；三族四季材质为同一SO内直接资源引用。roadType/seed/detail/驻留在重新生成时生效；等待应用期间材质继续跟随当前Core类型，避免半切换。

当前显示持续生成道路和橙色 Cube，背景纯色；没有地形、植被、车辆、音频、物理控制。全部设置读取 HillsSceneSettings；五种类型已联合装配P3路缘阴影采样宽度、P4网格/UV/越野语义和表现材质，半宽依次3.6/1.8/1.8/3.6/3.7米，由类型派生，不增加独立宽度字段。越野按原Nl只在桥区发布可见路面；当前缺少地形，普通越野区显示背景，不能用临时铺装网格伪造原版效果。相机在LateUpdate运行。完整验证见P5记录第13节。

性能边界：单个观察者的 CPU 路径采样与相机更新规模很小；Legacy 近似生成及线性查询路径已删除。当前三块道路 Mesh 复用，共享季节材质。无 GPU Compute、额外管线 Pass、RT 或全屏 Blit；专用路面 Shader 有 Forward/DepthOnly，支持 Instancing，无自定义 keyword。移动设备表现仍待实测。
