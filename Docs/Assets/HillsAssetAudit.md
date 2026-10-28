# 丘陵模型与贴图审计

日期：2026-10-28。范围：Slow Roads v1.0.2 Hills 四季场景的模型、场景贴图及相关旧材质/Prefab。本报告仅审计模型/贴图；随后项目清理已删除未接入的音频、字体和 UI 图标。

## 结果

| 项目 | 数量 |
|---|---:|
| 保留并验证的丘陵模型 | 9（8 个树模型、1 个路边标柱） |
| 保留并验证的丘陵贴图 | 95 |
| 补齐内嵌资源 | 5（1 模型、4 贴图） |
| 删除范围外/未被 Hills 引用的模型 | 56 |
| 删除范围外/重复版本贴图 | 86 |
| 删除对应无用材质 / Prefab | 9 / 9 |
| 保留原 GUID 的原有资源 | 99 |
| 修正类型/色彩空间等导入配置 | 24 |
| 恢复原尺寸的 4096 宽图集 | 29 |

原有 99 项模型字节/贴图解码像素与正确解包源一致，并无文件损坏。本次发现的缺失来自旧导入器只枚举外部文件、遗漏 JS 内嵌数据；其余问题来自场景范围混入、重名版本归类和 Unity 导入设置。

## 证据链

- 以 `HillsHeightmap.2938f21e.js` 的四季 maps、模型列表和路边对象表，以及其共享依赖 `tree_04.6b3d8a5d.js` 为依据；不是把所有通用模块的资产都算作丘陵依赖。
- 树模型列表为 `[Qr,Yr,Kr,qr]` 和 `[ps,ms,vs,As]`，对应 tree_01～08.obj；路边物件表 `sl.VergeMarker.src = _s` 指向内嵌 OBJ。GLB 橡/松/棕榈树不在该 Hills 列表。
- 保留四季全部变体。`sea_waves` 由 Hills 水面引用，不能按“海岸线”名字删除；墙体中 seaWall 同理。道路、地形、墙体、桥梁的程序化几何不是缺失的模型文件。
- `extracted_correct/build` 下 99 个外部模型/贴图与两个依赖模块，共 101 个文件，逐字节对照原始 `resources/app.asar` 通过。记录见 清单中的 `archiveSources`。
- 最终 104 项均验证内容；PNG 按 RGBA 像素与尺寸比较，OBJ 按字节比较。完整清单与删除列表见 `HillsAssetManifest.json`，其中含来源模块、原 hash 文件名/内嵌符号与当前路径。

## 补齐的内嵌资源

| 原符号 | 当前文件 | 用途 |
|---|---|---|
| `_s` | Models/Props/verge_marker.obj | Hills 路边标柱 |
| `Ee` | Textures/Walls/wall_barrier_normal.png | 护栏法线数据 |
| `Te` | Textures/Walls/wall_fence_normal.png | 木栏法线数据 |
| `Z` | Textures/Utility/placeholder_white.png | 原版初始化占位纹理 |
| `is` | Textures/Utility/snow_particle.png | 雪粒子图案（gi 构造函数的 map 输入） |

以上路径相对 `Assets/SlowRoads/Art/Reference/SlowRoads/`。均从源码 data URI 解码，不是重新生成的近似资源。

## 修正

- 删除车辆模型/贴图、地外与其他场景资源、菜单背景、未被丘陵引用的 GLB 树及三个旧道具。同步删除关联旧材质与 Prefab；删除前反向依赖检查没有发现保留资产对它们的引用。
- 去除同名其他 hash 版本，将实际 Hills 版本迁入无 `_2` 后缀的规范路径；岩石、冬季地表、道路、墙体等移出错误的 Vegetation 分类。移动使用 AssetDatabase，GUID 不变。
- 对象空间法线保留 Default 纹理类型与线性 RGB，避免 Unity NormalMap 重打包。数据/遮罩/噪声使用线性空间，颜色贴图保留 sRGB；不启用 CPU Read/Write。详见 清单中的 `importCorrections`。
- 29 张图集取消默认 2048 上限导致的缩小，恢复源文件 4096 宽；详见 清单中的 `resolutionCorrections`。这是参考资产精度基线，移动发布仍须结合实际 Shader、LOD、平台压缩及驻留预算验收，并非要求所有原图同时常驻。
- `signs.png` 源文件为 768×256，关闭 NPOT 自动缩放，避免路标图集变为 1024×256。
- 清理 `PC_Renderer.asset` 的 7 条 `probeVolumeResources.*` 失效引用。包内 `Universal/Runtime/Deprecated.cs` 明确标记它们自 2023.3 起废弃并迁往 GraphicsSettings；仅清空已失效旧字段，不改变 RendererFeature、渲染模式或其他管线配置。

## 验证与边界

Unity 导入后 9 个模型均有非空 Mesh，95 个 Texture2D 均与源文件尺寸一致。扫描场景配置、材质、Prefab 和 ScriptableObject，没有缺失脚本、失效材质 Shader 或序列化引用，编译失败标记为 false；详见 清单中的 `unityVerification`。

主场景 Play 回归：帧 2790 → 5095，观察者里程 158.0224 → 290.7368 米，保持一台摄像机，路径窗口为 152 节点。已退出 Play 并恢复后台运行设置，场景仍为 SlowRoadsRoadDemo；最终采样见 清单中的 `playVerification`。

本次不新增运行时场景生成、不修改 Shader 或 keyword；不宣称已有 Legacy 树 Shader 已完整复刻。新标柱仅补齐模型文件，其原版多部件材质语义与实例化仍需后续内容/渲染模块实现。模型与贴图完整性不等于场景效果复刻完成。

一次性文本迁移文件和重复审计快照已清除。旧图片和 Temp 下三处备份/暂存目录的文件系统清理被自动审批拦截，仍留在项目内；详见 Docs/Agents/ProjectLayout.md 的清理状态。权威完整源保留在用户的原版安装归档中。

复查脚本：`python Docs/Assets/audit_hills_assets.py`（需 Pillow）。仅读取 Assets，默认仅写入 Temp/HillsAssetAudit/audit.json；只有显式 --stage 才暂存原版文件，不执行资产删除或移动。历史计划已清除，来源及有效验证信息统一保存在 HillsAssetManifest.json。
