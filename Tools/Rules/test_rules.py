import unittest, random, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from rules_ref import *

def good(target='Men', **kw):
    e = Evidence(target=T[target], contact=True, contactAlongBlade=0.85, edgeSide=True, hasujiErrorDeg=5, tipSpeed=9.0, strikeArc=True,
                 approachSpeed=0.8, kiai=True, kiaiOffset=0.02, fumikomi=True, fumikomiOffset=-0.03, bothHands=True, torsoTiltDeg=4,
                 balanced=True, distance=2.0, opportunity=OPP['Debana'], zanshinObserved=True, followThrough=0.6, kamaeRestoredAfter=0.8)
    for k, v in kw.items(): setattr(e, k, v)
    return e

def rand_evidence(rng):
    return Evidence(target=rng.randrange(0, 9), contact=rng.random() < 0.9, contactAlongBlade=rng.random(), edgeSide=rng.random() < 0.85,
        hasujiErrorDeg=rng.uniform(0, 90), tipSpeed=rng.uniform(0, 14), strikeArc=rng.random() < 0.8, approachSpeed=rng.uniform(0, 2),
        kiai=rng.random() < 0.7, kiaiOffset=rng.uniform(-1, 1), fumikomi=rng.random() < 0.7, fumikomiOffset=rng.uniform(-1, 1),
        bothHands=rng.random() < 0.9, torsoTiltDeg=rng.uniform(0, 40), balanced=rng.random() < 0.9, distance=rng.uniform(0.8, 3.5),
        opportunity=rng.randrange(0, 7), blocked=rng.random() < 0.15, zanshinObserved=rng.random() < 0.8, followThrough=rng.uniform(0, 1.2),
        kamaeRestoredAfter=rng.uniform(0, 4), turnedAway=rng.random() < 0.1, celebrated=rng.random() < 0.05)

R, P = fik_standard(), intermediate()

class StrikeTests(unittest.TestCase):
    def test_clean_men_is_yuko_and_awarded(self):
        ev = evaluate_strike(good(), R, P)
        self.assertTrue(ev.yukoDatotsu); self.assertGreater(ev.performanceScore, 90)
        self.assertTrue(panel_decide(ev, R)['awarded']); self.assertEqual(panel_decide(ev, R)['flags'], 3)
    def test_contact_without_swing_is_never_a_point(self):
        ev = evaluate_strike(good(strikeArc=False), R, P)
        self.assertEqual(ev.invalidReason, INV['ContactOnly']); self.assertFalse(ev.yukoDatotsu)
        self.assertLessEqual(ev.performanceScore, P.contactOnlyScoreCap); self.assertFalse(panel_decide(ev, R)['awarded'])
        self.assertEqual(reward_own_strike(ev, panel_decide(ev, R)), 0.0)
    def test_slow_touch_is_contact_only(self):
        self.assertEqual(evaluate_strike(good(tipSpeed=1.0), R, P).invalidReason, INV['ContactOnly'])
    def test_missed(self):
        ev = evaluate_strike(good(contact=False), R, P)
        self.assertEqual(ev.invalidReason, INV['Missed']); self.assertEqual(ev.performanceScore, 0)
    def test_tsuki_config(self):
        self.assertTrue(evaluate_strike(good('Tsuki'), R, P).yukoDatotsu)
        self.assertEqual(evaluate_strike(good('Tsuki'), beginner_bout(), P).invalidReason, INV['TargetNotAllowed'])
    def test_left_kote_only_against_non_chudan(self):
        self.assertEqual(evaluate_strike(good('KoteLeft'), R, P, K['Chudan']).invalidReason, INV['TargetNotAllowed'])
        self.assertTrue(evaluate_strike(good('KoteLeft'), R, P, K['Jodan']).yukoDatotsu)
    def test_monouchi_and_side(self):
        self.assertEqual(evaluate_strike(good(contactAlongBlade=0.5), R, P).invalidReason, INV['NotMonouchi'])
        self.assertEqual(evaluate_strike(good(edgeSide=False), R, P).invalidReason, INV['WrongSide'])
    def test_hasuji(self):
        self.assertEqual(evaluate_strike(good(hasujiErrorDeg=45), R, P).invalidReason, INV['BadHasuji'])
    def test_spirit(self):
        self.assertEqual(evaluate_strike(good(kiai=False), R, P).invalidReason, INV['NoSpirit'])
        self.assertEqual(evaluate_strike(good(kiaiOffset=0.6), R, P).invalidReason, INV['NoSpirit'])
    def test_posture(self):
        self.assertEqual(evaluate_strike(good(bothHands=False), R, P).invalidReason, INV['BadPosture'])
        self.assertEqual(evaluate_strike(good(torsoTiltDeg=30), R, P).invalidReason, INV['BadPosture'])
    def test_zanshin(self):
        self.assertEqual(evaluate_strike(good(zanshinObserved=False), R, P).invalidReason, INV['NoZanshin'])
        self.assertEqual(evaluate_strike(good(turnedAway=True), R, P).invalidReason, INV['NoZanshin'])
        self.assertEqual(evaluate_strike(good(kamaeRestoredAfter=3.0), R, P).invalidReason, INV['NoZanshin'])
        self.assertEqual(evaluate_strike(good(celebrated=True), R, P).invalidReason, INV['NoZanshin'])
    def test_blocked(self):
        ev = evaluate_strike(good(blocked=True), R, P)
        self.assertEqual(ev.invalidReason, INV['Blocked']); self.assertFalse(panel_decide(ev, R)['awarded'])
    def test_coaching_never_changes_validity(self):
        for kw in [dict(fumikomi=False), dict(distance=3.4), dict(opportunity=OPP['IntoReadyKamae']), dict(approachSpeed=0.0), dict(kiaiOffset=0.18)]:
            ev = evaluate_strike(good(**kw), R, P)
            self.assertTrue(ev.yukoDatotsu, kw)
    def test_kkti(self):
        self.assertTrue(evaluate_strike(good(), R, P).get('KiKenTaiItchi')[2])
        self.assertFalse(evaluate_strike(good(fumikomiOffset=0.4), R, P).get('KiKenTaiItchi')[2])
        self.assertFalse(evaluate_strike(good(kiaiOffset=-0.3), R, P).get('KiKenTaiItchi')[2])
    def test_profiles_ordered(self):
        e = good(hasujiErrorDeg=27, tipSpeed=4.5)
        self.assertTrue(evaluate_strike(e, R, beginner()).yukoDatotsu)
        self.assertFalse(evaluate_strike(e, R, shiai()).yukoDatotsu)

