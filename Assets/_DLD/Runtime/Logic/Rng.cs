using System;
using System.Collections.Generic;

namespace DLD.Logic
{
    /// <summary>可复现的随机数（mulberry32），和原型同种子同序列</summary>
    public class Rng
    {
        uint a;
        public Rng(long seed) { a = (uint)(seed & 0xFFFFFFFF); }
        public double Next()
        {
            unchecked
            {
                a += 0x6d2b79f5;
                uint t = a;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
        public double Range(double lo, double hi) => lo + (hi - lo) * Next();
        public double Range(double[] r) => Range(r[0], r[1]);
        public int Int(int lo, int hi) => lo + (int)Math.Floor(Next() * (hi - lo + 1));
        public T Pick<T>(IList<T> arr) => arr[(int)Math.Floor(Next() * arr.Count)];
        public bool Chance(double p) => Next() < p;
        public List<T> Shuffle<T>(List<T> arr)
        {
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(Next() * (i + 1));
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }
        /// <summary>带权选择：顺序敏感，保持和表里写的一样</summary>
        public string Weighted(IList<(string k, double w)> items)
        {
            double sum = 0; string last = null;
            foreach (var (k, w) in items) if (w > 0) { sum += w; last = k; }
            double r = Next() * sum;
            foreach (var (k, w) in items) { if (w <= 0) continue; r -= w; if (r <= 0) return k; }
            return last;
        }
    }
}
