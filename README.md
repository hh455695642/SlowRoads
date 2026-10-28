# Slow Roads Hills

Unity 6.3 URP 丘陵场景复刻项目，面向 Android / iOS。

- Unity：`6000.3.22f1`
- URP：`17.0.1`
- 开发场景：`Assets/Scenes/SlowRoadsRoadDemo.unity`
- 当前阶段：S0 调试基础设施；现有高度场与道路是旧版近似实现，尚未完成原算法与渲染复刻。

## 获取与运行

安装 Git LFS 后克隆项目，使用指定版本的 Unity 打开项目根目录并等待资源导入。打开开发场景进入 Play Mode，通过 `Hills Scene` Inspector 暂停、单步或重置固定 seed。

```sh
git lfs install
git clone https://github.com/hh455695642/SlowRoads.git
```

PNG 贴图由 Git LFS 管理。`Assets`（含 `.meta`）、`Packages`、`ProjectSettings` 和 `Docs` 纳入版本控制；Unity 缓存、临时文件与本机编辑器设置不提交。

开发约束与阶段门禁见 [AGENTS.md](AGENTS.md)，职责与验收见 [HillsAgents.md](Docs/Agents/HillsAgents.md)，目录约定见 [ProjectLayout.md](Docs/Agents/ProjectLayout.md)。临时参考素材来源见 [HillsAssetManifest.json](Docs/Assets/HillsAssetManifest.json)，后续在 S2 替换为自有美术。
