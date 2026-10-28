# Rendering 开发边界

- 继承根 AGENTS.md 的 URP/移动端/variant 规则。当前仅保留有参考材质依赖的 Legacy 树 Shader，不代表已通过原版效果验收。
- 新 Feature、Pass、Compute 与 Shader 分目录、分职责。配置实例位于 Settings/Hills，不把美术资源塞入渲染代码目录。
- 每个 Pass 说明 RenderPassEvent、输入输出、RT 生命周期、是否触发额外深度或颜色拷贝；Frame Debugger 验证实际次数。
- 大规模植被采用 Chunk/Cluster + Indirect + GPU 剔除/LOD；明确平台支持检查与 fallback，禁止重新接回旧全场草地实验实现。
- Variant 必须列出局部 keyword 组合、URP 叠加项及 Instancing；不要声称 enableInstancing 单独保证了批处理。
- 提交优化结论区分 CPU 提交成本与 GPU 带宽/overdraw；没有目标移动设备实测时标记未测。
