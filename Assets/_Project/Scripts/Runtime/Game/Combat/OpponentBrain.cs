using System;
using Kendo.AI;
using Kendo.Campaign;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Observation for Agents 2 and 3 computed from the real game state, in exactly the layout of
    /// <see cref="DuelSim.RawObservation"/> (court frame: z = fighting axis). The same function describes the player
    /// (for imitation learning) and the opponent (for its policy).
    /// </summary>
    public static class GameObservation
    {
        public static double[] Build(Fighter me, Fighter op, KendoMatch match, int meIndex, Vector3 courtCenter, Quaternion courtRot, float courtHalfAlong)
        {
            var o = new double[KendoTimings.ObservationSize];
            var inv = Quaternion.Inverse(courtRot);
            var mp = inv * (me.transform.position - courtCenter);
            var opp = inv * (op.transform.position - courtCenter);
            // facing along the court axis: +1 if I face +z of the court frame
            float facing = Vector3.Dot(inv * me.transform.forward, Vector3.forward) >= 0f ? 1f : -1f;
            var d = op.transform.position - me.transform.position;
            d.y = 0f;
            o[0] = d.magnitude / 3.0;
            o[1] = mp.z * facing / courtHalfAlong;
            o[2] = opp.z * facing / courtHalfAlong;
            o[3] = (opp.x - mp.x) * facing;
            o[4 + (int)me.Phase] = 1.0;
            o[10 + (int)op.Phase] = 1.0;
            o[16] = me.Motion.PhaseProgress;
            o[17] = op.Motion.PhaseProgress;
            if (op.Phase == DuelPhase.Striking)
            {
                switch (op.Motion.Strike)
                {
                    case DuelAction.Men: o[18] = 1; break;
                    case DuelAction.Kote: o[19] = 1; break;
                    case DuelAction.Do: o[20] = 1; break;
                    case DuelAction.Tsuki: o[21] = 1; break;
                }
            }
            o[22] = Time.time < me.CounterUntil ? 1.0 : 0.0;
            if (match != null)
            {
                o[23] = (match.points[meIndex] - match.points[1 - meIndex]) / 2.0;
                o[24] = match.TimeRemaining / Math.Max(1.0, match.rules.matchSeconds);
            }
            o[25] = op.Phase == DuelPhase.Blocking ? 1.0 : 0.0;
            return o;
        }
    }

    /// <summary>
    /// Drives an AI fighter with one of the three agents (rule-based, imitation, A2C) through the same commands the
    /// player uses. Implements the paper's PDDA loop for Level 4 and the final battle: the imitation model learns from
    /// the player every decision, a background A2C trainer fine-tunes against it, and the live opponent is replaced by
    /// the current RL agent at each restart after a point.
    /// </summary>
    public sealed class OpponentBrain : MonoBehaviour
    {
        public Fighter self, player;
        public KendoMatch match;
        public Vector3 courtCenter;
        public Quaternion courtRot = Quaternion.identity;
        public float courtHalfAlong = 4f;
        public OpponentKind kind;
        public string tier = "medium";
        public IDuelPolicy policy;
        public RuleBasedPolicy ruleBased;
        public PddaController pdda = new PddaController();
        public BackgroundTrainer trainer;
        public bool active;
        public string AgentName => policy != null ? policy.Name : "none";
        public int Swaps { get; private set; }

        private KendoGame game;
        private float nextDecision;
        private DuelAction lastPlayerCommand = DuelAction.Wait;
        private float lastPlayerCommandTime = -99f;
        private float blockRelease;
        private readonly KRandom rng = new KRandom(31337);

        public static OpponentStyle StyleFor(string tier)
        {
            switch (tier)
            {
                case "easy": return OpponentStyle.Easy();
                case "hard": return OpponentStyle.Hard();
                case "boss": return OpponentStyle.FormerStudent();
                default: return OpponentStyle.Medium();
            }
        }

        public static Technique TechniqueFor(string tier)
        {
            switch (tier)
            {
                case "easy": return Technique.Easy();
                case "hard": return Technique.Hard();
                case "boss": return Technique.Boss();
                default: return Technique.Medium();
            }
        }

        public void Init(Fighter me, Fighter human, KendoMatch m, OpponentKind k, string t)
        {
            game = KendoGame.Instance;
            self = me;
            player = human;
            match = m;
            kind = k;
            tier = t;
            self.technique = TechniqueFor(t);
            self.SeedTechnique(1234 + (int)k);
            ruleBased = new RuleBasedPolicy(StyleFor(t), new KRandom(77));
            switch (k)
            {
                case OpponentKind.RuleBased:
                    policy = ruleBased;
                    break;
                case OpponentKind.Imitation:
                    // Agent 2 fights: it plays like the player (needs observations; falls back to rule-based until it has them)
                    policy = game.Imitation.Samples >= 200 ? new ImitationPolicy(game.Imitation, new KRandom(78)) : (IDuelPolicy)ruleBased;
                    break;
                case OpponentKind.PddaRl:
                case OpponentKind.FormerStudent:
                    pdda.minObservations = 200;
                    policy = ruleBased; // the paper starts with a rule-based opponent
                    var start = game.PersonalPolicy ?? game.BasePolicy;
                    if (start != null)
                        trainer = new BackgroundTrainer(start, game.Imitation, self.technique, Technique.Medium(), seed: 99 + (int)k);
                    break;
            }
            human.GetComponent<PlayerRig>().CommandIssued += a => { lastPlayerCommand = a; lastPlayerCommandTime = Time.time; };
        }

        /// <summary>Called by the match at every restart after a point (the paper's replacement interval).</summary>
        public void OnRestart(bool playerScored)
        {
            pdda.OnPointScored(playerScored, game.Imitation.Samples);
            if (kind == OpponentKind.Imitation && !(policy is ImitationPolicy) && game.Imitation.Samples >= 200)
                policy = new ImitationPolicy(game.Imitation, new KRandom(79));
            if (trainer != null && pdda.UsingRlAgent && trainer.Steps > 2000)
            {
                policy = new A2CPolicy(trainer.Snapshot(), new KRandom(80 + Swaps));
                Swaps++;
            }
            else if (kind == OpponentKind.FormerStudent || kind == OpponentKind.PddaRl)
            {
                ruleBased.style = pdda.Scale(StyleFor(tier));
            }
        }

        public void OnBoutEnd()
        {
            if (trainer != null && trainer.Steps > 2000) game.PersonalPolicy = trainer.Snapshot();
            game.SavePlayerModel();
        }

        private void Update()
        {
            if (!active || self == null || player == null || match == null || !match.Running) return;
            // background training in small slices (≈2 ms per frame) – the paper's separate training environment
            if (trainer != null) trainer.Tick(2.0);
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + (float)KendoTimings.DecisionDt;

            // Agent 2 observes the player's decision for this tick
            var playerObs = GameObservation.Build(player, self, match, 0, courtCenter, courtRot, courtHalfAlong);
            if (player.CanAct || Time.time - lastPlayerCommandTime < 0.1f)
            {
                var a = Time.time - lastPlayerCommandTime < 0.1f ? lastPlayerCommand : DuelAction.Wait;
                game.ObservePlayer(playerObs, a);
            }

            if (self.Motion.Mode == ShinaiMode.Block && Time.time > blockRelease) self.BlockCommand(false, true);
            if (!self.CanAct) return;
            var obs = GameObservation.Build(self, player, match, 1, courtCenter, courtRot, courtHalfAlong);
            Execute(policy.Act(obs));
        }

        private void Execute(DuelAction a)
        {
            switch (a)
            {
                case DuelAction.StepForward: self.StepCommand(StepDir.Forward); break;
                case DuelAction.StepBack: self.StepCommand(StepDir.Back); break;
                case DuelAction.StepLeft: self.StepCommand(StepDir.Left); break;
                case DuelAction.StepRight: self.StepCommand(StepDir.Right); break;
                case DuelAction.Men:
                case DuelAction.Kote:
                case DuelAction.Do:
                case DuelAction.Tsuki:
                    if (a == DuelAction.Tsuki && match != null && !match.rules.allowTsuki) a = DuelAction.Men;
                    if (self.Motion.Mode == ShinaiMode.Block) self.BlockCommand(false, true);
                    self.StrikeCommand(a);
                    break;
                case DuelAction.BlockHigh:
                case DuelAction.BlockDo:
                    self.BlockCommand(true, a == DuelAction.BlockHigh);
                    blockRelease = Time.time + (float)KendoTimings.BlockHold;
                    break;
            }
        }
    }
}
