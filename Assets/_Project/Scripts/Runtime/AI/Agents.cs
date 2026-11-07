using System;
using System.Diagnostics;

namespace Kendo.AI
{
    /// <summary>Agent 2 – plays like the player by sampling from the imitation model's predicted action distribution.</summary>
    public sealed class ImitationPolicy : IDuelPolicy
    {
        public readonly AdaptiveRandomForest model;
        private readonly KRandom rng;
        public ImitationPolicy(AdaptiveRandomForest model, KRandom rng) { this.model = model; this.rng = rng; }
        public string Name => "Imitation (ARF)";
        public DuelAction Act(double[] o) => (DuelAction)ActorCritic.Sample(model.Proba(o), rng.Value());
    }

    /// <summary>Agent 3 – samples from the A2C policy (greedy when <see cref="deterministic"/>).</summary>
    public sealed class A2CPolicy : IDuelPolicy
    {
        public ActorCritic net;
        private readonly KRandom rng;
        public bool deterministic;
        public A2CPolicy(ActorCritic net, KRandom rng) { this.net = net; this.rng = rng; }
        public string Name => "A2C";

        public DuelAction Act(double[] o)
        {
            var p = net.Policy(o);
            if (!deterministic) return (DuelAction)ActorCritic.Sample(p, rng.Value());
            int best = 0;
            for (int i = 1; i < p.Length; i++) if (p[i] > p[best]) best = i;
            return (DuelAction)best;
        }
    }

    /// <summary>
    /// The paper's training environment: Agent 3 (seat 1) fights Agent 2 (seat 0, imitating the player) in
    /// <see cref="DuelSim"/>, using n-step A2C updates. Runs in small time slices so the game can call it every frame
    /// without threads (the imitation model is updated on the main thread too).
    /// </summary>
    public sealed class BackgroundTrainer
    {
        public readonly ActorCritic learner;
        public readonly A2CTrainer trainer;
        private readonly AdaptiveRandomForest imitation;
        private readonly KRandom rng;
        private readonly Technique ownTech, playerTech;
        private readonly int nEnvs, nSteps;
        private readonly DuelSim[] envs;
        private readonly ImitationPolicy[] adversaries;
        private readonly int[] episodeLen;
        public long Steps { get; private set; }
        public int Episodes { get; private set; }
        public int Wins { get; private set; }
        public double LastEntropy { get; private set; }
        private long episodeSeed;

        public BackgroundTrainer(ActorCritic start, AdaptiveRandomForest imitation, Technique ownTech, Technique playerTech, long seed, int nEnvs = 4, int nSteps = 5)
        {
            learner = start.CloneDeep();
            trainer = new A2CTrainer(learner);
            this.imitation = imitation;
            this.ownTech = ownTech;
            this.playerTech = playerTech;
            rng = new KRandom(seed);
            this.nEnvs = nEnvs;
            this.nSteps = nSteps;
            envs = new DuelSim[nEnvs];
            adversaries = new ImitationPolicy[nEnvs];
            episodeLen = new int[nEnvs];
            episodeSeed = seed * 7919;
            for (int i = 0; i < nEnvs; i++) NewEpisode(i);
        }

        private void NewEpisode(int i)
        {
            episodeSeed++;
            var p = new DuelParams { startDistance = rng.Range(2.4, 3.2), trackingNoise = rng.Range(0.0, 0.04), latencySteps = new[] { rng.Int(2), 0 } };
            envs[i] = new DuelSim(episodeSeed, p, playerTech, ownTech);
            adversaries[i] = new ImitationPolicy(imitation, new KRandom(episodeSeed + 1));
            episodeLen[i] = 0;
        }

        /// <summary>Win rate over all finished training episodes (evidence shown in the debug overlay; never faked).</summary>
        public double WinRate => Episodes > 0 ? (double)Wins / Episodes : 0.0;

        /// <summary>Runs updates until the time budget is used. Returns the number of updates performed.</summary>
        public int Tick(double budgetMs)
        {
            var sw = Stopwatch.StartNew();
            int updates = 0;
            while (sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                RunUpdate();
                updates++;
            }
            return updates;
        }

