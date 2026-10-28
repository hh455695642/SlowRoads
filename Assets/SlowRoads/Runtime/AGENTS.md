# Runtime 开发边界

- 继承项目根 AGENTS.md；先读 Docs/Agents/ProjectLayout.md。
- Debug 只提供观察者与摄像机，不承载生产场景生成。Legacy/Road 是待替换预览实现，不继续扩展草地/车辆/地形系统。
- 新算法进入 Generation 对应职责目录；先实现可验证的数据算法，再接入场景与渲染。
- 生成输入由 seed、配置和观察者路径进度构成；不要通过 FindFirstObjectByType<Camera> 或具体 Cube 获取算法输入。
- 生命周期必须有明确初始化、增量更新和释放；禁止重复实例化根对象、隐式全局状态、每帧全量重建。
- 与原版比较时记录 Alea 调用顺序、角度单位、世界/局部坐标和数值容差。不要在移动调试框架整理任务中顺手改写生成公式。
- 路径提供者保留 IRouteSource 边界，纯算法不得依赖 UnityEditor；不用 RendererFeature 实现非渲染逻辑。
