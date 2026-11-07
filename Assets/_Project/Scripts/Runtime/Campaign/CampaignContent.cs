using System.Collections.Generic;
using Kendo.Rules;

namespace Kendo.Campaign
{
    public enum ExerciseKind { IdentifyTarget, ContactVersusStrike, ShinaiHandling, Rei, Kamae, Suriashi, Strike, KiKenTaiItchi, Zanshin }

    /// <summary>What the master shows before the player practises. The game maps each to a procedural demonstration.</summary>
    public enum DemoKind { None, PointTargets, ContactVersusStrike, Sageto, Rei, Kamae, Suriashi, Strikes, KiKenTaiItchi, Zanshin }

    public enum SuriashiMove { Forward, Back, Left, Right }

    public enum OpponentKind { RuleBased, Imitation, PddaRl, FormerStudent }

    public enum ReiPlace { EntranceToKamiza, ToMaster, LeavingToKamiza }

    public sealed class Term
    {
        public string word, meaning;
        public Term(string w, string m) { word = w; meaning = m; }
    }

    public sealed class ExerciseDef
    {
        public string id, title, instruction, hint;
        public ExerciseKind kind;
        public int requiredSuccesses = 1;
        public Target[] targets = new Target[0];     // strike / identify exercises: requested target per success
        public ReiPlace[] reiSequence = new ReiPlace[0];
        public SuriashiMove[] moves = new SuriashiMove[0];
        public Criterion[] required = new Criterion[0]; // criteria that must pass for a successful attempt
        public string profile = "Beginner";
        public bool allowTsuki = true;
    }

    public sealed class LessonDef
    {
        public int index;
        public string id, title, japanese, summary;
        public string[] intro, explain, demoNarration, completion;
        public DemoKind demo;
        public Term[] terms;
        public ExerciseDef[] exercises;
    }

    public sealed class LevelDef
    {
        public int index;          // 0..3 levels, 4 final
        public string id, title, location, scene, objective, opponentName;
        public OpponentKind opponent;
        public string tier;        // easy / medium / hard / boss
        public string rulesPreset; // Beginner / Training / Fik
        public string profile;
        public string[] briefing, skills;
        public int badge;
        public bool usesShinpan;

        public KendoRuleConfig Rules()
        {
            switch (rulesPreset)
            {
                case "Beginner": return KendoRuleConfig.BeginnerBout();
                case "Training": return KendoRuleConfig.TrainingBout();
                default: return KendoRuleConfig.FikStandard();
            }
        }

        public KendoEvaluationProfile Profile()
        {
            switch (profile)
            {
                case "Beginner": return KendoEvaluationProfile.Beginner();
                case "Shiai": return KendoEvaluationProfile.Shiai();
                default: return KendoEvaluationProfile.Intermediate();
            }
        }
    }

    public sealed class BadgeDef
    {
        public int index;
        public string name, description, model;
    }

    /// <summary>
    /// All campaign content: seven lessons, four levels, the final battle, five badges and the story text.
    /// Kendo facts are kept to what is commonly taught to beginners; anything that varies between dojo is phrased as
    /// "usually" and the numeric thresholds live in the evaluation profiles (design values).
    /// </summary>
    public static class CampaignContent
    {
        public const string MasterName = "Sensei Hayashi";
        public const string FormerStudentName = "Kuroda";
        public const int LessonCount = 7;
        public const int LevelCount = 4;     // plus the final battle
        public const int StageCount = 5;     // four levels + final
        public const int BadgeCount = 5;

        public static readonly BadgeDef[] Badges =
        {
            new BadgeDef { index = 0, name = "Beginner Dojo Badge", description = "Completed Level 1 – Basic Training.", model = "Badge_01" },
            new BadgeDef { index = 1, name = "Improvement Badge", description = "Completed Level 2 – Improvement Training.", model = "Badge_02" },
            new BadgeDef { index = 2, name = "Skill Mastery Badge", description = "Completed Level 3 – Skill Training.", model = "Badge_03" },
            new BadgeDef { index = 3, name = "Elite Student Badge", description = "Completed Level 4 – Elite Training.", model = "Badge_04" },
            new BadgeDef { index = 4, name = "Master's Legacy", description = "Defeated the former student and completed the master's legacy.", model = "Badge_05" },
        };

