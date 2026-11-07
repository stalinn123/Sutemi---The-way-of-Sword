using System;
using System.Collections.Generic;
using System.IO;

namespace Kendo.AI
{
    /// <summary>
    /// Agent 2's learner: an Adaptive-Random-Forest-style online classifier (Gomes et al. 2017, the model the paper uses
    /// through River). Poisson(λ=6) online bagging of Hoeffding trees, each split choosing from a random feature subset,
    /// accuracy-weighted voting, and drift-triggered tree replacement with background trees.
    /// Simplifications versus River's ARFClassifier (documented): DDM drift detection instead of ADWIN, majority-class
    /// leaves. Port of Tools/AI/arf.py. Learns one sample at a time, so it can run every decision tick in the game.
    /// </summary>
    public sealed class AdaptiveRandomForest
    {
        private sealed class Gauss
        {
            public double n, mean, m2, lo = 1e30, hi = -1e30;

            public void Add(double x, double w)
            {
                n += w;
                double d = x - mean;
                mean += w * d / n;
                m2 += w * d * (x - mean);
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }

            public double Cdf(double x)
            {
                if (n <= 0) return 0.0;
                double sd = n > 1 ? Math.Sqrt(Math.Max(0.0, m2) / n) : 0.0;
                if (sd < 1e-6) return x >= mean ? 1.0 : 0.0;
                return 0.5 * (1.0 + Erf((x - mean) / (sd * Math.Sqrt(2.0))));
            }
        }

        private sealed class Node
        {
            public double[] counts;
            public int[] feats;
            public Dictionary<int, Gauss[]> stats;
            public double seenSince;
            public int feature = -1;
            public double threshold;
            public Node left, right;
            public int depth;
            public bool Leaf => feature < 0;
        }

        private sealed class Tree
        {
            private readonly AdaptiveRandomForest forest;
            public Node root;

            public Tree(AdaptiveRandomForest forest)
            {
                this.forest = forest;
                root = forest.NewNode(null, 0);
            }

            public Node Sort(double[] x)
            {
                var n = root;
                while (!n.Leaf) n = x[n.feature] <= n.threshold ? n.left : n.right;
                return n;
            }

            public int Predict(double[] x)
            {
                var c = Sort(x).counts;
                int best = 0;
                double sum = 0;
                for (int i = 0; i < c.Length; i++)
                {
                    sum += c[i];
                    if (c[i] > c[best]) best = i;
                }
                return sum > 0 ? best : 0;
            }

            public void Learn(double[] x, int y, double w)
            {
                var n = Sort(x);
                n.counts[y] += w;
                foreach (var f in n.feats) n.stats[f][y].Add(x[f], w);
                n.seenSince += w;
                if (n.seenSince >= forest.grace && n.depth < forest.maxDepth)
                {
                    n.seenSince = 0;
                    forest.TrySplit(n);
                }
            }
        }

        private sealed class Ddm
        {
            private int n;
            private double p = 1.0, pmin = 1e9, smin = 1e9, psmin = 1e9;
            public void Reset() { n = 0; p = 1.0; pmin = 1e9; smin = 1e9; psmin = 1e9; }

            public int Update(double err)
            {
                n++;
                p += (err - p) / n;
                double sd = Math.Sqrt(p * (1 - p) / n);
                if (n < 30) return 0;
                if (p + sd < psmin) { pmin = p; smin = sd; psmin = p + sd; }
                if (p + sd >= pmin + 3 * smin) return 2;
                if (p + sd >= pmin + 2 * smin) return 1;
                return 0;
            }
        }

        private sealed class Member
        {
            public Tree tree, background;
            public readonly Ddm detector = new Ddm();
            public int correct, seen;
        }

        public readonly int features, classes;
        public readonly double lambda;
        public readonly int grace;
        public readonly int maxDepth = 12;
        public readonly int splits = 10;
        public readonly double delta = 0.01, tau = 0.05;
        private readonly int subspace;
        private readonly KRandom rng;
        private readonly Member[] members;
        public int Drifts { get; private set; }
        public int Samples { get; private set; }

        public AdaptiveRandomForest(int features, int classes, long seed = 1, int models = 10, double lambda = 6.0, int grace = 50)
        {
            this.features = features;
            this.classes = classes;
            this.lambda = lambda;
            this.grace = grace;
            subspace = Math.Max(1, (int)Math.Round(Math.Sqrt(features))) + 1;
            rng = new KRandom(seed);
            members = new Member[models];
            for (int i = 0; i < models; i++) members[i] = new Member { tree = new Tree(this) };
        }

        private int[] Subset()
        {
            var idx = new int[features];
            for (int i = 0; i < features; i++) idx[i] = i;
            int m = Math.Min(subspace, features);
            for (int i = 0; i < m; i++)
            {
                int j = i + rng.Int(features - i);
                int t = idx[i]; idx[i] = idx[j]; idx[j] = t;
            }
            var s = new int[m];
            Array.Copy(idx, s, m);
            Array.Sort(s);
            return s;
        }

        private Node NewNode(double[] counts, int depth)
        {
            var n = new Node { counts = counts != null ? (double[])counts.Clone() : new double[classes], feats = Subset(), depth = depth, stats = new Dictionary<int, Gauss[]>() };
            foreach (var f in n.feats)
            {
                var g = new Gauss[classes];
                for (int c = 0; c < classes; c++) g[c] = new Gauss();
                n.stats[f] = g;
            }
            return n;
        }

        private static double Entropy(double[] c)
        {
            double t = 0;
            foreach (var v in c) t += v;
            if (t <= 0) return 0;
            double h = 0;
            foreach (var v in c)
            {
                if (v > 0) { double p = v / t; h -= p * Math.Log(p, 2); }
            }
            return h;
        }

