using UnityEngine;
using System.Collections.Generic;

namespace SlowRoads
{
    /// <summary>道路中线节点：世界位置 + 朝向 + 左法线。</summary>
    public struct RoadNode
    {
        public Vector3 pos;      // 世界坐标（y 为地形高度，已减去 roadSink）
        public float   angle;    // 朝向（弧度）
        public Vector2 normal;   // 左侧垂直方向（XZ 平面单位向量）

        public RoadNode(Vector3 p, float a, Vector2 n)
        {
            pos = p; angle = a; normal = n;
        }
    }

    /// <summary>道路生成配置（对齐原项目 P 常量表）。</summary>
    [System.Serializable]
    public struct RoadGenConfig
    {
        [Header("Feel 采样")]
        [Tooltip("前方左右采样距离")] public float feelDist;
        [Tooltip("左右采样夹角（弧度）")] public float feelAng;
        [Tooltip("节点间距")] public float nodeSpacing;

        [Header("地形约束")]
        [Tooltip("最大纵向/横向坡度")] public float maxGrad;
        [Tooltip("最低海拔（海平面，低于此算水域）")] public float minElev;
        [Tooltip("道路下沉量（嵌入地表）")] public float roadSink;

        [Header("平滑与转向")]
        [Tooltip("bT 平滑系数")] public float smoothFactor;
        [Tooltip("每节点最大转向角（弧度）")] public float maxTurnDelta;
        [Tooltip("直行偏置（0=纯等高线，越大越倾向于直行）")] public float straightBias;

        [Header("路径避让（防回环）")]
        [Tooltip("新节点与历史路径的最小允许距离（小于则强制偏转）")] public float avoidDist;

        [Header("道路尺寸")]
        [Tooltip("半幅路面宽")] public float halfWidth;
        [Tooltip("路面高出地形的高度")] public float roadHeight;

        public static RoadGenConfig Default => new RoadGenConfig
        {
            feelDist = 12f,
            feelAng = 0.5f,          // ≈28.6°
            nodeSpacing = 8f,
            maxGrad = 0.18f,
            minElev = 1f,
            roadSink = 0.3f,
            smoothFactor = 1f,
            maxTurnDelta = 0.7f,
            straightBias = 0.22f,
            halfWidth = 6f,
            roadHeight = 0.35f,
            avoidDist = 22f,
        };
    }

    /// <summary>
    /// 道路中线生成器 —— 复刻原项目 extendMidline 的核心「贴地感受 + 坡度转向」。
    ///
    /// 每个节点的推进流程（对齐 DevMidlineGenerator.extendMidline）：
    ///   1. Feel：在当前朝向 a 的前方左右各 feelAng 角度、feelDist 距离处采样高度，
    ///      得到 feelL（左前）、feelR（右前）、feelA（当前点）。
    ///   2. 计算 feelAvg=(R+L)/2、feelDif=(R-L)/2，
    ///      以及纵向/横向坡度 gradLon、gradLat。
    ///   3. 转向偏置 bT = clamp((feelAvg - feelA) / feelDif, -1, 1)，
    ///      它让道路在爬坡时转向缓坡一侧（沿等高线盘旋）。
    ///   4. 新朝向 bDir = a - bT * feelAng，并按 maxTurnDelta 限幅，
    ///      再叠加 straightBias 的直行偏置（简化版防回环，替代原项目 turnback 机制）。
    ///   5. 新节点位置 = 沿 bDir 前进 nodeSpacing，高度 = max(地形高度, minElev) - roadSink。
    /// </summary>
    public class RoadMidline
    {
        readonly NoiseHeightmap m_Hm;
        readonly RoadGenConfig  m_Cfg;
        readonly System.Func<float, float, float> m_NearestDist;  // (x,z) -> 到历史路径最近距离

        RoadNode m_Tail;
        float    m_SmoothedBT;   // bT 的一阶平滑

        public RoadNode Tail => m_Tail;

        /// <param name="nearestDist">可选：查询某点到历史路径的最近距离，用于防回环。</param>
        public RoadMidline(NoiseHeightmap hm, RoadGenConfig cfg, RoadNode start,
                           System.Func<float, float, float> nearestDist = null)
        {
            m_Hm = hm;
            m_Cfg = cfg;
            m_Tail = start;
            m_SmoothedBT = 0f;
            m_NearestDist = nearestDist;
        }