        public static readonly Criterion[] StrikeBasics = { Criterion.DeliberateStrike, Criterion.ValidTarget, Criterion.StrikingPart, Criterion.Hasuji, Criterion.Posture };

        public static readonly LessonDef[] Lessons =
        {
            new LessonDef
            {
                index = 0, id = "L1", title = "Introduction to Kendo and Rules", japanese = "Kendo no kihon",
                summary = "What kendo is, dojo etiquette, the equipment, the four targets, and why touching is not striking.",
                terms = new[]
                {
                    new Term("Kendo", "\"the way of the sword\" – Japanese fencing with a bamboo sword and armour"),
                    new Term("Dojo", "the training hall; it is treated with respect"),
                    new Term("Shinai", "the bamboo practice sword"),
                    new Term("Bogu", "the armour: men (helmet), kote (gloves), do (chest protector), tare (waist guard)"),
                    new Term("Datotsu-bui", "the valid targets: men, kote, do and tsuki"),
                    new Term("Yuko-datotsu", "a valid strike that can earn a point (ippon)"),
                    new Term("Sageto", "the shinai held at the left side, tip low, when not in kamae"),
                },
                intro = new[]
                {
                    "Welcome to the dojo, student. Before you lift the shinai in anger, you must understand what we do here.",
                    "Kendo means \"the way of the sword\". We fight with a bamboo sword, the shinai, and wear armour called bogu. But the purpose is not to hurt anyone – it is to forge the spirit through correct, disciplined practice.",
                },
                explain = new[]
                {
                    "In the dojo we bow when we enter and leave, we listen to the teacher, and we never strike someone who is not ready. Discipline first, technique second.",
                    "There are only four places you may strike. Men – the head. Kote – the right forearm. Do – the side of the chest protector. Tsuki – a thrust to the throat guard, which beginners do not use against people.",
                    "Hitting a target is not enough. A valid strike, yuko-datotsu, is a deliberate cut with the top part of the shinai, the right edge leading, full spirit, good posture – and readiness afterwards. A clumsy touch or a bump is only contact. It never counts.",
                    "When you are not in a fighting stance, hold the shinai at your left side with the tip low. This is sageto.",
                },
                demo = DemoKind.PointTargets,
                demoNarration = new[] { "Watch. Men… kote… do… and tsuki. Now see the difference: this is a touch – nothing. This is a strike." },
                completion = new[] { "Good. You know where to strike and why a touch is not a point. Next, the first thing every kendoka learns: how to bow." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L1.identify", kind = ExerciseKind.IdentifyTarget, title = "Name the targets",
                        instruction = "I will call a target. Look at that part of the bogu stand and press F.",
                        hint = "Men is the helmet, kote the arm bar, do the chest plate, tsuki the throat flap below the helmet grille.",
                        targets = new[] { Target.Men, Target.KoteRight, Target.DoRight, Target.Tsuki }, requiredSuccesses = 4,
                    },
                    new ExerciseDef
                    {
                        id = "L1.handling", kind = ExerciseKind.ShinaiHandling, title = "Carry the shinai",
                        instruction = "Press X to lower the shinai to sageto at your left side. Hold it still, then press X again to return to the ready stance.",
                        hint = "In sageto the tip points down and forward. Do not wave the shinai around in the dojo.",
                        requiredSuccesses = 1,
                    },
                    new ExerciseDef
                    {
                        id = "L1.contact", kind = ExerciseKind.ContactVersusStrike, title = "Touch is not a strike",
                        instruction = "First, walk forward slowly until your shinai just touches the men of the bogu stand – no swing. Then step back and strike men properly (left click).",
                        hint = "Hold the right mouse button and raise the tip to touch the men without swinging.",
                        targets = new[] { Target.Men }, requiredSuccesses = 1,
                        required = new[] { Criterion.DeliberateStrike, Criterion.ValidTarget },
                    },
                },
            },
            new LessonDef
            {
                index = 1, id = "L2", title = "Rei – Bowing", japanese = "Rei",
                summary = "The standing bow, entering and leaving the dojo, and bowing to the teacher.",
                terms = new[]
                {
                    new Term("Rei", "a bow – respect and gratitude"),
                    new Term("Ritsu-rei", "the standing bow"),
                    new Term("Kamiza / shomen", "the front of the dojo, the place of honour"),
                    new Term("Sensei", "teacher"),
                    new Term("Metsuke", "where you look; when bowing to a person you keep your eyes on them"),
                },
                intro = new[] { "Kendo begins and ends with rei. A strike without respect is just violence." },
                explain = new[]
                {
                    "When you enter the dojo, stop at the entrance, face the kamiza – the front of the hall – and bow. A deeper bow, about thirty degrees.",
                    "To a teacher or partner the bow is smaller, about fifteen degrees, and you keep your eyes on them. Never look at the floor when you bow to a person.",
                    "Hold the shinai in sageto at your left side. Bend from the hips with a straight back, pause for a moment, and rise.",
                    "When you leave, you turn to the kamiza at the entrance and bow again.",
                },
                demo = DemoKind.Rei,
                demoNarration = new[] { "To the kamiza – deep, calm. To a partner – small, eyes forward. Pause… rise." },
                completion = new[] { "Your bows show respect. Remember: courtesy is not decoration, it is the first technique. Now, the stance." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L2.rei", kind = ExerciseKind.Rei, title = "Enter, greet, leave",
                        instruction = "Go to the glowing mark at the entrance, face the kamiza and hold B to bow (about 30°). Then walk to me and bow (about 15°) while looking at me. Finally return to the entrance and bow to the kamiza as you leave.",
                        hint = "Hold B to bend, release to rise. The depth meter shows your angle. Press X first so the shinai is in sageto.",
                        reiSequence = new[] { ReiPlace.EntranceToKamiza, ReiPlace.ToMaster, ReiPlace.LeavingToKamiza }, requiredSuccesses = 3,
                    },
                },
            },
            new LessonDef
            {
                index = 2, id = "L3", title = "Chudan-no-Kamae", japanese = "Chudan-no-kamae",
                summary = "The basic stance: feet, posture, the two-handed grip and the kensen line.",
                terms = new[]
                {
                    new Term("Kamae", "a stance or guard"),
                    new Term("Chudan-no-kamae", "the middle stance – the basic stance of kendo"),
                    new Term("Kensen", "the tip of the shinai"),
                    new Term("Tsuka", "the grip of the shinai"),
                    new Term("Issoku-itto-no-maai", "the basic distance: one step away from striking"),
                },
                intro = new[] { "From chudan-no-kamae you can attack and defend. Every technique starts and ends here." },
                explain = new[]
                {
                    "Feet: right foot forward, left foot behind about a fist's width apart, left heel slightly raised. Weight balanced, ready to push off with the left foot.",
                    "Grip: left hand at the end of the tsuka, about one fist in front of your navel. Right hand lightly behind the guard. The left hand does the work.",
                    "Kensen: the tip points at your opponent's throat, on the centre line. Shoulders relaxed, back straight, eyes on the whole opponent.",
                },
                demo = DemoKind.Kamae,
                demoNarration = new[] { "Right foot forward. Left fist before the navel. The kensen finds the throat. Still." },
                completion = new[] { "That is a living kamae. Hold it like that and your opponent will feel the kensen on his throat. Next: how to move." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L3.kamae", kind = ExerciseKind.Kamae, title = "Take chudan",
                        instruction = "Stand on the footprints facing the bogu stand. Press X to take chudan if you are in sageto. Hold the right mouse button and aim the kensen at the throat ring, then keep still.",
                        hint = "The ring turns gold when the kensen is on the throat line. Hold it for a moment without moving.",
                        requiredSuccesses = 2, profile = "Beginner",
                    },
                },
            },
            new LessonDef
            {
                index = 3, id = "L4", title = "Suriashi", japanese = "Suriashi / okuri-ashi",
                summary = "Sliding footwork forward, back and to the sides with okuri-ashi, keeping kamae and balance.",
                terms = new[]
                {
                    new Term("Suriashi", "sliding footwork – the feet glide over the floor"),
                    new Term("Okuri-ashi", "the basic step: the lead foot moves, the rear foot follows at once – the feet never cross"),
                    new Term("Enzan-no-metsuke", "\"looking at a far mountain\" – eyes forward, seeing the whole opponent"),
                },
                intro = new[] { "A kendoka who cannot move cannot strike. We do not walk, we glide." },
                explain = new[]
                {
                    "Okuri-ashi: to go forward, the right foot slides first and the left foot follows immediately. To go back, the left foot moves first and the right follows. Sideways, the foot on that side leads.",
                    "The feet never cross and never leave the floor. Keep your kamae – the kensen stays on the centre line while you move.",
                    "Keep your eyes forward, enzan-no-metsuke. Looking at your feet breaks your posture.",
                },
                demo = DemoKind.Suriashi,
                demoNarration = new[] { "Forward… forward… back… back. To the left… to the right. The kensen never moves." },
                completion = new[] { "Your feet are learning. Good footwork is invisible – the opponent only sees the kensen coming closer." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L4.line", kind = ExerciseKind.Suriashi, title = "Forward and back",
                        instruction = "Follow the marks: W three steps forward, then S three steps back. Keep chudan and look ahead.",
                        hint = "Each tap of W or S is one okuri-ashi step. Do not look at the floor.",
                        moves = new[] { SuriashiMove.Forward, SuriashiMove.Forward, SuriashiMove.Forward, SuriashiMove.Back, SuriashiMove.Back, SuriashiMove.Back },
                        requiredSuccesses = 1,
                    },
                    new ExerciseDef
                    {
                        id = "L4.side", kind = ExerciseKind.Suriashi, title = "Left and right",
                        instruction = "Now sideways: D two steps right, then A two steps left, back to the start mark.",
                        hint = "Sideways steps are smaller. Keep facing the front.",
                        moves = new[] { SuriashiMove.Right, SuriashiMove.Right, SuriashiMove.Left, SuriashiMove.Left },
                        requiredSuccesses = 1,
                    },
                },
            },
            new LessonDef
            {
                index = 4, id = "L5", title = "Basic Strikes", japanese = "Kihon-uchi",
                summary = "Men, kote, do and tsuki with the monouchi and correct hasuji.",
                terms = new[]
                {
                    new Term("Monouchi", "the striking part – roughly the top quarter of the shinai, on the blade side"),
                    new Term("Tsuru", "the string on the back of the shinai – it marks the back of the blade"),
                    new Term("Hasuji", "the cutting angle: the edge (opposite the tsuru) must lead along the line of the cut"),
                    new Term("Furikaburi", "raising the shinai before the cut"),
                    new Term("Men / Kote / Do / Tsuki", "head / right forearm / chest side / throat thrust"),
                },
                intro = new[] { "Now we cut. A good strike is not hard – it is correct." },
                explain = new[]
                {
                    "Raise the shinai straight up the centre line – furikaburi – and cut down so the monouchi, the top part of the shinai, lands on the target.",
                    "The tsuru, the string, faces you. That means the cutting edge faces the target. If the shinai rolls, the flat side lands and the cut is dead: that is bad hasuji.",
                    "Men: straight down on top of the head. Kote: a small, sharp cut onto the right forearm. Do: cut diagonally onto the right side of the chest protector. Tsuki: thrust straight to the throat guard – it is dangerous, so beginners practise it only on a stand like this one, never on a person.",
                },
                demo = DemoKind.Strikes,
                demoNarration = new[] { "Men! Kote! Do! And a careful tsuki – only on the stand." },
                completion = new[] { "Your cuts land with the monouchi and the right edge. Now we give them life: spirit, sword and body together." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L5.strikes", kind = ExerciseKind.Strike, title = "Men, kote, do, tsuki",
                        instruction = "Strike the target I call on the bogu stand. Men: left click. Kote: E. Do: R. Tsuki: T. Step in with W if you are too far.",
                        hint = "Stand on the gold distance mark. Look at the target zone so the cut follows your eyes.",
                        targets = new[] { Target.Men, Target.KoteRight, Target.DoRight, Target.Tsuki }, requiredSuccesses = 4,
                        required = StrikeBasics,
                    },
                },
            },
            new LessonDef
            {
                index = 5, id = "L6", title = "Ki-Ken-Tai-Itchi", japanese = "Ki-ken-tai-itchi",
                summary = "Spirit, sword and body as one: kiai, fumikomi and the cut at the same instant, from the right distance.",
                terms = new[]
                {
                    new Term("Ki-ken-tai-itchi", "spirit (ki), sword (ken) and body (tai) as one (itchi)"),
                    new Term("Kiai", "the shout that shows full spirit – you call the target: \"Men!\""),
                    new Term("Fumikomi", "the stamping step with the right foot at the moment of the strike"),
                    new Term("Maai", "distance"),
                },
                intro = new[] { "A cut with a sleeping spirit is only a movement. The voice, the foot and the sword must arrive together." },
                explain = new[]
                {
                    "From issoku-itto-no-maai – one step away – push off the left foot, stamp with the right foot (fumikomi), shout the target name (kiai) and cut, all at the same instant.",
                    "If the foot lands after the sword, or the voice comes late, the strike is broken. Ki-ken-tai-itchi is what turns a hit into a strike.",
                    "Start from the distance mark. Too close and you cannot extend; too far and you will never arrive.",
                },
                demo = DemoKind.KiKenTaiItchi,
                demoNarration = new[] { "Step… MEN! Foot, voice and sword – one sound." },
                completion = new[] { "One sound – foot, voice, sword. That is the heart of a real strike. One thing remains: what you do afterwards." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L6.kkti", kind = ExerciseKind.KiKenTaiItchi, title = "One sound",
                        instruction = "From the distance mark: press W (fumikomi), Space (kiai) and left click (men) together, so all three land at the same moment.",
                        hint = "The timing bars show your foot and voice against the moment of impact. Aim for all three inside the gold window.",
                        targets = new[] { Target.Men }, requiredSuccesses = 3,
                        required = new[] { Criterion.DeliberateStrike, Criterion.ValidTarget, Criterion.StrikingPart, Criterion.Hasuji, Criterion.Posture, Criterion.Spirit, Criterion.KiKenTaiItchi, Criterion.Distance },
                    },
                },
            },
            new LessonDef
            {
                index = 6, id = "L7", title = "Zanshin", japanese = "Zanshin",
                summary = "Follow-through, awareness after striking, and returning to kamae ready for the next exchange.",
                terms = new[]
                {
                    new Term("Zanshin", "\"remaining mind\" – staying alert and ready after the strike"),
                    new Term("Nuki / follow-through", "moving through and past after the cut"),
                },
                intro = new[] { "The strike is not finished when the shinai lands. It is finished when you are ready again." },
                explain = new[]
                {
                    "After the cut, keep moving forward and past – do not stop on top of the target. Then turn, face your opponent and return to chudan with the kensen on the throat.",
                    "Never drop the shinai, turn your back, or celebrate. Even a perfect cut is taken away if you show no zanshin – judges can cancel a point for an arrogant pose.",
                    "This completes the valid strike: target, monouchi, hasuji, spirit, posture – and zanshin.",
                },
                demo = DemoKind.Zanshin,
                demoNarration = new[] { "MEN! – through… turn… kamae. The mind stays." },
                completion = new[] { "Now your strike is complete. You have finished the fundamentals. Soon you will face real opponents – the level board at the entrance is open to you." },
                exercises = new[]
                {
                    new ExerciseDef
                    {
                        id = "L7.zanshin", kind = ExerciseKind.Zanshin, title = "Complete strike",
                        instruction = "Strike men with kiai and fumikomi, keep moving forward past the stand (keep W held), then turn back with the mouse, staying in chudan, and point the kensen at the stand again within two seconds.",
                        hint = "After the hit, keep W held for half a second, then turn to face the stand. Do not lower the shinai.",
                        targets = new[] { Target.Men }, requiredSuccesses = 2,
                        required = new[] { Criterion.DeliberateStrike, Criterion.ValidTarget, Criterion.StrikingPart, Criterion.Hasuji, Criterion.Posture, Criterion.Spirit, Criterion.Zanshin },
                    },
                },
            },
        };

        public static readonly LevelDef[] Stages =
        {
            new LevelDef
            {
                index = 0, id = "level1", title = "Level 1 – Basic Training", location = "The dojo floor, morning practice", scene = "03_Level1",
                objective = "Win a sanbon-shobu (first to two ippon) against a fellow beginner. Tsuki is not allowed.",
                opponentName = "Kenji (beginner)", opponent = OpponentKind.RuleBased, tier = "easy", rulesPreset = "Beginner", profile = "Beginner",
                badge = 0, usesShinpan = false, skills = new[] { "movement", "stance", "basic attack", "basic defence" },
                briefing = new[]
                {
                    "Your first real bout. Kenji has trained here for a few months. He is slow, but he will punish wild swings.",
                    "Keep your kamae, close the distance with suriashi, and strike when you are at the right distance. Shift raises your guard.",
                },
            },
            new LevelDef
            {
                index = 1, id = "level2", title = "Level 2 – Improvement Training", location = "The dojo, evening practice", scene = "04_Level2",
                objective = "Win a sanbon-shobu against a faster opponent who defends and counters. Three shinpan judge every strike.",
                opponentName = "Haruka (intermediate)", opponent = OpponentKind.RuleBased, tier = "medium", rulesPreset = "Beginner", profile = "Beginner",
                badge = 1, usesShinpan = true, skills = new[] { "timing", "defence", "attack combinations", "positioning" },
                briefing = new[]
                {
                    "Haruka blocks well and counters the moment you leave an opening. Attacking first is not enough.",
                    "Block her strike, then answer at once – the counter is your opportunity. And keep zanshin, or the judges will not raise their flags.",
                },
            },
            new LevelDef
            {
                index = 2, id = "level3", title = "Level 3 – Skill Training", location = "The temple hall", scene = "05_Level3",
                objective = "Defeat an opponent who has studied the way YOU fight. Random attacks will not work.",
                opponentName = "The Shadow (fights like you)", opponent = OpponentKind.Imitation, tier = "hard", rulesPreset = "Training", profile = "Intermediate",
                badge = 2, usesShinpan = true, skills = new[] { "strategy", "observation", "counterattacks", "reaction" },
                briefing = new[]
                {
                    "This opponent has watched every bout you fought here. He moves like you, attacks like you – and makes your mistakes.",
                    "To win, you must change. Observe him, find the habit you share, and use it against him.",
                },
            },
            new LevelDef
            {
                index = 3, id = "level4", title = "Level 4 – Elite Training", location = "The temple hall at dusk", scene = "06_Level4",
                objective = "Defeat the temple champion. He adapts to you during the bout.",
                opponentName = "Temple champion", opponent = OpponentKind.PddaRl, tier = "hard", rulesPreset = "Fik", profile = "Intermediate",
                badge = 3, usesShinpan = true, skills = new[] { "complete skill set", "advanced timing", "defence", "counters" },
                briefing = new[]
                {
                    "The temple champion is the best I could find. After every point, he changes – he learns how to beat the way you fight.",
                    "Do not repeat what worked once. Vary your attacks, defend, counter, and keep perfect zanshin.",
                },
            },
            new LevelDef
            {
                index = 4, id = "final", title = "Final Battle – The Former Student", location = "The former student's hall", scene = "07_FinalBattle",
                objective = "Defeat Kuroda in a sanbon-shobu. He feints, chains attacks, strikes as you move, and adapts to you.",
                opponentName = "Kuroda, the former student", opponent = OpponentKind.FormerStudent, tier = "boss", rulesPreset = "Fik", profile = "Shiai",
                badge = 4, usesShinpan = true, skills = new[] { "everything" },
                briefing = new[]
                {
                    "He is fast, he feints, and he strikes the moment you begin to move. He will try to make you attack first.",
                    "Be patient. Keep your centre. When he overreaches, answer. And whatever happens – zanshin.",
                },
            },
        };

        public static LessonDef Lesson(int i) => i >= 0 && i < Lessons.Length ? Lessons[i] : null;
        public static LevelDef Stage(int i) => i >= 0 && i < Stages.Length ? Stages[i] : null;

        public static IEnumerable<Term> AllTerms()
        {
            foreach (var l in Lessons) foreach (var t in l.terms) yield return t;
        }
    }
}
