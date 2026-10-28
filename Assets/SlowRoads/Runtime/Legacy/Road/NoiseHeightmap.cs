using UnityEngine;

namespace SlowRoads
{
    /// <summary>
    /// 旧调试用分形值噪声高度图，非原版 Hills 高度场的等价实现。
    ///
    /// 原版 Pl 使用 Alea 与多尺度紧支撑径向核，并含 temper 调制。
    /// 本类只给旧道路预览提供起伏；正式移植依据 Docs/ReverseEngineering/SceneArchitecture。
    ///
    /// GetHeight(x, z) 返回世界单位高度（可正可负，海平面≈0）。
    /// </summary>
    public class NoiseHeightmap
    {
        readonly int   m_Seed;
        readonly float m_Amplitude;   // 总高度幅度（世界单位）
        readonly float m_BaseScale;   // 基频尺度（值越大山丘越小）
        readonly int   m_Octaves;     // 叠加层数
        readonly float m_Persistence; // 每层幅度衰减
        readonly float m_Lacunarity;  // 每层频率倍增
        readonly float m_Offset;      // 整体高度偏移

        public NoiseHeightmap(int seed, float amplitude = 120f, float baseScale = 0.0006f,
                              int octaves = 5, float persistence = 0.5f,
                              float lacunarity = 2f, float offset = 0f)
        {
            m_Seed = seed;
            m_Amplitude = amplitude;
            m_BaseScale = baseScale;
            m_Octaves = octaves;
            m_Persistence = persistence;
            m_Lacunarity = lacunarity;
            m_Offset = offset;
        }

        /// <summary>采样世界坐标 (x,z) 处的高度。</summary>
        public float GetHeight(float x, float z)
        {
            float sum = 0f, amp = 1f, freq = m_BaseScale, norm = 0f;
            for (int i = 0; i < m_Octaves; i++)
            {
                sum  += ValueNoise(x * freq, z * freq, m_Seed + i * 1013) * amp;
                norm += amp;
                amp  *= m_Persistence;
                freq *= m_Lacunarity;
            }
            return (sum / norm - 0.5f) * 2f * m_Amplitude + m_Offset;   // 归一到 [-1,1] 再缩放
        }

        /// <summary>带 smootherstep 插值的 2D 值噪声（返回 [0,1]）。</summary>
        static float ValueNoise(float x, float z, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int zi = Mathf.FloorToInt(z);
            float xf = x - xi;
            float zf = z - zi;

            // 4 个格子角点的伪随机值
            float a = Hash(xi,     zi,     seed);
            float b = Hash(xi + 1, zi,     seed);
            float c = Hash(xi,     zi + 1, seed);
            float d = Hash(xi + 1, zi + 1, seed);

            // smootherstep：6t^5 - 15t^4 + 10t^3（原项目 smootherLerp 同款曲线）
            float u = xf * xf * xf * (xf * (xf * 6f - 15f) + 10f);
            float v = zf * zf * zf * (zf * (zf * 6f - 15f) + 10f);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>整数坐标 → [0,1) 的稳定伪随机哈希。</summary>
        static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 374761393 + x * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h = h * 374761393 + z * 668265263;
                h = (h ^ (h >> 16));
                return (h & 0x00FFFFFF) / (float)0x01000000;
            }
        }
    }
}
