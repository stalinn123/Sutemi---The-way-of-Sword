using System;
using System.Collections.Generic;

namespace Kendo.Rules
{
    public enum MatchPhase { NotStarted, Running, Yame, Finished }
    public enum MatchEndReason { None, Points, TimeLeader, Hantei, Hikiwake, Forfeit }
    public enum MatchEventKind { Hajime, Ippon, Hansoku, HansokuIppon, AiUchi, Torikeshi, Yame, Encho, TimeUp, Hantei, Shobuari, Hikiwake }

    public struct MatchEvent
    {
        public float time;
        public MatchEventKind kind;
        public int side;          // 0 = red (aka) / player, 1 = white (shiro) / opponent, -1 = none
        public Target target;
        public string detail;
    }

    /// <summary>Match state for one bout: clock, points, encho, hansoku, hantei (FIK Regulations Art. 6–9, 17–20).</summary>
    public sealed class KendoMatch
    {
        public readonly KendoRuleConfig rules;
        public readonly int[] points = new int[2];
        public readonly int[] hansoku = new int[2];
        public readonly List<MatchEvent> events = new List<MatchEvent>();
        public MatchPhase Phase { get; private set; } = MatchPhase.NotStarted;
        public bool InEncho { get; private set; }
        public float TimeRemaining { get; private set; }
        public float Elapsed { get; private set; }
        public int Winner { get; private set; } = -1;
        public MatchEndReason EndReason { get; private set; }
        /// <summary>Supplies the panel's hantei view of each fighter (e.g. average training performance). Design hook.</summary>
        public Func<int, float> hanteiScore;

        public KendoMatch(KendoRuleConfig rules)
        {
            this.rules = rules ?? KendoRuleConfig.FikStandard();
            TimeRemaining = this.rules.matchSeconds;
        }

        public bool Running => Phase == MatchPhase.Running;

        public void Hajime()
        {
            if (Phase == MatchPhase.Finished) return;
            Phase = MatchPhase.Running;
            Log(MatchEventKind.Hajime, -1, Target.None, InEncho ? "encho hajime" : "hajime");
        }

        public void Yame(string why)
        {
            if (Phase != MatchPhase.Running) return;
            Phase = MatchPhase.Yame;
            Log(MatchEventKind.Yame, -1, Target.None, why);
        }

        public void Tick(float dt)
        {
            if (Phase != MatchPhase.Running) return;
            Elapsed += dt;
            if (InEncho && rules.enchoSeconds <= 0f) return; // unlimited encho
            TimeRemaining -= dt;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                TimeUp();
            }
        }

        /// <summary>A panel-awarded ippon.</summary>
        public void AwardIppon(int side, Target target, string detail = null)
        {
            if (Phase != MatchPhase.Running) return;
            points[side]++;
            Log(MatchEventKind.Ippon, side, target, detail ?? KendoEvaluationEngine.TargetName(target));
            AfterPoint();
        }

        /// <summary>Subsidiary Art. 24: the point is revoked (e.g. guts pose).</summary>
        public void Torikeshi(int side)
        {
            if (points[side] <= 0) return;
            points[side]--;
            Log(MatchEventKind.Torikeshi, side, Target.None, "point revoked");
        }

        public void AiUchi() => Log(MatchEventKind.AiUchi, -1, Target.None, "simultaneous strikes - no point");

        /// <summary>Art. 17: a prohibited act. Every <see cref="KendoRuleConfig.hansokuPerIppon"/> hansoku give the opponent one ippon.</summary>
        public void Hansoku(int side, string reason)
        {
            if (Phase == MatchPhase.Finished || Phase == MatchPhase.NotStarted) return;
            hansoku[side]++;
            Log(MatchEventKind.Hansoku, side, Target.None, reason);
            if (rules.hansokuPerIppon > 0 && hansoku[side] % rules.hansokuPerIppon == 0)
            {
                int other = 1 - side;
                points[other]++;
                Log(MatchEventKind.HansokuIppon, other, Target.None, "two hansoku");
                Phase = MatchPhase.Running;
                AfterPoint();
            }
        }

        public void Forfeit(int loser)
        {
            Finish(1 - loser, MatchEndReason.Forfeit);
        }

        private void AfterPoint()
        {
            int leader = points[0] > points[1] ? 0 : (points[1] > points[0] ? 1 : -1);
            if (points[0] >= rules.pointsToWin || points[1] >= rules.pointsToWin || (InEncho && leader >= 0))
            {
                Finish(leader, MatchEndReason.Points);
                return;
            }
            Phase = MatchPhase.Yame; // nihonme / shobu: restart from the start lines
        }

        private void TimeUp()
        {
            Log(MatchEventKind.TimeUp, -1, Target.None, InEncho ? "encho time" : "time");
            int leader = points[0] > points[1] ? 0 : (points[1] > points[0] ? 1 : -1);
            if (!InEncho && leader >= 0 && rules.leaderWinsAtTime) { Finish(leader, MatchEndReason.TimeLeader); return; }
            if (!InEncho && rules.encho)
            {
                InEncho = true;
                TimeRemaining = rules.enchoSeconds;
                Phase = MatchPhase.Yame;
                Log(MatchEventKind.Encho, -1, Target.None, "encho - first point wins");
                return;
            }
            if (rules.hantei && hanteiScore != null)
            {
                float a = hanteiScore(0), b = hanteiScore(1);
                if (Math.Abs(a - b) > 1e-3f)
                {
                    int w = a > b ? 0 : 1;
                    Log(MatchEventKind.Hantei, w, Target.None, "judges' decision");
                    Finish(w, MatchEndReason.Hantei);
                    return;
                }
            }
            Finish(-1, MatchEndReason.Hikiwake);
        }

        private void Finish(int winner, MatchEndReason reason)
        {
            Winner = winner;
            EndReason = reason;
            Phase = MatchPhase.Finished;
            Log(winner >= 0 ? MatchEventKind.Shobuari : MatchEventKind.Hikiwake, winner, Target.None, reason.ToString());
        }

        private void Log(MatchEventKind kind, int side, Target target, string detail)
            => events.Add(new MatchEvent { time = Elapsed, kind = kind, side = side, target = target, detail = detail });
    }
}