class InvariantTests(unittest.TestCase):
    def test_random_invariants(self):
        rng = random.Random(7)
        for prof in (beginner(), intermediate(), shiai()):
            for _ in range(10000):
                e = rand_evidence(rng)
                ev = evaluate_strike(e, R, prof, rng.randrange(0, 2))
                self.assertGreaterEqual(ev.performanceScore, 0); self.assertLessEqual(ev.performanceScore, 100.0001)
                d = panel_decide(ev, R)
                if d['awarded']: self.assertTrue(ev.yukoDatotsu)
                if not e.strikeArc or not e.contact: self.assertFalse(d['awarded'])
                if ev.invalidReason == INV['ContactOnly']: self.assertLessEqual(ev.performanceScore, prof.contactOnlyScoreCap)
                self.assertEqual(ev.yukoDatotsu, all(r[2] for r in ev.criteria if r[1]) and not e.blocked)
    def test_monotone_in_tip_speed(self):
        rng = random.Random(3)
        for _ in range(2000):
            e = rand_evidence(rng); e.contact = True; e.target = T['Men']
            a = evaluate_strike(e, R, P).performanceScore
            e.tipSpeed += 1.0
            self.assertGreaterEqual(evaluate_strike(e, R, P).performanceScore + 1e-6, a)

class PanelTests(unittest.TestCase):
    def test_marginal_strike_splits_panel(self):
        ev = evaluate_strike(good(tipSpeed=2.5), R, P)  # deliberate but slow -> small margin
        self.assertTrue(ev.yukoDatotsu)
        self.assertLess(panel_decide(ev, R)['flags'], 3)
    def test_abstentions(self):
        ev = evaluate_strike(good(), R, P)
        self.assertTrue(panel_decide(ev, R, [1.0, 0.1, 0.1])['awarded'])
        self.assertFalse(panel_decide(ev, RuleConfig(oneFlagWithAbstentionsCounts=False), [1.0, 0.1, 0.1])['awarded'])

