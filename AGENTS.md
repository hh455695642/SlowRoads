# Slow Roads Hills / Unity 6.3 URP

## 项目目标与阶段门禁
- 仅复刻 Slow Roads v1.0.2 丘陵场景。先完整复刻算法和渲染，临时沿用现有参考美术；算法与效果验收后全部替换为自有美术；确认替换效果后才增加路径控制、地标插入等改造。
- 禁止接入车辆、驾驶、轮胎、动力、碰撞驾驶系统，禁止海岸线、California、OffWorld。观察者仅 Cube 沿轨迹移动与相机跟随；原版 vehicleIndex 在生成逻辑中映射为观察者进度，不引入车辆依赖。
- 当前处于 S0（agent 约定与调试基础设施）。旧 NoiseHeightmap/RoadMidline 是简化近似，不是算法复刻验收结果。不得把“能跑/看起来像”标记为完整复刻。
- S1：原算法与渲染复刻；S2：替换全部美术并验证；S3：路径控制和地标等扩展。禁止跨阶段改写参考算法。

## 证据与工作入口
- 首读 `Docs/Agents/HillsAgents.md`，按职责选择证据和实现范围。
- 原版 Slow Roads v1.0.2 安装文件位于 `E:\UnityProject\Unity6\SlowRoads\Slow Roads`（仓库根目录 `Slow Roads/`）；`Slow Roads/resources/app.asar` 是本地参考源码的权威输入。整个 `Slow Roads/` 由根目录 `.gitignore` 排除，不提交安装包、解包副本或可执行文件。旧文档里的 OneDrive `extracted_correct` 路径仅是历史证据路径；新增原版验证脚本须从当前 `app.asar` 读取并核对哈希。
- 原始依据：`Docs/ReverseEngineering/SceneArchitecture/source-manifest.json` 与 `SourceAtlas/*.source.txt`；架构报告解释调用链；`Docs/ReverseEngineering/RoadAudit/道路生成逆向对照报告.md` 记录现有实现差异。
- 每次移植记录原符号、摘录行号、配置、seed、坐标/随机数约定、验证结果与未复刻项。摘录行前的 Lxxxxx 是原格式化文件行号，不一定等于摘录文件物理行号。
- 不修改参考摘录以迎合实现。阶段验收以同 seed、同配置、同采样点的参考输出比较为依据。
- 模型/场景贴图以 `Docs/Assets/HillsAssetManifest.json` 为当前来源清单；新增前确认 Hills 实际引用，检查源码内嵌 data URI。不得按名字误删丘陵共享水面/墙体资源，不得按旧全量资产清单恢复车辆/其他场景资源。对象空间法线与遮罩保留原始通道语义，不能一律设为 Unity NormalMap。

## 实现约束
- Unity 项目开发优先使用 `unity` CLI：先执行 `unity status --format json` 核对当前项目与 Editor 的 `ready` 状态，再通过 `unity command` 操作场景、GameObject、Prefab、`.asset`、Play Mode 和测试；命令调用标注 `--caller plugin --skill unity-cli`。脚本源码可用文件工具编辑，随后用 CLI 触发重编译和验证。连接失败时先排查 CLI、Pipeline 包和编译状态，不直接手改运行中 Editor 的场景或资源 YAML。
- 移动端优先，URP，Android Mali/Adreno 与 iOS Metal；禁止 Geometry Shader。
- 新的自定义渲染功能采用独立 RendererFeature / RenderPass，SO 配置与功能开关；标注 RenderPassEvent、性能关键点和扩展点。不侵入 RenderPipeline 主流程，不构建超级 Feature。
- 纯生成、轨迹移动、摄像机跟随不属于渲染 Pass；调试预览复用 URP 内置不透明材质，不为它添加空 Pass、RT 或全屏 Blit。
- Shader 必须支持 multi_compile_instancing；其他可选 keyword 默认 shader_feature_local，强度与简单开关优先 uniform；列出 variant 组合与 URP 叠加风险，移动端目标 <200。优先 half，世界坐标/距离使用足够精度。
- 大规模实例默认 GPU Indirect + Compute 剔除 + Chunk/Cluster + LOD；不支持时必须有明确降级，不逐实例每帧 CPU 循环。植被/角色/特效独立 Shader。
- 减少 DrawCall、SetPass、overdraw、带宽和 RT 切换；必要 Depth Prepass → Opaque → Transparent → 最小后处理。禁止为未来功能预先增加 Pass/MRT。
- Debug 必须可关闭；使用 Frame Debugger/RenderDoc/Profiler，避免复杂调试系统。性能结论必须区分 CPU 与 GPU，并附设备、分辨率、配置和测量依据。
- 唯一开发场景为 `Assets/Scenes/SlowRoadsRoadDemo.unity`，直接在该场景迭代。禁止再创建 HillsAgentDebug 或其他平行演示场景、禁止恢复旧场景创建菜单。保持场景 GUID，不整场重建覆盖；按需修改现有层级。Build Settings 只在任务需要时调整。
- 目录规范见 `Docs/Agents/ProjectLayout.md`；业务代码、工具、SO、临时美术、渲染分层存放。资源移动必须保留 .meta/GUID，优先 AssetDatabase.MoveAsset；删除代码前检查场景/Prefab/类型引用。
- 固定 seed 不污染全局随机数状态。运行时资源必须释放。

## 交付
- 默认直接实现、关键注释、扩展点、实际验证结果、剩余缺口；不做基础教学。
- agent 是职责结构，不强制并行。只有任务明确授权时才启动子 agent；各职责共享契约，禁止多个 agent 同时写同一文件。
- 任务开始检查当前场景、Play 状态、未保存修改及相关目录 AGENTS.md。用户明确指定的场景与范围优先于旧任务文档。
- 不保留重复实现、过期菜单、一键清空目录式生成工具。历史证据保留在 Docs，不能将旧审计路径当作当前代码入口。
- 迁移后必须检查 missing scripts、序列化对象引用、材质 Shader、Build Settings 场景路径及 Play Mode 帧/里程推进；仅编译成功不能作为场景可用的证明。
- 当前 Debug 框架是主场景观察工具，不因目录名包含 Debug 就删除。清理仅删除确无依赖且不属于阶段目标或证据的文件。完成一次性迁移后清理脚本、暂存副本和截图，将有效审计信息合并到权威清单；不要持续新增重复验证文档。
