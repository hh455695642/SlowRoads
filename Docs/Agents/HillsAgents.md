# Hills agent 结构与阶段验收

2026-11-13 LT5–LT8补证：显式重新应用地表主题已接现有Debug Inspector；去重51/51、三seed四预算完整回收历史2304事件、域重载开/关各10轮重建、2.8km全内容推进与独立Weather Player通过。24主题/动态阴影、独立天气、786保护文件、密度与6m/2m保持，旧Lighting/运行主题Volume和临时Before Shader清退。LT6当前桌面CPU/GPU基线已实测，但严格整图相等与同条件Before成本比较仍未关闭，整体LT/C7/S1不标全PASS。详细结果见环境Volume计划§13.8。

2026-11-13 W迁移当前入口：天气通用代码/主题表/Shader/Compute/材质/纹理/Resources/Profile统一位于Assets/Art/Weather，旧embedded包已移除；主场景Hills Environment Volume挂WeatherController，只写天空/云/雾/雪。天气季节/时段/天气与HillsDebugSession地表主题输入独立，无自动同步。Hills光照、道路阴影及水面主题链保持；历史V0–V10路径不再是当前入口。详细验证及限定范围见环境Volume计划§12.9。

2026-11-13 Scene View补充：现已补齐原先缺失的SceneCamera环境owner，Edit/Play均复用正式Volume/Pass，PC Renderer资源已接入。12项定向回归、Scene窗口即时试色/云雪及唯一主场景1182m冒烟通过；用户SO/Profile RAM和dirty、树草算法/密度、相机6m/2m保持。此为Editor预览修复，不关闭V9原生路线/最终整图或C7门禁，见环境Volume计划§11 V10。

2026-11-12环境Volume迁移V0–V8限定门禁PASS：通用天空/云/雾照明/雪统一于embedded包com.slowroads.environment，五类原生Volume、独立资源SO与三Feature，HillsDebugSession保留运行控制和低优先级主题适配；主场景Global Volume持久化，原GUID保留。30/30环境回归、720参数/原GPUFixture、三组最终同机位0像素差、3seed四预算完整回收历史、十轮重建（域重载开/关）及独立无Hills工程通过。手工Override不再被基础预览逐帧重写。修正版实跑1800秒/14.23km/3原点迁移/0错误；同条件桌面3轮CPU/GPU/GC和资源已实测，旧通用副本/重复字段/临时生产探针已清退。102个Generation源码、9原Fixture、候选/实例窗口/RNG与密度、6m/2m保持。V9已补batch Editor D3D11三轮GPU实测（CPU中位增加约0.5ms）及自然桥2434六组原Ni受控道路回放整图；原应用原生路线有跨执行环境数值分歧，最终整图仍UNVERIFIED。用户RAM中的黑色天空Override已原样恢复，生产代码/密度/6m与2m不变；移动/XR未测，不宣称E7/C7/S1全通过。详见ContentsAudit/URP环境Volume模块化与跨项目复用实施计划.md §11。

2026-11-12环境专项E5：原版gi冬阴full48841/half5476/0、54317次交错随机消费、108组原GPU顶点及33组原图通过；原贴图base/mip逐字节一致，环境回归20/20。私有LCG仅适配原非seed雪初始化，不接道路/草木随机流。HillsSnowRendererFeature在449事件Compute+单次Indirect，固定4 Buffer共2214736B，深度写入/遮挡通过，无新增RT/Blit；不支持Compute/Indirect时禁雪。唯一SO新增两资源引用，C5共用noise原资源/Importer不变。主场景雪开关树/草灌/实例窗口/RNG hash及相机6m/2m不变；原点、重建、退出释放有记录。E6/E7/C7未开始，范围与真实CPU/GPU采样见ContentsAudit环境计划§8。

2026-11-12环境专项E0–E4：唯一SO/按相机天空云链已接入原地景雾和主题光照。原雾480样本max1.789e-7、288照明样本max4.769e-7、14/14环境测试及24组固定机位通过；树/草灌/随机/相机hash保持。C3旧PBR FAIL与E4前差异报告完全相同，沿用既有接受边界、不改Fixture。Feature复用原入口，BeforeRendering绑定/AfterRendering清空，雾在正式各族Forward，无新增RT/绘制；移除水双雾及旧固定太阳。相机6m/2m、102个Generation源码和密度保持。E5雪已通过上述限定门禁，E6/E7整图/性能和C7仍未完成，详见ContentsAudit/S1_四季天空时段与天气复刻实施计划.md §8。

2026-11-10补充：用户已接受C3已知轻微HDR效果差异；原线性数值FAIL/阈值/Fixture保留，但该项不再单独阻塞结项，具体范围覆盖早期强制L1修复依赖，见C7计划§14。用户随后授权相机高度2m及六张草灌图集Trilinear/Clamp、草aniso4/灌木2，已保存并通过9/9定向回归、冻结候选/随机一致及60m主场景冒烟；原算法/密度和运行代码不变。详见C5C6复核计划§11–12；动态mip、压缩alpha及完整画面仍归C7.4。 随后按用户要求相机后退改为6m、高度保持2m；远景树定向复核修复Fo/zo局部噪声UV，密度/生成不变，18/18回归通过，同机位与候选Hash记录见C0C1完成度复核计划§10。