        private void TrySplit(Node n)
        {
            double tot = 0;
            int nonzero = 0;
            foreach (var v in n.counts) { tot += v; if (v > 0) nonzero++; }
            if (nonzero < 2) return;
            double h0 = Entropy(n.counts);
            double bestGain = 0, second = 0, bestThr = 0;
            int bestF = -1;
            double[] bestL = null, bestR = null;
            foreach (var f in n.feats)
            {
                var st = n.stats[f];
                double lo = 1e30, hi = -1e30;
                foreach (var g in st) { if (g.n > 0) { if (g.lo < lo) lo = g.lo; if (g.hi > hi) hi = g.hi; } }
                if (hi - lo < 1e-9) continue;
                for (int j = 1; j <= splits; j++)
                {
                    double thr = lo + (hi - lo) * j / (splits + 1);
                    var l = new double[classes];
                    var r = new double[classes];
                    double tl = 0, tr = 0;
                    for (int c = 0; c < classes; c++)
                    {
                        l[c] = n.counts[c] * st[c].Cdf(thr);
                        r[c] = n.counts[c] - l[c];
                        tl += l[c];
                        tr += r[c];
                    }
                    if (tl < 1e-9 || tr < 1e-9) continue;
                    double gain = h0 - tl / tot * Entropy(l) - tr / tot * Entropy(r);
                    if (gain > bestGain)
                    {
                        second = Math.Max(second, bestGain);
                        bestGain = gain; bestF = f; bestThr = thr; bestL = l; bestR = r;
                    }
                    else if (gain > second) second = gain;
                }
            }
            if (bestF < 0) return;
            double range = Math.Log(classes, 2);
            double eps = Math.Sqrt(range * range * Math.Log(1.0 / delta) / (2.0 * tot));
            if (bestGain - second > eps || eps < tau)
            {
                n.feature = bestF;
                n.threshold = bestThr;
                n.left = NewNode(bestL, n.depth + 1);
                n.right = NewNode(bestR, n.depth + 1);
                n.stats = null;
            }
        }

        public double[] Proba(double[] x)
        {
            var v = new double[classes];
            double tot = 0;
            foreach (var m in members)
            {
                double w = m.seen > 0 ? (double)m.correct / m.seen : 1.0;
                v[m.tree.Predict(x)] += w;
                tot += w;
            }
            for (int i = 0; i < classes; i++) v[i] = tot > 0 ? v[i] / tot : 1.0 / classes;
            return v;
        }

        public int Predict(double[] x)
        {
            var p = Proba(x);
            int best = 0;
            for (int i = 1; i < p.Length; i++) if (p[i] > p[best]) best = i;
            return best;
        }

        public void Learn(double[] x, int y)
        {
            Samples++;
            foreach (var m in members)
            {
                bool ok = m.tree.Predict(x) == y;
                m.seen++;
                if (ok) m.correct++;
                int level = m.detector.Update(ok ? 0.0 : 1.0);
                if (level >= 1 && m.background == null) m.background = new Tree(this);
                if (level == 2)
                {
                    m.tree = m.background ?? new Tree(this);
                    m.background = null;
                    m.detector.Reset();
                    m.correct = 0;
                    m.seen = 0;
                    Drifts++;
                }
                int w = rng.Poisson(lambda);
                if (w > 0)
                {
                    m.tree.Learn(x, y, w);
                    m.background?.Learn(x, y, w);
                }
            }
        }

        /// <summary>Abramowitz–Stegun 7.1.26 (|error| &lt; 1.5e-7). .NET Standard 2.1 has no Math.Erf.</summary>
        public static double Erf(double x)
        {
            double s = Math.Sign(x);
            x = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.3275911 * x);
            double y = 1.0 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return s * y;
        }
    }

    /// <summary>A bounded record of the player's decisions, so the imitation model can be rebuilt after a restart.</summary>
    public sealed class PlayerBehaviourLog
    {
        public readonly int capacity;
        public readonly List<double[]> observations = new List<double[]>();
        public readonly List<int> actions = new List<int>();

        public PlayerBehaviourLog(int capacity = 4000) { this.capacity = capacity; }

        public int Count => actions.Count;

        public void Add(double[] obs, int action)
        {
            observations.Add((double[])obs.Clone());
            actions.Add(action);
            if (actions.Count > capacity)
            {
                observations.RemoveAt(0);
                actions.RemoveAt(0);
            }
        }

        public int[] ActionHistogram()
        {
            var h = new int[KendoTimings.ActionCount];
            foreach (var a in actions) if (a >= 0 && a < h.Length) h[a]++;
            return h;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(0x4B424C31); // "KBL1"
            w.Write(actions.Count);
            for (int i = 0; i < actions.Count; i++)
            {
                w.Write((byte)actions[i]);
                var o = observations[i];
                w.Write((byte)o.Length);
                foreach (var v in o) w.Write((float)v);
            }
        }

        public static PlayerBehaviourLog Read(BinaryReader r, int capacity = 4000)
        {
            var log = new PlayerBehaviourLog(capacity);
            if (r.ReadInt32() != 0x4B424C31) throw new InvalidDataException("not a behaviour log");
            int n = r.ReadInt32();
            if (n < 0 || n > 1_000_000) throw new InvalidDataException("bad count");
            for (int i = 0; i < n; i++)
            {
                int a = r.ReadByte();
                int len = r.ReadByte();
                var o = new double[len];
                for (int k = 0; k < len; k++) o[k] = r.ReadSingle();
                log.Add(o, a);
            }
            return log;
        }
    }
}
