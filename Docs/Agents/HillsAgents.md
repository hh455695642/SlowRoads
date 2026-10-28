# Hills agent 结构与阶段验收

此文件定义开发 agent 的职责和交接契约；不是运行时 AI、NavMesh agent 或车辆控制系统。运行时最小结构为 HillsDebugSession → IRouteSource → RouteObserver → ObserverCamera，配置为 HillsDebugSettings。

唯一开发场景为 `Assets/Scenes/SlowRoadsRoadDemo.unity`；目录与协作边界见 `ProjectLayout.md`。不再通过菜单创建演示场景。

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

- S0 当前：文档职责、固定 seed 的最小道路调试、暂停/单步/重置、相机跟随。仅接入旧道路，不宣称地形/内容/渲染已复刻。
- S1a：Alea 与 Pl 高度场对照，覆盖负坐标、tile 边界、多 seed；记录数值容差。
- S1b：粗/精中线、回退、桥梁与道路属性对照；空间索引、裁剪和分步作业验证。
- S1c：地形、路基、道路、树群、农田、墙/桥/路边物件完整复刻；临时参考资产绑定。
- S1d：固定观察点图像、生成计数、生命周期、Android/iOS GPU 帧耗时与带宽验收；通过后才能进入 S2。
- S2：自有美术替换与材质/LOD 验证，检查所有临时素材引用已经清除。
- S3：路径可控、自定义地标等需求另行验收。

## 使用调试框架

直接打开 `Assets/Scenes/SlowRoadsRoadDemo.unity`。配置资产为 `Assets/SlowRoads/Settings/Debug/HillsDebugSettings.asset`。旧 Slow Roads 菜单及创建器已经移除；不要重新引入自动覆盖场景、清空材质/Prefab 目录的生成器。

进入 Play，选择 `Hills Scene`：Pause / Resume、Step Forward、Reset Fixed Seed；Inspector 展示里程和保留节点数。SO 控制移动、相机、道路可见性、Gizmos、速度与镜头参数。seed、道路参数、道路可见性、Gizmos 与窗口大小在 Reset 时生效；移动/相机开关和速度实时生效。

当前只显示灰色道路和橙色 Cube，背景纯色；没有地形、植被、车辆、音频、物理控制。相机在 LateUpdate 运行，指数阻尼，不依赖输入系统。

性能边界：单个观察者的 CPU 路径采样与相机更新规模很小；旧道路在节点变化时重建有限窗口网格，仍有 CPU 分配和线性查询，不可用于正式大规模场景。当前无 GPU Compute、额外 Pass、RT 或全屏 Blit；预览材质使用 URP Unlit 且 enableInstancing，没有新增 Shader keyword。正式 S1 移植必须替换旧道路重建和调度路径，不能把该预览的性能当作最终移动端表现。