C5/C6前置补证（2026-11-10）：E1完整归档OBJLoader/Three/K0首对象回调与正式灌木Mesh逐位PASS；E2测试内全模块历史入口、三seed四预算/重放、贡献撤回重发、warm/取消和真实C3受控Marker已交付。去重20/20自动测试及唯一主场景60m窗口、受控显示/原点、退出释放通过；无运行时代码/场景/SO/GUID变更。完整记录及覆盖边界见`Docs/ReverseEngineering/ContentsAudit/C5C6_完成度复核与C7前置补全计划.md` §10。C7仍PARTIALLY READY；C3 HDR FAIL、正式全配置/自然桥区域/固定图/独立GPU/长跑/Legacy结项仍待C7，不能把前置子矩阵PASS外推为第三目标完成。

C5/C6当前状态（2026-11-09，取代旧§13待办）：C5原Et/Lt窗口、Ni模块暖重建及共享G4联合门禁已关闭，Grass21/21、TreeGpu6/6通过。C6十类标识、原E7/h4复用语义、三组路桩Prefab及x7双缓冲已接唯一主场景，Roadside13/13通过；新增24组/727事件原控制器+对象池联合对照、18组Ni地表选择、72组反光与7组发光GPU样本。道钉保留原透明但写深度状态，原路桩OBJ的黑/白/反光分组不能用合并导入Mesh代替。1000m、原点、warm保留随机、开关和释放已有证据；完整画面/光照/雾/mip和GPU毫秒归C7，不能据此宣称整个S1完成。实施、输入边界、成本及适配说明统一见`Docs/ReverseEngineering/ContentsAudit/C5C6_草灌与路边物件开发实施计划.md` §14–15。

C4增量合同（2026-11-05）：T4队列拥有水面需求及逐列构建，FarTerrainTileBuilder在Heights0结束冻结原高程/hasSea；Fields提供逐区域完成收据，WaterTilePresentation仅拥有Mesh与共享材质显示。只使用原Y=0、原depth及不透明普通URP路径，不改Pl/Ll/桥规划，不新增调度器或SO。C4桌面实现/原版对照/生命周期/两桥/成本及明确未验证范围统一见`Docs/ReverseEngineering/ContentsAudit/C4_湖泊水面开发实施计划.md` §10；不是完整S1或C5/C6交付。

