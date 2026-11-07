using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kendo.AI
{
    /// <summary>Fully connected tanh network (last layer linear), row-major weights W[out][in].</summary>
    public sealed class Mlp
    {
        public double[][][] W; // layer, out, in
        public double[][] B;

        public int Layers => W.Length;

        public double[] Forward(double[] x, List<double[]> acts = null)
        {
            acts?.Add(x);
            double[] h = x;
            for (int l = 0; l < W.Length; l++)
            {
                var w = W[l];
                var z = new double[w.Length];
                for (int o = 0; o < w.Length; o++)
                {
                    double s = B[l][o];
                    var row = w[o];
                    for (int i = 0; i < row.Length; i++) s += row[i] * h[i];
                    z[o] = l < W.Length - 1 ? Math.Tanh(s) : s;
                }
                h = z;
                acts?.Add(h);
            }
            return h;
        }

        public Mlp CloneDeep()
        {
            var c = new Mlp { W = new double[W.Length][][], B = new double[B.Length][] };
            for (int l = 0; l < W.Length; l++)
            {
                c.W[l] = new double[W[l].Length][];
                for (int o = 0; o < W[l].Length; o++) c.W[l][o] = (double[])W[l][o].Clone();
                c.B[l] = (double[])B[l].Clone();
            }
            return c;
        }
    }

    /// <summary>
    /// Agent 3's network: separate actor and critic 64-64 tanh MLPs (Stable-Baselines3 A2C MlpPolicy layout).
    /// Loads the JSON exported by Tools/AI/a2c.py (ActorCritic.to_json).
    /// </summary>
    public sealed class ActorCritic
    {
        public Mlp pi, vf;
        public int obs, actions, hidden;

        public static double[] Softmax(double[] z)
        {
            double m = double.NegativeInfinity;
            foreach (var v in z) if (v > m) m = v;
            var e = new double[z.Length];
            double s = 0;
            for (int i = 0; i < z.Length; i++) { e[i] = Math.Exp(z[i] - m); s += e[i]; }
            for (int i = 0; i < z.Length; i++) e[i] /= s;
            return e;
        }

        public double[] Policy(double[] x) => Softmax(pi.Forward(x));
        public double Value(double[] x) => vf.Forward(x)[0];

        /// <summary>Inverse-CDF sampling with an external uniform (same as Tools/AI/a2c.py sample).</summary>
        public static int Sample(double[] p, double u)
        {
            double c = 0;
            for (int i = 0; i < p.Length; i++)
            {
                c += p[i];
                if (u < c) return i;
            }
            return p.Length - 1;
        }

        public ActorCritic CloneDeep() => new ActorCritic { pi = pi.CloneDeep(), vf = vf.CloneDeep(), obs = obs, actions = actions, hidden = hidden };

        public static ActorCritic FromJson(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var ac = new ActorCritic
            {
                obs = (int)(double)root["obs"],
                actions = (int)(double)root["actions"],
                hidden = (int)(double)root["hidden"],
                pi = ParseNet((List<object>)root["pi"]),
                vf = ParseNet((List<object>)root["vf"]),
            };
            if (ac.pi.W[0][0].Length != ac.obs) throw new FormatException("policy input size does not match 'obs'");
            return ac;
        }

        private static Mlp ParseNet(List<object> layers)
        {
            var m = new Mlp { W = new double[layers.Count][][], B = new double[layers.Count][] };
            for (int l = 0; l < layers.Count; l++)
            {
                var pair = (List<object>)layers[l];
                var w = (List<object>)pair[0];
                m.W[l] = new double[w.Count][];
                for (int o = 0; o < w.Count; o++) m.W[l][o] = ToArray((List<object>)w[o]);
                m.B[l] = ToArray((List<object>)pair[1]);
            }
            return m;
        }

        private static double[] ToArray(List<object> list)
        {
            var a = new double[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = (double)list[i];
            return a;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"obs\": ").Append(obs).Append(", \"actions\": ").Append(actions).Append(", \"hidden\": ").Append(hidden);
            sb.Append(", \"pi\": "); WriteNet(sb, pi);
            sb.Append(", \"vf\": "); WriteNet(sb, vf);
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteNet(StringBuilder sb, Mlp m)
        {
            sb.Append('[');
            for (int l = 0; l < m.W.Length; l++)
            {
                if (l > 0) sb.Append(", ");
                sb.Append("[[");
                for (int o = 0; o < m.W[l].Length; o++)
                {
                    if (o > 0) sb.Append(", ");
                    WriteArray(sb, m.W[l][o]);
                }
                sb.Append("], ");
                WriteArray(sb, m.B[l]);
                sb.Append(']');
            }
            sb.Append(']');
        }

        private static void WriteArray(StringBuilder sb, double[] a)
        {
            sb.Append('[');
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(a[i].ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append(']');
        }
    }

    /// <summary>
    /// Synchronous A2C update (port of Tools/AI/a2c.py A2C.update): policy-gradient + entropy + value loss, global
    /// gradient-norm clipping and RMSprop. Used by <see cref="BackgroundTrainer"/> to fine-tune Agent 3 against Agent 2
    /// while the player fights (the paper's "separate training environment running in the background").
    /// </summary>
    public sealed class A2CTrainer
    {
        public readonly ActorCritic ac;
        public double lr = 7e-4, gamma = 0.99, entCoef = 0.01, vfCoef = 0.5, maxGradNorm = 0.5, alpha = 0.99, eps = 1e-5;
        private readonly double[][][][] sqW = new double[2][][][];
        private readonly double[][][] sqB = new double[2][][];

        public A2CTrainer(ActorCritic ac)
        {
            this.ac = ac;
            var nets = new[] { ac.pi, ac.vf };
            for (int n = 0; n < 2; n++)
            {
                sqW[n] = new double[nets[n].W.Length][][];
                sqB[n] = new double[nets[n].W.Length][];
                for (int l = 0; l < nets[n].W.Length; l++)
                {
                    sqW[n][l] = new double[nets[n].W[l].Length][];
                    for (int o = 0; o < nets[n].W[l].Length; o++) sqW[n][l][o] = new double[nets[n].W[l][o].Length];
                    sqB[n][l] = new double[nets[n].B[l].Length];
                }
            }
        }

        private static void Backward(Mlp m, List<double[]>[] acts, double[][] gOut, out double[][][] gW, out double[][] gB)
        {
            int L = m.W.Length;
            gW = new double[L][][];
            gB = new double[L][];
            for (int l = 0; l < L; l++)
            {
                gW[l] = new double[m.W[l].Length][];
                for (int o = 0; o < m.W[l].Length; o++) gW[l][o] = new double[m.W[l][o].Length];
                gB[l] = new double[m.B[l].Length];
            }
            for (int s = 0; s < acts.Length; s++)
            {
                double[] g = gOut[s];
                for (int l = L - 1; l >= 0; l--)
                {
                    var input = acts[s][l];
                    for (int o = 0; o < g.Length; o++)
                    {
                        gB[l][o] += g[o];
                        var row = gW[l][o];
                        for (int i = 0; i < input.Length; i++) row[i] += g[o] * input[i];
                    }
                    if (l > 0)
                    {
                        var ng = new double[input.Length];
                        for (int i = 0; i < input.Length; i++)
                        {
                            double sum = 0;
                            for (int o = 0; o < g.Length; o++) sum += g[o] * m.W[l][o][i];
                            ng[i] = sum * (1 - input[i] * input[i]);
                        }
                        g = ng;
                    }
                }
            }
        }

        /// <summary>One update on a batch of (observation, action, n-step return). Returns the policy entropy.</summary>
        public double Update(double[][] obs, int[] actions, double[] returns)
        {
            int n = actions.Length;
            var pActs = new List<double[]>[n];
            var vActs = new List<double[]>[n];
            var gLogits = new double[n][];
            var gV = new double[n][];
            double entSum = 0;
            for (int s = 0; s < n; s++)
            {
                pActs[s] = new List<double[]>();
                vActs[s] = new List<double[]>();
                var logits = ac.pi.Forward(obs[s], pActs[s]);
                double v = ac.vf.Forward(obs[s], vActs[s])[0];
                var p = ActorCritic.Softmax(logits);
                double adv = returns[s] - v;
                double ent = 0;
                var logp = new double[p.Length];
                for (int k = 0; k < p.Length; k++) { logp[k] = Math.Log(p[k] + 1e-12); ent -= p[k] * logp[k]; }
                entSum += ent;
                var g = new double[p.Length];
                for (int k = 0; k < p.Length; k++)
                {
                    double onehot = k == actions[s] ? 1.0 : 0.0;
                    g[k] = -adv * (onehot - p[k]) / n + entCoef * (p[k] * (logp[k] + ent)) / n;
                }
                gLogits[s] = g;
                gV[s] = new[] { vfCoef * 2.0 * (v - returns[s]) / n };
            }
            Backward(ac.pi, pActs, gLogits, out var gWp, out var gBp);
            Backward(ac.vf, vActs, gV, out var gWv, out var gBv);
            double norm2 = 0;
            foreach (var arr in new[] { gWp, gWv }) foreach (var layer in arr) foreach (var row in layer) foreach (var v in row) norm2 += v * v;
            foreach (var arr in new[] { gBp, gBv }) foreach (var layer in arr) foreach (var v in layer) norm2 += v * v;
            double norm = Math.Sqrt(norm2);
            double scale = Math.Min(1.0, maxGradNorm / (norm + 1e-6));
            Apply(ac.pi, 0, gWp, gBp, scale);
            Apply(ac.vf, 1, gWv, gBv, scale);
            return entSum / n;
        }

        private void Apply(Mlp m, int net, double[][][] gW, double[][] gB, double scale)
        {
            for (int l = 0; l < m.W.Length; l++)
            {
                for (int o = 0; o < m.W[l].Length; o++)
                {
                    for (int i = 0; i < m.W[l][o].Length; i++)
                    {
                        double g = gW[l][o][i] * scale;
                        sqW[net][l][o][i] = alpha * sqW[net][l][o][i] + (1 - alpha) * g * g;
                        m.W[l][o][i] -= lr * g / (Math.Sqrt(sqW[net][l][o][i]) + eps);
                    }
                }
                for (int o = 0; o < m.B[l].Length; o++)
                {
                    double g = gB[l][o] * scale;
                    sqB[net][l][o] = alpha * sqB[net][l][o] + (1 - alpha) * g * g;
                    m.B[l][o] -= lr * g / (Math.Sqrt(sqB[net][l][o]) + eps);
                }
            }
        }
    }

    /// <summary>Tiny JSON reader (objects, arrays, numbers, strings, booleans, null) – Kendo.AI has no engine references.</summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            Skip(s, ref i);
            if (i != s.Length) throw new FormatException("trailing characters in JSON");
            return v;
        }

        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    var key = (string)Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] != ':') throw new FormatException("expected ':'");
                    i++;
                    d[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("expected ',' or ']'");
                }
            }
            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"unexpected character '{c}' in JSON");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