class MatchTests(unittest.TestCase):
    def test_sanbon(self):
        m = Match(fik_standard()); m.hajime(); m.award_ippon(0)
        self.assertEqual(m.phase, Match.Yame); m.hajime(); m.award_ippon(1); m.hajime(); m.award_ippon(0)
        self.assertEqual((m.winner, m.end_reason), (0, 'Points'))
    def test_time_leader(self):
        m = Match(fik_standard()); m.hajime(); m.award_ippon(1); m.hajime()
        for _ in range(400): m.tick(1.0)
        self.assertEqual((m.winner, m.end_reason), (1, 'TimeLeader'))
    def test_encho_first_point(self):
        m = Match(fik_standard()); m.hajime()
        for _ in range(301): m.tick(1.0)
        self.assertTrue(m.in_encho); self.assertEqual(m.phase, Match.Yame)
        m.hajime(); [m.tick(1.0) for _ in range(1000)]
        self.assertEqual(m.phase, Match.Running)  # unlimited encho
        m.award_ippon(1); self.assertEqual(m.winner, 1)
    def test_limited_encho_hantei_and_hikiwake(self):
        m = Match(beginner_bout()); m.hantei_score = lambda s: [60, 40][s]; m.hajime()
        for _ in range(400): m.tick(1.0)
        self.assertTrue(m.in_encho); m.hajime()
        for _ in range(200): m.tick(1.0)
        self.assertEqual((m.winner, m.end_reason), (0, 'Hantei'))
        m = Match(beginner_bout()); m.hajime()
        for _ in range(181): m.tick(1.0)
        m.hajime()
        for _ in range(121): m.tick(1.0)
        self.assertEqual(m.end_reason, 'Hikiwake')
    def test_two_hansoku_give_ippon(self):
        m = Match(fik_standard()); m.hajime(); m.hansoku_(0, 'jogai'); self.assertEqual(m.points, [0, 0])
        m.hansoku_(0, 'jogai'); self.assertEqual(m.points, [0, 1])
    def test_clock_stops_in_yame(self):
        m = Match(fik_standard()); m.hajime(); m.yame('x'); t = m.time_remaining; m.tick(5); self.assertEqual(m.time_remaining, t)

class KamaeReiSuriashiTests(unittest.TestCase):
    def test_kamae(self):
        self.assertTrue(evaluate_kamae(KamaeEvidence(), P).passed)
        self.assertEqual(evaluate_kamae(KamaeEvidence(kensenOffset=0.3), P).failed(), ['KensenLine'])
        self.assertEqual(evaluate_kamae(KamaeEvidence(bothHands=False), P).failed(), ['Grip'])
        self.assertEqual(evaluate_kamae(KamaeEvidence(stanceLength=0.0), P).failed(), ['Feet'])
        self.assertEqual(evaluate_kamae(KamaeEvidence(heldSeconds=0.4), P).failed(), ['Stillness'])
        self.assertFalse(evaluate_kamae(KamaeEvidence(kamae=K['Sageto']), P).passed)
    def test_rei(self):
        ok = ReiEvidence(bowAngleDeg=16, targetAngleDeg=15, holdSeconds=1.0, requireGaze=True)
        self.assertTrue(evaluate_rei(ok, P).passed)
        self.assertEqual(evaluate_rei(ReiEvidence(bowAngleDeg=40, holdSeconds=1.0), P).failed(), ['BowDepth'])
        self.assertEqual(evaluate_rei(ReiEvidence(bowAngleDeg=15, holdSeconds=0.1), P).failed(), ['BowHold'])
        self.assertEqual(evaluate_rei(ReiEvidence(bowAngleDeg=15, holdSeconds=1, requireGaze=True, gazeOnPartner=False), P).failed(), ['Gaze'])
        self.assertEqual(evaluate_rei(ReiEvidence(bowAngleDeg=15, holdSeconds=1, shinaiAtSide=False), P).failed(), ['ShinaiAtSide'])
    def test_suriashi(self):
        ok = SuriashiEvidence(steps=6, stepsWithOkuriAshi=6, maxGazePitchDeg=5)
        self.assertTrue(evaluate_suriashi(ok, P).passed)
        self.assertIn('OkuriAshi', evaluate_suriashi(SuriashiEvidence(steps=6, stepsWithOkuriAshi=4), P).failed())
        self.assertIn('Accuracy', evaluate_suriashi(SuriashiEvidence(steps=6, stepsWithOkuriAshi=6, markerError=0.5), P).failed())
        self.assertFalse(evaluate_suriashi(SuriashiEvidence(), P).passed)  # no steps -> no evidence -> no success

if __name__ == '__main__':
    unittest.main()
