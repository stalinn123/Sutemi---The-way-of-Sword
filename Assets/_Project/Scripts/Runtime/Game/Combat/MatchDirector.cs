using System;
using System.Collections;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Campaign;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Runs one shiai: hajime → strikes (sensor → engine → three-shinpan panel) → ippon / ai-uchi / yame → restart on the
    /// start lines, jogai and other hansoku, time, encho, hantei. Collects the player's evaluated strikes and the bout
    /// statistics the master's feedback uses. Match scoring is separate from training performance scores.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        public Fighter player, opponent;
        public OpponentBrain brain;
        public KendoMatch match;
        public LevelDef stage;
        public SceneLayout layout;
        public readonly ShinpanPanel panel = new ShinpanPanel();
        public readonly List<StrikeEvaluation> playerStrikes = new List<StrikeEvaluation>();
        public readonly List<StrikeEvaluation> opponentStrikes = new List<StrikeEvaluation>();
        public readonly BoutStats stats = new BoutStats();
        public event Action<bool> Finished;      // true = player won
        public bool Running => match != null && match.Phase != MatchPhase.Finished;
        public string pointsText0 = "", pointsText1 = "";

        private KendoGame game;
        private KendoEvaluationProfile profile;
        private StrikeReport pendingAward;
        private float pendingAt;
        private bool restarting;
        private readonly List<Target> conceded = new List<Target>();

        public void Begin(Fighter p, Fighter o, OpponentBrain b, LevelDef s, SceneLayout l)
        {
            game = KendoGame.Instance;
            player = p;
            opponent = o;
            brain = b;
            stage = s;
            layout = l;
            match = new KendoMatch(s.Rules());
            match.hanteiScore = side => Average(side == 0 ? playerStrikes : opponentStrikes);
            profile = s.Profile();
            foreach (var f in new[] { player, opponent })
            {
                f.sensor.rules = match.rules;
                f.sensor.profile = profile;
                f.sensor.targets.Clear();
                f.arenaCenter = layout.courtCenter;
                f.arenaHalf = new Vector2(layout.courtHalf.x, layout.courtHalf.y);
                f.arenaRotation = layout.CourtRotation;
            }
            player.sensor.targets.Add(opponent);
            opponent.sensor.targets.Add(player);
            player.opponent = opponent;
            opponent.opponent = player;
            player.sensor.opposingShinai = () => opponent.rig;
            player.sensor.opposingBlockRaised = () => opponent.Motion.BlockRaised;
            opponent.sensor.opposingShinai = () => player.rig;
            opponent.sensor.opposingBlockRaised = () => player.Motion.BlockRaised;
            player.sensor.Evaluated += r => OnStrike(r, 0);
            opponent.sensor.Evaluated += r => OnStrike(r, 1);
            player.sensor.Contact += OnContact;
            opponent.sensor.Contact += OnContact;
            player.sensor.Missed += r => { if (Running) { playerStrikes.Add(r.evaluation); game.Campaign.data.stats.strikes++; } };
            opponent.sensor.Missed += r => { if (Running) { opponentStrikes.Add(r.evaluation); stats.opponentStrikes++; } };
            game.Ui.CombatHud.Show(true);
            StartCoroutine(StartSequence(false));
        }

        private static float Average(List<StrikeEvaluation> l)
        {
            if (l.Count == 0) return 0f;
            float s = 0f;
            foreach (var e in l) s += e.performanceScore;
            return s / l.Count;
        }

        public float PlayerPerformance => Average(playerStrikes);

        private void Update()
        {
            if (match == null) return;
            if (match.Running)
            {
                match.Tick(Time.deltaTime);
                CheckJogai(player, 0);
                CheckJogai(opponent, 1);
                if (pendingAward != null && Time.time - pendingAt > 0.1f) Award();
            }
            game.Ui.CombatHud.Set("You (aka)", opponent.displayName + " (shiro)", match, pointsText0, pointsText1);
            game.Ui.CombatHud.Status(brain != null ? $"Opponent agent: {brain.AgentName}" + (brain.trainer != null ? $" · background A2C: {brain.trainer.Steps / 1000}k steps, win {brain.trainer.WinRate:P0} vs your imitation" : "") : "");
            if (match.Phase == MatchPhase.Yame && !restarting && match.Phase != MatchPhase.Finished) StartCoroutine(StartSequence(true));
            if (match.Phase == MatchPhase.Finished && !finishedRaised) { finishedRaised = true; StartCoroutine(EndSequence()); }
        }

        private bool finishedRaised;

        private void CheckJogai(Fighter f, int side)
        {
            if (!match.Running || !f.OutsideArena(0.05f)) return;
            match.Hansoku(side, "jogai – stepped out of the court");
            if (side == 0) stats.jogai++;
            game.Ui.CombatHud.Banner("Hansoku – jogai");
            if (match.Running) match.Yame("jogai");
        }

        private void OnContact(StrikeReport r)
        {
            var cfgA = game.Config;
            var at = r.contactPoint;
            if (r.blockedByShinai)
            {
                game.Audio.Sfx(cfgA.shinaiClash, at);
                var defender = r.attacker == player ? opponent : player;
                r.attacker.Motion.Deflected();
                defender.CounterUntil = Time.time + (float)KendoTimings.CounterWindow;
                if (defender == player) stats.blocks++;
                return;
            }
            game.Audio.Sfx(r.evaluation.invalidReason == InvalidReason.ContactOnly ? cfgA.shinaiTouch : cfgA.shinaiHit, at, 1f, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        private void OnStrike(StrikeReport r, int side)
        {
            if (!Running || restarting) return;
            if (r.blockedByShinai) { (side == 0 ? playerStrikes : opponentStrikes).Add(r.evaluation); return; }
            var ev = r.evaluation;
            if (side == 0)
            {
                playerStrikes.Add(ev);
                var st = game.Campaign.data.stats;
                st.Record(ev);
                st.strikes++;
                if (ev.yukoDatotsu) st.validStrikes++;
                if (ev.invalidReason == InvalidReason.ContactOnly) st.contactsOnly++;
                if (r.evidence.opportunity == Opportunity.AfterBlock) { stats.counters++; st.counters++; }
                if (r.evidence.opportunity == Opportunity.IntoReadyKamae) stats.intoReadyKamae++;
            }
            else { opponentStrikes.Add(ev); stats.opponentStrikes++; }
            if (!match.Running) return;
            var d = panel.Decide(ev, match.rules);
            string who = side == 0 ? "Your" : opponent.displayName + "'s";
            game.Ui.CombatHud.Verdict(d.awarded ? $"{who} {KendoEvaluationEngine.TargetName(ev.target)} – ippon!" : $"{who} {KendoEvaluationEngine.TargetName(ev.target)}: {ReasonText(ev)}", d.flags, side == 0);
            if (!d.awarded) return;
            // ai-uchi: an awarded strike by the other side within 0.1 s cancels both
            if (pendingAward != null && pendingAward.attacker != r.attacker && Mathf.Abs(pendingAward.contactTime - r.contactTime) <= 0.1f)
            {
                pendingAward = null;
                match.AiUchi();
                game.Ui.CombatHud.Banner("Ai-uchi – no point");
                match.Yame("ai-uchi");
                return;
            }
            pendingAward = r;
            pendingAt = Time.time;
        }

        private void Award()
        {
            var r = pendingAward;
            pendingAward = null;
            int side = r.attacker == player ? 0 : 1;
            match.AwardIppon(side, r.evaluation.target);
            game.Audio.Stinger(game.Config.flagRaise);
            string mark = r.evaluation.target == Target.Tsuki ? "T" : KendoEvaluationEngine.IsKote(r.evaluation.target) ? "K" : KendoEvaluationEngine.IsDo(r.evaluation.target) ? "D" : "M";
            if (side == 0) { pointsText0 += mark + " "; game.Campaign.data.stats.ippon++; }
            else
            {
                pointsText1 = mark + " " + pointsText1;
                stats.conceded++;
                game.Campaign.data.stats.conceded++;
                if (player.Phase == DuelPhase.Recovering || player.Phase == DuelPhase.Stunned || player.Phase == DuelPhase.Striking) stats.concededWhileRecovering++;
                if (conceded.Contains(r.evaluation.target)) stats.repeatedPatternConceded++;
                conceded.Add(r.evaluation.target);
            }
            game.Ui.CombatHud.Banner(side == 0 ? $"Ippon – {KendoEvaluationEngine.TargetName(r.evaluation.target)}!" : $"{opponent.displayName} scores {KendoEvaluationEngine.TargetName(r.evaluation.target)}");
            brain?.OnRestart(side == 0);
            brain?.pdda.OnPerformance(PlayerPerformance / 100.0);
        }

        public static string ReasonText(StrikeEvaluation ev)
        {
            switch (ev.invalidReason)
            {
                case InvalidReason.None: return "valid, but the judges were not convinced";
                case InvalidReason.ContactOnly: return "only contact";
                case InvalidReason.TargetNotAllowed: return "target not allowed";
                case InvalidReason.NotMonouchi: return "not with the monouchi";
                case InvalidReason.WrongSide: return "struck with the wrong side";
                case InvalidReason.BadHasuji: return "bad hasuji";
                case InvalidReason.NoSpirit: return "no kiai";
                case InvalidReason.BadPosture: return "poor posture";
                case InvalidReason.NoZanshin: return "no zanshin";
                case InvalidReason.Blocked: return "blocked";
                case InvalidReason.Missed: return "missed";
                default: return ev.invalidReason.ToString();
            }
        }

        private IEnumerator StartSequence(bool restart)
        {
            restarting = true;
            player.Frozen = opponent.Frozen = true;
            if (brain != null) brain.active = false;
            player.sensor.Cancel();
            opponent.sensor.Cancel();
            if (restart) yield return new WaitForSeconds(1.6f);
            // back to the start lines
            yield return game.Ui.Fade(0.6f, 0.25f);
            Place(player, layout.PlayerStart, layout.PlayerStartYaw);
            Place(opponent, layout.OpponentStart, layout.OpponentStartYaw);
            yield return game.Ui.Fade(0f, 0.25f);
            string call = !restart ? "Hajime!" : (match.InEncho ? "Encho – hajime!" : (match.points[0] + match.points[1] == 1 ? "Nihonme!" : "Shobu!"));
            game.Ui.CombatHud.Banner(call, 1.2f);
            game.Audio.Stinger(game.Config.taiko);
            yield return new WaitForSeconds(0.9f);
            player.Frozen = opponent.Frozen = false;
            if (brain != null) brain.active = true;
            match.Hajime();
            restarting = false;
        }

        private static void Place(Fighter f, Vector3 pos, float yaw)
        {
            var cc = f.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            f.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var rig = f.GetComponent<PlayerRig>();
            if (rig != null) rig.yaw = yaw;
            f.footwork.Cancel();
            if (cc != null) cc.enabled = true;
        }

        private IEnumerator EndSequence()
        {
            player.Frozen = opponent.Frozen = true;
            if (brain != null) { brain.active = false; brain.OnBoutEnd(); }
            game.Ui.CombatHud.Banner(match.Winner == 0 ? "Shobu-ari – you win" : match.Winner == 1 ? "Shobu-ari" : "Hikiwake", 2.5f);
            yield return new WaitForSeconds(2.5f);
            game.Ui.CombatHud.Show(false);
            Finished?.Invoke(match.Winner == 0);
        }
    }
}
