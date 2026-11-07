"""Evaluates trained policies on (a) the fixed evaluation environments and (b) held-out randomised conditions
(sensor noise, input latency, speed/movement scaling, start distance, court size, opponent styles never seen in
training). python3 -I eval_policies.py runs/a2c_fixed/final_policy.json runs/a2c_dr/final_policy.json ..."""
import sys as _s, os as _o; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__)))
import sys, json, os
import numpy as np
import duel_sim as D, policies as P, train_a2c as T
from a2c import ActorCritic, sample
from krandom import KRandom

def play(ac, sim, adv, rng):
    n = 0
    while not sim.done and n < 1500:
        a1 = sample(ac.policy(np.array(sim.observe(1)))[0], rng.value()) if sim.can_act(sim.f[1]) else 0
        sim.step(adv.act(sim.observe(0)), a1); n += 1
    return sim.match.winner == 1

def randomised(ac, forests, episodes=300, seed=424242):
    r = KRandom(seed); rng = KRandom(seed + 1); wins = 0
    for e in range(episodes):
        params = T.rand_params(r); tech = (T.rand_tech(r), P.tech_medium())
        sim = D.DuelSim(seed * 7 + e, params, tech)
        adv = T.ImitationAdversary(forests['defensive_counter'], KRandom(e)) if r.value() < 0.3 else P.RuleBasedPolicy(T.rand_style(r), KRandom(e + 9))
        wins += play(ac, sim, adv, rng)
    return wins / episodes

if __name__ == '__main__':
    forests = {n: T.fit_persona(n, 100 + i)[0] for i, n in enumerate(T.PERSONAS)}
    res = {}
    rnd = ActorCritic(D.OBS_SIZE, D.N_ACTIONS, seed=1)
    for path in ['(untrained)'] + sys.argv[1:]:
        ac = rnd if path == '(untrained)' else ActorCritic.from_json(open(path).read())
        fixed = T.evaluate(ac, forests, episodes=100, seed=991)
        res[path] = {'fixed': {k: v['win_rate'] for k, v in fixed.items()}, 'randomised_heldout': randomised(ac, forests)}
        print(path, json.dumps(res[path]), flush=True)
    json.dump(res, open('runs/evaluation.json', 'w'), indent=1)