        /// <summary>推进一个节点，返回新节点并更新 tail。</summary>
        public RoadNode Step()
        {
            RoadGenConfig c = m_Cfg;
            float a = m_Tail.angle;
            float x = m_Tail.pos.x;
            float z = m_Tail.pos.z;

            // ---- 1. Feel：前方左右采样高度 ----
            float feelL = m_Hm.GetHeight(x + Mathf.Cos(a - c.feelAng) * c.feelDist,
                                         z + Mathf.Sin(a - c.feelAng) * c.feelDist);
            float feelR = m_Hm.GetHeight(x + Mathf.Cos(a + c.feelAng) * c.feelDist,
                                         z + Mathf.Sin(a + c.feelAng) * c.feelDist);
            float feelA = Mathf.Max(m_Hm.GetHeight(x, z), c.minElev);
            float feelAvg = (feelR + feelL) * 0.5f;
            float feelDif = (feelR - feelL) * 0.5f;

            // ---- 2. 坡度（纵向 lon / 横向 lat）----
            float gradLon = (feelAvg - feelA) / c.feelDist / c.maxGrad;   // 正 = 上坡
            float gradLat = (feelL - feelR) / c.feelDist / c.maxGrad;

            // ---- 3. 转向偏置 bT（原项目核心公式）----
            float bT = 0f;
            if (Mathf.Abs(feelDif) > 1e-4f)
                bT = Mathf.Clamp((feelAvg - feelA) / feelDif, -1f, 1f);

            // 一阶平滑（对齐原项目 smoothFactor + 抗抖）
            m_SmoothedBT = Mathf.Lerp(m_SmoothedBT, bT, 0.35f);
            bT = m_SmoothedBT * c.smoothFactor;

            // ---- 4. 新朝向（等高线转向 + 直行偏置 + 限幅）----
            float bDirContour = a - bT * c.feelAng;
            float bDir = Mathf.LerpAngle(a, bDirContour, 1f - c.straightBias);
            bDir = ClampAngleDelta(a, bDir, c.maxTurnDelta);

            // ---- 5. 新节点 ----
            float dx = Mathf.Cos(bDir);
            float dz = Mathf.Sin(bDir);
            float nx = x + dx * c.nodeSpacing;
            float nz = z + dz * c.nodeSpacing;

            // ---- 6. 路径避让（防回环，对应原项目 turnback/hasPrev 的简化版）----
            // 若新节点太靠近历史路径，说明正在绕圈回到旧路，向两侧探测并偏转朝向避开。
            if (m_NearestDist != null && c.avoidDist > 0f)
            {
                float d = m_NearestDist(nx, nz);
                if (d < c.avoidDist)
                {
                    for (int k = 0; k < 10 && d < c.avoidDist; k++)
                    {
                        float probe = (k + 1) * 0.14f;   // 逐步加大偏转角
                        float rx = x + Mathf.Cos(bDir + probe) * c.nodeSpacing;
                        float rz = z + Mathf.Sin(bDir + probe) * c.nodeSpacing;
                        float rd = m_NearestDist(rx, rz);
                        float lx = x + Mathf.Cos(bDir - probe) * c.nodeSpacing;
                        float lz = z + Mathf.Sin(bDir - probe) * c.nodeSpacing;
                        float ld = m_NearestDist(lx, lz);

                        if (rd >= ld && rd > d)          { bDir += probe; nx = rx; nz = rz; d = rd; }
                        else if (ld > rd && ld > d)      { bDir -= probe; nx = lx; nz = lz; d = ld; }
                        else break;                       // 两侧都避不开，保持原样
                    }
                }
            }

            float ny = Mathf.Max(m_Hm.GetHeight(nx, nz), c.minElev) - c.roadSink;
            dx = Mathf.Cos(bDir);
            dz = Mathf.Sin(bDir);
            Vector2 normal = new Vector2(-dz, dx);   // 左法线 = 朝向逆时针 90°

            m_Tail = new RoadNode(new Vector3(nx, ny, nz), bDir, normal);
            return m_Tail;
        }

        /// <summary>把目标角 target 限制在 current ± maxDelta 内。</summary>
        static float ClampAngleDelta(float current, float target, float maxDelta)
        {
            float delta = Mathf.DeltaAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            delta = Mathf.Clamp(delta, -maxDelta, maxDelta);
            return current + delta;
        }

        /// <summary>在给定点沿等高线（垂直于梯度）计算一个合适的起始朝向。</summary>
        public static float ComputeStartAngle(NoiseHeightmap hm, float x, float z)
        {
            float e = hm.GetHeight(x + 2f, z);
            float w = hm.GetHeight(x - 2f, z);
            float n = hm.GetHeight(x, z + 2f);
            float s = hm.GetHeight(x, z - 2f);
            Vector2 grad = new Vector2(e - w, n - s);         // 上坡方向
            Vector2 contour = new Vector2(-grad.y, grad.x);   // 垂直于梯度 = 等高线方向
            if (contour.sqrMagnitude < 1e-6f) contour = new Vector2(1f, 0f);
            return Mathf.Atan2(contour.y, contour.x);
        }
    }
}
