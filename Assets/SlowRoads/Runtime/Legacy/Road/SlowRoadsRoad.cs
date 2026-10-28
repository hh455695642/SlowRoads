using UnityEngine;
using System.Collections.Generic;

namespace SlowRoads
{
    /// <summary>
    /// 道路生成器：拥有高度图 + 中线生成器 + 路面 mesh，负责「持续程序化生成」。
    ///
    /// 每帧由观察者调用 Sync(travelDist)：
    ///   - 向前生成节点，保证观察者前方始终有 horizonAhead 长度的路；
    ///   - 丢弃观察者后方超过 keepBehind 的节点（滑动窗口）；
    ///   - 从当前窗口重建路面 mesh（两条边组成的带状网格）。
    ///
    /// 路面 mesh 结构（对齐原项目 D 类）：每个节点 2 个顶点（左、右边缘），
    ///   左 = pos + normal * halfWidth，右 = pos - normal * halfWidth，
    ///   相邻节点间用 2 个三角形拼成一个面片。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SlowRoadsRoad : MonoBehaviour
    {
        [Header("地形")]
        public int    seed = 1337;
        public float  heightAmplitude = 120f;
        public float  heightBaseScale = 0.0006f;
        public int    heightOctaves = 5;

        [Header("道路生成")]
        public RoadGenConfig gen = RoadGenConfig.Default;

        [Header("流式")]
        [Tooltip("观察者前方保留的节点数")] public int horizonAhead = 120;
        [Tooltip("观察者后方保留的节点数")] public int keepBehind = 30;

        [Header("路径避让")]
        [Tooltip("避让检测时忽略最近 N 个节点（正在走的路不参与回环判定）")] public int avoidLookback = 40;

        [Header("调试")]
        public bool showGizmos = true;

        NoiseHeightmap m_Hm;
        RoadMidline    m_Midline;

        readonly List<RoadNode> m_Nodes    = new List<RoadNode>();
        readonly List<float>    m_CumDist  = new List<float>();   // 每个节点累计里程
        float m_BaseDist = 0f;                                    // 首节点对应的绝对里程

        Mesh m_Mesh;
        Material m_OwnedMaterial;
        MeshFilter m_MeshFilter;
        MeshRenderer m_MeshRenderer;

        public int NodeCount => m_Nodes.Count;
        public float TotalLength => (m_Nodes.Count == 0) ? 0f : m_BaseDist + m_CumDist[m_CumDist.Count - 1];
        /// <summary>当前节点窗口（供草地等系统沿路散布）。</summary>
        public IReadOnlyList<RoadNode> Nodes => m_Nodes;

        /// <summary>采样地形高度（供草地等其它系统复用同一份高度图）。</summary>
        public float GetHeight(float x, float z) => (m_Hm != null) ? m_Hm.GetHeight(x, z) : 0f;

        void Awake()
        {
            m_MeshFilter = GetComponent<MeshFilter>();
            m_MeshRenderer = GetComponent<MeshRenderer>();

            m_Hm = new NoiseHeightmap(seed, heightAmplitude, heightBaseScale, heightOctaves);
            InitStartNode();

            // 简单灰色材质（仅用于可见性，原项目道路贴图/材质不在本次范围内）
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = "Road_Placeholder" };
            m_OwnedMaterial = mat;
            mat.enableInstancing = true;
            mat.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.45f, 1f));
            mat.SetFloat("_Cull", 0f);   // 双面，避免朝向问题
            m_MeshRenderer.sharedMaterial = mat;
        }

        /// <summary>在随机合法位置放置起始节点，并沿等高线给定初始朝向。</summary>
        void InitStartNode()
        {
            float x = 0f, z = 0f;
            // 固定 seed 必须同时控制起点；不污染 UnityEngine.Random 全局状态。
            var random = new System.Random(seed);
            // 随机找一处海拔合法（高于海平面）的点作为起点
            for (int i = 0; i < 200; i++)
            {
                x = (float)random.NextDouble() * 600f - 300f;
                z = (float)random.NextDouble() * 600f - 300f;
                float h = m_Hm.GetHeight(x, z);
                if (h > gen.minElev + 5f) break;
            }
            float h0 = Mathf.Max(m_Hm.GetHeight(x, z), gen.minElev) - gen.roadSink;
            float ang = RoadMidline.ComputeStartAngle(m_Hm, x, z);
            Vector2 n = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang));

            m_Midline = new RoadMidline(m_Hm, gen, new RoadNode(new Vector3(x, h0, z), ang, n), NearestNodeDistance);
            m_Nodes.Add(m_Midline.Tail);
            m_CumDist.Add(0f);
        }

        /// <summary>查询某点到「较久之前」历史路径的最近距离（供避让用，忽略最近 avoidLookback 个节点）。</summary>
        float NearestNodeDistance(float x, float z)
        {
            int upto = m_Nodes.Count - avoidLookback;
            if (upto <= 0) return float.MaxValue;

            float best = float.MaxValue;
            for (int i = 0; i < upto; i++)
            {
                float dx = m_Nodes[i].pos.x - x;
                float dz = m_Nodes[i].pos.z - z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>预热生成指定数量的节点（供草地等系统在观察者出发前拿到整段路径）。</summary>
        public void Prewarm(int nodeCount)
        {
            while (m_Nodes.Count < nodeCount)
            {
                RoadNode next = m_Midline.Step();
                float seg = Vector3.Distance(next.pos, m_Nodes[m_Nodes.Count - 1].pos);
                m_Nodes.Add(next);
                m_CumDist.Add(m_CumDist[m_CumDist.Count - 1] + seg);
            }
            RebuildMesh();
        }

        /// <summary>按观察者里程推进：向前生成、向后裁剪、重建 mesh。</summary>
        public void Sync(float travelDist)
        {
            bool dirty = false;

            // 向前生成，直到覆盖 travelDist + horizonAhead 里程
            float front = m_BaseDist + m_CumDist[m_CumDist.Count - 1];
            float target = travelDist + horizonAhead * gen.nodeSpacing;
            while (front < target)
            {
                RoadNode next = m_Midline.Step();
                m_Nodes.Add(next);
                float seg = Vector3.Distance(next.pos, m_Nodes[m_Nodes.Count - 2].pos);
                front += seg;
                m_CumDist.Add(front - m_BaseDist);
                dirty = true;
            }

            // 向后裁剪
            float backTarget = travelDist - keepBehind * gen.nodeSpacing;
            while (m_Nodes.Count > 2 && m_BaseDist + m_CumDist[1] < backTarget)
            {
                m_Nodes.RemoveAt(0);
                float removed = m_CumDist[1];
                m_CumDist.RemoveAt(0);
                m_BaseDist += removed;
                for (int i = 0; i < m_CumDist.Count; i++) m_CumDist[i] -= removed;
                dirty = true;
            }

            if (dirty) RebuildMesh();
        }

        /// <summary>采样里程 dist 处的道路姿态。</summary>
        public void Sample(float dist, out Vector3 pos, out Vector3 forward, out Vector3 up)
        {
            up = Vector3.up;
            pos = Vector3.zero;
            forward = Vector3.forward;

            if (m_Nodes.Count < 2) return;

            float local = dist - m_BaseDist;
            // 找到区间 [i, i+1]
            int i = 0;
            while (i < m_CumDist.Count - 1 && m_CumDist[i + 1] < local) i++;
            int j = Mathf.Min(i + 1, m_CumDist.Count - 1);

            float d0 = m_CumDist[i], d1 = m_CumDist[j];
            float t = (d1 - d0) > 1e-4f ? Mathf.Clamp01((local - d0) / (d1 - d0)) : 0f;

            RoadNode a = m_Nodes[i];
            RoadNode b = m_Nodes[j];
            pos = Vector3.Lerp(a.pos, b.pos, t);
            float ang = Mathf.LerpAngle(a.angle * Mathf.Rad2Deg, b.angle * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
            forward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;

            // 上方向：用相邻节点坡度做一个近似地表法线
            float hBack = (i > 0) ? m_Nodes[i - 1].pos.y : a.pos.y;
            float hFwd  = (j < m_Nodes.Count - 1) ? m_Nodes[Mathf.Min(j + 1, m_Nodes.Count - 1)].pos.y : b.pos.y;
            Vector3 slope = new Vector3(forward.x, 0f, forward.z) * (hFwd - hBack);
            up = (Vector3.up - slope * 0.4f).normalized;
        }

        /// <summary>从当前节点窗口重建路面带状 mesh。</summary>
        void RebuildMesh()
        {
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "SlowRoadsRoadMesh" };
                m_MeshFilter.sharedMesh = m_Mesh;
            }
            m_Mesh.Clear();

            int n = m_Nodes.Count;
            if (n < 2) return;

            var verts = new Vector3[n * 2];
            var norms = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];

            float w = gen.halfWidth;
            for (int i = 0; i < n; i++)
            {
                RoadNode nd = m_Nodes[i];
                Vector3 left  = new Vector3(nd.normal.x, 0f, nd.normal.y);
                Vector3 center = nd.pos + Vector3.up * gen.roadHeight;

                verts[i * 2]     = center + left * w;
                verts[i * 2 + 1] = center - left * w;
                norms[i * 2]     = Vector3.up;
                norms[i * 2 + 1] = Vector3.up;
                uvs[i * 2]       = new Vector2(0f, i);
                uvs[i * 2 + 1]   = new Vector2(1f, i);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                int t = i * 6;
                tris[t] = a; tris[t + 1] = c; tris[t + 2] = b;   // 面朝上
                tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
            }

            m_Mesh.vertices = verts;
            m_Mesh.normals = norms;
            m_Mesh.uv = uvs;
            m_Mesh.triangles = tris;
            m_Mesh.RecalculateBounds();
        }

        void OnDrawGizmos()
        {
            if (!showGizmos || m_Nodes == null || m_Nodes.Count < 2) return;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
            for (int i = 0; i < m_Nodes.Count - 1; i++)
                Gizmos.DrawLine(m_Nodes[i].pos, m_Nodes[i + 1].pos);
        }

        void OnDestroy()
        {
            if (m_Mesh != null) Destroy(m_Mesh);
            if (m_OwnedMaterial != null) Destroy(m_OwnedMaterial);
        }
    }
}

