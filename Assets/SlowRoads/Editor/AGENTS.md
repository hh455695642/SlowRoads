# Editor 开发边界

- 继承项目根 AGENTS.md。所有编辑器代码必须位于此目录或后续显式 Editor 程序集。
- 不恢复 Slow Roads 顶层菜单、演示场景创建器、全量材质/Prefab 清空再生成工具。
- 当前开发入口是 Assets/Scenes/SlowRoadsRoadDemo.unity；调试按钮仅在 HillsDebugSession Inspector 提供。
- 新工具围绕选中对象或显式资产运行，必须有明确输出路径与覆盖边界；必要时使用 Undo、SerializedObject、SetDirty 和 SaveAssets。
- 资源迁移使用 AssetDatabase.MoveAsset 保留 GUID；创建父目录后先保证 AssetDatabase 已识别，再批量移动。StartAssetEditing 必须用 finally 配对 StopAssetEditing。
- 临时迁移脚本放 Temp，完成后不留为业务菜单或常驻 InitializeOnLoad。禁止将备份 .cs 放进 Assets。
