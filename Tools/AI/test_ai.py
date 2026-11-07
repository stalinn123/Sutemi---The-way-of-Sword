import unittest, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import duel_sim as D, policies as P, arf, a2c
from krandom import KRandom

class SimTests(unittest.TestCase):
    def test_deterministic(self):
        def run(seed):
            s = D.DuelSim(seed); a = P.RuleBasedPolicy(P.medium(), KRandom(1)); b = P.RuleBasedPolicy(P.hard(), KRandom(2)); n = 0
            while not s.done and n < 2000: s.step(a.act(s.observe(0)), b.act(s.observe(1))); n += 1
            return s.match.points, s.match.winner, n
        self.assertEqual(run(4), run(4))
    def test_passive_opponent_never_scores(self):
        s = D.DuelSim(1)
        for _ in range(600): s.step(0, 0)
        self.assertEqual(s.match.points, [0, 0])
    def test_strike_out_of_reach_never_scores(self):
        s = D.DuelSim(2, D.EnvParams(start_distance=4.0))
        for k in range(300): s.step(5 if k % 10 == 0 else 0, 0)
        self.assertEqual(s.match.points[0], 0)
    def test_tiers_ordered(self):
        def wins(ta, tb, n=40):
            w = 0
            for sd in range(n):
                s = D.DuelSim(sd, tech=(P.TIERS[ta][1](), P.TIERS[tb][1]()))
                a = P.RuleBasedPolicy(P.TIERS[ta][0](), KRandom(sd + 1)); b = P.RuleBasedPolicy(P.TIERS[tb][0](), KRandom(sd + 2)); k = 0
                while not s.done and k < 3000: s.step(a.act(s.observe(0)), b.act(s.observe(1))); k += 1
                w += s.match.winner == 1
            return w / n
        self.assertGreater(wins('easy', 'boss'), 0.75)
        self.assertGreater(wins('easy', 'medium'), 0.5)

class LearnerTests(unittest.TestCase):
    def test_arf_learns(self):
        import random
        r = random.Random(1); f = arf.AdaptiveRandomForest(4, 11, seed=3); ok = n = 0
        for t in range(3000):
            o = [r.uniform(0.3, 1.1), 1.0 if r.random() < 0.2 else 0.0, r.random(), r.random()]
            d = o[0] * 3; y = 9 if o[1] > 0.5 else (5 if 1.8 <= d <= 2.3 else (1 if d > 2.3 else 2))
            if t > 1500: ok += f.predict(o) == y; n += 1
            f.learn(o, y)
        self.assertGreater(ok / n, 0.7)
    def test_a2c_gradient_matches_finite_difference(self):
        ac = a2c.ActorCritic(5, 4, seed=0, hidden=8); rng = np.random.default_rng(1)
        ac.pi.W[-1] = rng.standard_normal(ac.pi.W[-1].shape)
        obs = rng.standard_normal((6, 5)); acts = rng.integers(0, 4, 6); R = rng.standard_normal(6); adv = R - ac.value(obs)
        def loss():
            p = ac.policy(obs); lp = np.log(p + 1e-12); H = -(p * lp).sum(1)
            return -(adv * lp[np.arange(6), acts]).mean() - 0.01 * H.mean() + 0.5 * ((R - ac.value(obs)) ** 2).mean()
        logits, pa = ac.pi.forward(obs); p = a2c.softmax(logits); lp = np.log(p + 1e-12); oh = np.eye(4)[acts]; H = -(p * lp).sum(1)
        g = (-(adv[:, None]) * (oh - p)) / 6 + 0.01 * (p * (lp + H[:, None])) / 6
        gW, _ = ac.pi.backward(pa, g)
        W = ac.pi.W[0]; e = 1e-6; W[1, 2] += e; a = loss(); W[1, 2] -= 2 * e; b = loss(); W[1, 2] += e
        self.assertAlmostEqual(gW[0][1, 2], (a - b) / (2 * e), places=6)
    def test_shipped_policy_beats_untrained(self):
        import train_a2c as T
        path = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'AI', 'opponent_a2c.json')
        ac = a2c.ActorCritic.from_json(open(path).read())
        forests = {n: T.fit_persona(n, 100 + i, n=3000)[0] for i, n in enumerate(T.PERSONAS)}
        ev = T.evaluate(ac, forests, episodes=20, seed=5)
        self.assertGreaterEqual(ev['hard']['win_rate'], 0.8)

if __name__ == '__main__':
    unittest.main()
