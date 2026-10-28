# Hills agent 结构与阶段验收

此文件定义开发 agent 的职责和交接契约；不是运行时 AI、NavMesh agent 或车辆控制系统。运行时最小结构为 HillsDebugSession → IRouteSource → RouteObserver → ObserverCamera，配置为 HillsDebugSettings。

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
4. `IRouteSource.EnsureAhead(distance)` 必须覆盖随后 Sample 的范围；Sample 返回路面位置和非零切向，单位为米。当前 LegacyRoadRouteSource 仅适配旧道路，尚无粗精索引和原版精采样语义。
5. 替换 LegacyRoadRouteSource 时保留观察者接口；路径可控与地标约束仅在 S3 加入，不预先改变 S1 输出。
6. 各职责提交：来源符号/行号、输入输出、确定性、资源所有权、开关、性能预算、验证与未完成项。

## 阶段

- S0：调试基础设施已具备；主场景仍接旧道路，提供暂停/单步/重置、Cube 与相机跟随。
- S1a：P0/P1 已通过下述限定范围的原版对照与 Editor 回归；未接入中线。移动设备性能与 getXZLayer 不在本次已验收范围。
- S1b：粗/精中线、回退、桥梁与道路属性对照；空间索引、裁剪和分步作业验证。
- S1c：地形、路基、道路、树群、农田、墙/桥/路边物件完整复刻；临时参考资产绑定。
- S1d：固定观察点图像、生成计数、生命周期、Android/iOS GPU 帧耗时与带宽验收；通过后才能进入 S2。
- S2：自有美术替换与材质/LOD 验证，检查所有临时素材引用已经清除。
- S3：路径可控、自定义地标等需求另行验收。

## P0/P1 道路生成基础验收（2026-10-28）

- 权威输入：Slow Roads/resources/app.asar，package.version=1.0.2。heightfield-reference.cjs 校验清单中 Dev、Hills、Settings、Rand 四个 chunk 的 SHA-256，直接提取并执行 archive 原始代码，不再执行 SourceAtlas 文本。提取标记须唯一且有序；执行片段另存 SHA-256、符号与摘录范围到 schema 2 fixture。--self-test 验证错误哈希、缺失/重复/反向标记会失败。参考摘录保持只读，不提交安装包或提取副本。
- 来源：Alea/Mash 对应 Dev Z（L00147–L00216）；缓存对应 oe（L00025–L00133）；高度查询对应 Hills Pl（L06516–L06829）。参考执行仅适配 window、导入别名与导出入口，不启动原游戏、车辆或绘制系统。
- 配置修正：之前标作 style0/style3 的 fixture 和 SO 错将 squared/temper 设为 false，旧 5 项测试结果及性能记录不能作为原版样式验收。现在从 archive 配置字面量提取 style0（Hills L01575–L01587）与 style3（L01698–L01709），两者 squared/temper 均为 true；合并顺序为 seed → roadStyle.heightmap → graphics.maxCached（Settings L50112–L50113，Hills L05979、L06895–L06896）。本次原版样式选 graphics detail=2，maxCached=20（Hills L01810），不是硬容量上限。
- SO：HillsHeightfieldSettings 类型与资产默认采用 style3/detail2/seed=a，squared=true、temper=true、maxCached=20。资产 GUID c000a17070ab05048b92923ab786bc02 保留；主场景仍使用 Legacy，不把示例资产当成运行时 roadStyle 判定。default、synthetic-linear、synthetic-repeated 在 fixture 中标明原默认配置或合成覆盖用途。
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

## 使用调试框架

直接打开 `Assets/Scenes/SlowRoadsRoadDemo.unity`。配置资产为 `Assets/SlowRoads/Settings/Debug/HillsDebugSettings.asset`。旧 Slow Roads 菜单及创建器已经移除；不要重新引入自动覆盖场景、清空材质/Prefab 目录的生成器。

进入 Play，选择 `Hills Scene`：Pause / Resume、Step Forward、Reset Fixed Seed；Inspector 展示里程和保留节点数。SO 控制移动、相机、道路可见性、Gizmos、速度与镜头参数。seed、道路参数、道路可见性、Gizmos 与窗口大小在 Reset 时生效；移动/相机开关和速度实时生效。

当前只显示灰色道路和橙色 Cube，背景纯色；没有地形、植被、车辆、音频、物理控制。相机在 LateUpdate 运行，指数阻尼，不依赖输入系统。

性能边界：单个观察者的 CPU 路径采样与相机更新规模很小；旧道路在节点变化时重建有限窗口网格，仍有 CPU 分配和线性查询，不可用于正式大规模场景。当前无 GPU Compute、额外 Pass、RT 或全屏 Blit；预览材质使用 URP Unlit 且 enableInstancing，没有新增 Shader keyword。正式 S1 移植必须替换旧道路重建和调度路径，不能把该预览的性能当作最终移动端表现。