此文件定义开发 agent 的职责和交接契约；不是运行时 AI、NavMesh agent 或车辆控制系统。当前唯一运行时为 HillsDebugSession → RoadStreamingPresentation（协调RoadStreamingSession、TerrainStreamingSession与正式TreeStreamingSession/Presentation）→ StreamingRoadRouteSource → RouteObserver → ObserverCamera，唯一业务 SO 为 `Assets/SlowRoads/Settings/Hills/HillsSceneSettings.asset`。2026-10-29 删除有限预览/分散配置，仅一个种子入口；2026-11-02树群已接主场景，原八套树材质保GUID迁移后Legacy树Shader已删除。

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
- T2/T3及C1–C3（2026-10-30）：远景Tile/近路Chunk无内容几何已有原版对照；补齐只读空间产物、缓冲所有权、首次上传与驻留收据、低预算进度和边界/属性来源合同。Terrain 73/73通过。该记录对应前置完成时点；后续T4/T5实际装配与验收见下条和其第9节。当前状态与限制见 `Docs/ReverseEngineering/TerrainAudit/T2T3_完成度复核与补全实施计划.md` 第6节。
- T4/T5（2026-10-30）：已装配Ma区域/覆盖流式、共享P5预算/Ready背压及四季地形Shader；原版12组调度轨迹和32组独立GLSL表面GPU结果已对照，主场景可持续显示地形。用户已授权overlap显示流改用完整float索引，原Int8证据保留。续验补齐48组正式固定机位地形图像、48组诊断图像和实际Frame Debugger提交，T5地形范围门禁PASS。最终测试、图像差异、场景里程和明确适配边界以 `Docs/ReverseEngineering/TerrainAudit/T4T5_地形流式与材质开发实施计划.md` 第9–10节为准；未执行T6或移动端专项，不代表完整S1光照/雾/内容复刻。
- T6（2026-10-30最终结项）：约定Editor/桌面范围九类门禁全部PASS，第二目标“道路与丘陵地形一体化生成”完成。失败上传产物释放缺陷已最小修复；新增联合10+1项、受影响流式6项、新桥区36区域原版对照、49帧色彩/实际深度、八配置轮转、十次重置及30分钟/93.33km联合长跑记录有效。用户手动清理全部17项目标后，1004个保护文件核验及正式主场景清理后回归通过：实际增加291.67m、固定seed重置后继续140.89m，退出资源归零。最终证据以 `Docs/ReverseEngineering/TerrainAudit/T6_道路地形联合验收实施计划.md` §13及HillsTerrainT6/result.json为准；历史拒绝记录保留，不含移动端性能，不代表整个S1完成，不自动进入下一目标或S2/S3。
- 第三目标C0/C1（2026-10-31进行中）：内容原版执行器、13份fixture与最小SourceAtlas补证已建立，初始Ma urgent中心Tile顺序已纠正；C1.1只读数值基础（密度、ze族map、h5变体noise、四循环表/checkpoint）已实现，原采样对照4/4及受影响道路阴影输入/属性8/8通过。完整live内容调度/重建trace尚未收口，C0整体及C1整体均未PASS；未接候选生成、GPU渲染或主场景内容，不包含移动端专项。实际结果、批次调整和剩余门槛统一见 `Docs/ReverseEngineering/ContentsAudit/C0C1_内容清单与树群开发实施计划.md` §15，不覆盖T6既有结项。
- C0/C1补全R1（2026-10-31）：已补真实Ma/si/Ca/qb/Dt/i7动态参考套件和U父依赖、urgent/部分消费后取消收据；原13份fixture受影响非写入回归无漂移。进度点排空子集PASS；连续低预算合成压力输入复现缺失父Tile读取，R1 Ready Gate为FAIL，未进入R2。该样本排除真实Chunk几何/C2/C5，不宣称完整原应用故障。原始证据、输入域/驻留解除条件与实际范围见 `Docs/ReverseEngineering/ContentsAudit/C0C1_完成度复核与补全实施计划.md` §8；没有运行时代码或主场景改动。
- C0/C1补全实施（2026-11-02，取代上两条的当前状态）：用户已解除旧R1开发阻断；R2–R7正式候选/稳定来源事务/近路解析/组合属性/GPU Cluster差量/Compute/Indirect/四季材质/唯一主场景接入已完成。CPU14/14、GPU6/6（含96组原组装GLSL表面/顶点/自发光），受影响地形流式6/6和材质3/3通过；原点迁移17次、开关随机中立和退出native资源归零。原R1压力FAIL与全Ni/全图像UNVERIFIED保留，不伪改为PASS，也不重新设立开发许可。C2可复用现有ApplyExternal/ContentAttributeStore，C3独立桥分支可先做；农田派生墙按C2输出串行。实现、接口、渲染复核、成本及限制统一见同文件§9，C2/C3尚未实施。
- S1c：地形、路基、道路、树群、农田、墙/桥/路边物件完整复刻；临时参考资产绑定。
- C2/C3实施（2026-11-02）：已按用户后续指令接入正式Fields/Bridges/Walls及原场景资源owner；原Ka/Ca、126桥、32墙规则、240墙几何和288组Ce/alpha GPU样本通过，新增/定向44/44测试通过。边界树复用唯一C1 Store/GPU，signed属性/精阴影有序组合，近地形局部替换及道路上传冻结映射已验证。用户已批准K4两处Math.random采用独立确定性Alea，原公式/随机回放证据保留。完整Three/URP整图、完整Ni自然时序和阶段性能归因仍未验，不能宣称整个C2/C3原渲染最终验收PASS。当前代码、C5数据接口、主场景重置/释放、成本和清退范围以`Docs/ReverseEngineering/ContentsAudit/C2C3_农田桥墙开发实施计划.md` §10为准；不自动实施C4–C7。
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

进入 Play，选择 `Hills Scene`：五种道路类型，点击“应用场景设置并重新生成”生效；保留暂停/继续、单步与里程状态。唯一SO控制seed/detail、树密度、季节、树群/投影可见性、移动/相机、预算及资源。生成配置重建时生效；显示/季节不重抽随机；等待道路类型应用期间保持当前Core材质。

当前显示持续生成道路、近远景丘陵、近远树群、农田/桥墙、水面、草灌、标识/路桩及橙色Cube，并通过正式天空/云Pass显示季节/时段/天气背景（关闭时为纯色clear）。道钉按原道路类型和观察者反光开关显示，默认日间关闭反光照明；不接车辆/音频/物理控制。全部设置读HillsSceneSettings；道路类型派生半宽3.6/1.8/1.8/3.6/3.7米，不另增宽度字段。越野按原Nl仅在桥区显示路面，其余由近地形表达。图形设置可暖应用并保留里程；种子/道路类型仍需fresh重建。相机在LateUpdate运行；各模块验收范围见ContentsAudit当前记录。

性能边界：观察者CPU采样规模很小；三块道路Mesh与地形Mesh池复用。树群使用独立RendererFeature、Compute/Indirect、320m显示Cluster与局部上传；没有为树分配RT或全屏Blit，仅Instancing变体。原地形13次SRPBatch draw为T5历史采样，不等于当前含树全场景draw。CPU/GPU/上传/显存结论分别记录；本次C0/C1不含移动专项，完整光照/雾图像留后续验收。