        public void RunUpdate()
        {
            int batch = nEnvs * nSteps;
            var obs = new double[batch][];
            var acts = new int[batch];
            var rew = new double[nSteps, nEnvs];
            var done = new bool[nSteps, nEnvs];
            for (int t = 0; t < nSteps; t++)
            {
                for (int i = 0; i < nEnvs; i++)
                {
                    var env = envs[i];
                    var o = env.Observe(1);
                    int a = ActorCritic.Sample(learner.Policy(o), rng.Value());
                    obs[t * nEnvs + i] = o;
                    acts[t * nEnvs + i] = a;
                    var r = env.Step(adversaries[i].Act(env.Observe(0)), (DuelAction)a);
                    double sum = r[1];
                    episodeLen[i]++;
                    Steps++;
                    // macro decision (same as Tools/AI/train_a2c.py and the game): only ask the policy when it can act
                    while (!env.Done && !env.CanAct(1) && episodeLen[i] < 1500)
                    {
                        r = env.Step(adversaries[i].Act(env.Observe(0)), DuelAction.Wait);
                        sum += r[1];
                        episodeLen[i]++;
                        Steps++;
                    }
                    rew[t, i] = sum;
                    if (env.Done || episodeLen[i] >= 1500)
                    {
                        done[t, i] = true;
                        Episodes++;
                        if (env.match.Winner == 1) Wins++;
                        NewEpisode(i);
                    }
                }
            }
            var returns = new double[batch];
            for (int i = 0; i < nEnvs; i++)
            {
                double next = learner.Value(envs[i].RawObservation(1));
                for (int t = nSteps - 1; t >= 0; t--)
                {
                    next = rew[t, i] + trainer.gamma * next * (done[t, i] ? 0.0 : 1.0);
                    returns[t * nEnvs + i] = next;
                }
            }
            LastEntropy = trainer.Update(obs, acts, returns);
        }

        /// <summary>A frozen copy for the live opponent (the paper swaps the opponent for the current RL agent).</summary>
        public ActorCritic Snapshot() => learner.CloneDeep();
    }

    /// <summary>
    /// Personalised dynamic difficulty adjustment. Paper-derived: start with the rule-based opponent, collect observations
    /// of the player, then replace the opponent with the current RL agent at fixed intervals (here: at every restart
    /// after a point, once enough observations exist). Additional design decision: a bounded difficulty scale from the
    /// score difference and recent performance adjusts reaction time and aggression of the rule-based phases.
    /// </summary>
    public sealed class PddaController
    {
        public int minObservations = 300;
        public double difficulty = 0.5; // 0 easiest .. 1 hardest
        public double minDifficulty = 0.25, maxDifficulty = 1.0;
        public bool UsingRlAgent { get; private set; }

        public void OnPointScored(bool playerScored, int observations)
        {
            difficulty += playerScored ? 0.15 : -0.10;
            if (difficulty < minDifficulty) difficulty = minDifficulty;
            if (difficulty > maxDifficulty) difficulty = maxDifficulty;
            if (observations >= minObservations) UsingRlAgent = true;
        }

        public void OnPerformance(double playerPerformance01)
        {
            difficulty += (playerPerformance01 - 0.5) * 0.1;
            if (difficulty < minDifficulty) difficulty = minDifficulty;
            if (difficulty > maxDifficulty) difficulty = maxDifficulty;
        }

        public OpponentStyle Scale(OpponentStyle baseStyle)
        {
            var s = baseStyle.Clone();
            double k = 0.6 + 0.8 * difficulty; // 0.6x .. 1.4x
            s.aggression = Math.Min(0.95, s.aggression * k);
            s.reaction = Math.Max(0.08, s.reaction / k);
            s.defense = Math.Min(0.9, s.defense * k);
            s.counter = Math.Min(0.9, s.counter * k);
            return s;
        }
    }

    /// <summary>Converts the player's committed commands into the shared discrete action space (labels for Agent 2).</summary>
    public static class ActionLabels
    {
        public static string Describe(DuelAction a)
        {
            switch (a)
            {
                case DuelAction.Men: return "men";
                case DuelAction.Kote: return "kote";
                case DuelAction.Do: return "do";
                case DuelAction.Tsuki: return "tsuki";
                case DuelAction.BlockHigh: return "high block";
                case DuelAction.BlockDo: return "do block";
                case DuelAction.StepForward: return "step in";
                case DuelAction.StepBack: return "step back";
                case DuelAction.StepLeft:
                case DuelAction.StepRight: return "side step";
                default: return "wait";
            }
        }
    }
}
