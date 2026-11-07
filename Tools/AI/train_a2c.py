"""Train the Agent-3 A2C opponent (numpy implementation, runs without PyTorch) in the kendo duel simulator.

Paper-derived (Fuchs et al. 2024): A2C agent trained in a separate environment against an imitation-learning agent that
models the player (Adaptive Random Forest), reward = + for the opponent losing "hit points" (ippon), − for own losses.
Additional design decisions: domain randomisation of opponent/arena/sensing parameters, a mixture of rule-based and
imitation adversaries for the shipped base policy, fixed evaluation environments, entropy bonus 0.01.

Usage: python3 -I train_a2c.py --timesteps 600000 --seed 1 --out runs/a2c_dr
       python3 -I train_a2c.py --timesteps 600000 --seed 1 --no-dr --out runs/a2c_fixed     (ablation)"""
import argparse, json, os, sys, time, csv
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import duel_sim as D, policies as P, arf
from a2c import ActorCritic, A2C, sample
from krandom import KRandom

PERSONAS = {  # stand-ins for human players whose behaviour the imitation agent learns (no real player data exists offline)
    'aggressive_men': (P.Style(aggression=0.85, reaction=0.35, defense=0.2, counter=0.2, mix=(0.8, 0.1, 0.1, 0.0), preferred_distance=2.0), P.tech_medium),
    'defensive_counter': (P.Style(aggression=0.25, reaction=0.2, defense=0.75, counter=0.75, mix=(0.3, 0.5, 0.2, 0.0), preferred_distance=2.5), P.tech_medium),
    'kote_mover': (P.Style(aggression=0.5, reaction=0.3, defense=0.4, counter=0.4, mix=(0.2, 0.7, 0.1, 0.0), jitter=0.35, preferred_distance=2.2), P.tech_easy),
}

def rand_style(r):
    mix = [r.range(0.1, 1.0) for _ in range(4)]
    return P.Style(aggression=r.range(0.1, 0.9), reaction=r.range(0.1, 0.5), defense=r.range(0.1, 0.8), counter=r.range(0.0, 0.7),
                   preferred_distance=r.range(1.9, 2.6), mix=mix, jitter=r.range(0.0, 0.3),
                   feint=r.range(0, 0.4) if r.value() < 0.5 else 0.0, combo=r.range(0, 0.4) if r.value() < 0.5 else 0.0,
                   debana=r.range(0, 0.4) if r.value() < 0.5 else 0.0, hiki=r.range(0, 0.4) if r.value() < 0.5 else 0.0)

def rand_tech(r, lo=6.5, hi=10.5):
    return D.Technique(speed=r.range(lo, hi), hasuji_sigma=r.range(6, 20), kiai_prob=r.range(0.6, 0.98), zanshin_prob=r.range(0.5, 0.95),
                       guard=r.range(0.1, 0.55), timing_sigma=r.range(0.04, 0.14))

def rand_params(r):
    return D.EnvParams(court_half=r.range(4.0, 5.5), start_distance=r.range(2.2, 3.4), tracking_noise=r.range(0.0, 0.08),
                       latency_steps=(r.int(3), r.int(3)), speed_scale=(r.range(0.85, 1.15), r.range(0.85, 1.15)),
                       move_scale=(r.range(0.8, 1.2), r.range(0.8, 1.2)))

class ImitationAdversary:
    """Agent 2 in the training environment: samples actions from the ARF's predicted distribution."""
    def __init__(s, forest, rng): s.f, s.rng = forest, rng
    def act(s, o): return sample(s.f.proba(o), s.rng.value())

def fit_persona(name, seed, n=6000):
    style, tech = PERSONAS[name]
    forest = arf.AdaptiveRandomForest(D.OBS_SIZE, D.N_ACTIONS, seed=seed)
    sim = D.DuelSim(seed); pol = P.RuleBasedPolicy(style, KRandom(seed + 9)); opp = P.RuleBasedPolicy(P.medium(), KRandom(seed + 10))
    acc = 0; cnt = 0
    for t in range(n):
        if sim.done: sim = D.DuelSim(seed + t, tech=(tech(), P.tech_medium()))
        o = sim.observe(0); a = pol.act(o)
        if t > n // 2: acc += forest.predict(o) == a; cnt += 1
        forest.learn(o, a); sim.step(a, opp.act(sim.observe(1)))
    return forest, acc / max(1, cnt)

class Env:
    """The learner is fighter 1 (the opponent's seat); the adversary sits in fighter 0 (the player's seat)."""
    def __init__(s, seed, dr, forests):
        s.r = KRandom(seed); s.dr = dr; s.forests = forests; s.seed = seed; s.episodes = 0; s.reset()
    def reset(s):
        r = s.r; s.episodes += 1; ep_seed = s.seed * 100003 + s.episodes
        if s.dr:
            params = rand_params(r); tech = (rand_tech(r), rand_tech(r, 7.5, 9.5))
        else:
            params = D.EnvParams(); tech = (P.tech_medium(), P.tech_medium())
        s.sim = D.DuelSim(ep_seed, params, tech)
        if s.forests and r.value() < 0.5:
            s.adv = ImitationAdversary(s.forests[r.int(len(s.forests))], KRandom(ep_seed + 1))
        else:
            s.adv = P.RuleBasedPolicy(rand_style(r) if s.dr else P.medium(), KRandom(ep_seed + 2))
        s.ret = 0.0; s.len = 0
        return np.array(s.sim.observe(1))
    def step(s, a):
        # macro decision: the learner is only asked when it can act; in-between steps it waits (same in the game)
        r = s.sim.step(s.adv.act(s.sim.observe(0)), a); rew = r[1]
        while not s.sim.done and not s.sim.can_act(s.sim.f[1]) and s.len < 1500:
            r = s.sim.step(s.adv.act(s.sim.observe(0)), 0); rew += r[1]; s.len += 1
        r = [0.0, rew]
        s.ret += r[1]; s.len += 1
        done = s.sim.done or s.len >= 1500
        info = None
        if done:
            info = {'return': s.ret, 'win': 1.0 if s.sim.match.winner == 1 else 0.0, 'len': s.len}
            obs = s.reset()
        else:
            obs = np.array(s.sim.observe(1))
        return obs, r[1], done, info

FIXED_EVAL = {  # fixed evaluation environments: no randomisation, held-out seeds
    'easy': ('rule', 'easy'), 'medium': ('rule', 'medium'), 'hard': ('rule', 'hard'), 'former_student': ('rule', 'boss'),
    'imit_aggressive_men': ('imit', 'aggressive_men'), 'imit_defensive_counter': ('imit', 'defensive_counter'),
}

def evaluate(ac, forests_by_name, episodes=40, seed=777, own_tech=P.tech_medium):
    out = {}
    rng = KRandom(seed)
    for name, (kind, key) in FIXED_EVAL.items():
        wins = 0; pts_for = 0; pts_against = 0
        for e in range(episodes):
            s = seed * 1000 + e
            if kind == 'rule':
                style, tech = P.TIERS[key][0](), P.TIERS[key][1]()
                adv = P.RuleBasedPolicy(style, KRandom(s + 5))
            else:
                tech = PERSONAS[key][1](); adv = ImitationAdversary(forests_by_name[key], KRandom(s + 6))
            sim = D.DuelSim(s, D.EnvParams(), (tech, own_tech())); n = 0
            while not sim.done and n < 1500:
                o1 = sim.observe(1)
                a1 = sample(ac.policy(np.array(o1))[0], rng.value()) if sim.can_act(sim.f[1]) else 0
                sim.step(adv.act(sim.observe(0)), a1); n += 1
            wins += sim.match.winner == 1; pts_for += sim.match.points[1]; pts_against += sim.match.points[0]
        out[name] = {'win_rate': wins / episodes, 'points_for': pts_for / episodes, 'points_against': pts_against / episodes}
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--timesteps', type=int, default=600000)
    ap.add_argument('--seed', type=int, default=1)
    ap.add_argument('--n-envs', type=int, default=16)
    ap.add_argument('--n-steps', type=int, default=10)
    ap.add_argument('--no-dr', action='store_true')
    ap.add_argument('--eval-every', type=int, default=250000)
    ap.add_argument('--out', default='runs/a2c')
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    t0 = time.time()
    forests = {}; persona_acc = {}
    for i, name in enumerate(PERSONAS):
        forests[name], persona_acc[name] = fit_persona(name, 100 + i)
    print('imitation accuracy (held-out half of stream):', {k: round(v, 3) for k, v in persona_acc.items()}, flush=True)
    train_forests = [forests['aggressive_men'], forests['kote_mover']]   # defensive_counter is held out for evaluation
    ac = ActorCritic(D.OBS_SIZE, D.N_ACTIONS, seed=a.seed); opt = A2C(ac)
    envs = [Env(a.seed * 50 + i, not a.no_dr, train_forests) for i in range(a.n_envs)]
    obs = np.stack([np.array(e.sim.observe(1)) for e in envs])
    act_rng = KRandom(a.seed + 4242)
    log = open(os.path.join(a.out, 'progress.csv'), 'w', newline=''); w = csv.writer(log)
    w.writerow(['timesteps', 'episodes', 'mean_return_100', 'train_win_rate_100', 'policy_loss', 'value_loss', 'entropy', 'wall_s'])
    evlog = open(os.path.join(a.out, 'eval.jsonl'), 'w')
    returns, wins = [], []
    steps = 0; next_eval = a.eval_every; stats = {}
    ev0 = evaluate(ac, forests); evlog.write(json.dumps({'timesteps': 0, 'eval': ev0}) + '\n'); evlog.flush()
    print('eval@0', {k: v['win_rate'] for k, v in ev0.items()}, flush=True)
    while steps < a.timesteps:
        bo, ba, br, bd = [], [], [], []
        for _ in range(a.n_steps):
            p = ac.policy(obs)
            acts = np.array([sample(p[i], act_rng.value()) for i in range(len(envs))])
            nobs, rew, dn = [], [], []
            for i, e in enumerate(envs):
                o2, r, d, info = e.step(int(acts[i]))
                nobs.append(o2); rew.append(r); dn.append(d)
                if info: returns.append(info['return']); wins.append(info['win'])
            bo.append(obs); ba.append(acts); br.append(rew); bd.append(dn)
            obs = np.stack(nobs); steps += len(envs)
        last_v = ac.value(obs)
        R = np.zeros((a.n_steps, len(envs))); nxt = last_v
        for t in reversed(range(a.n_steps)):
            nxt = np.array(br[t]) + 0.99 * nxt * (1 - np.array(bd[t], float)); R[t] = nxt
        stats = opt.update(np.concatenate(bo), np.concatenate(ba), R.reshape(-1))
        if steps % 50000 < len(envs) * a.n_steps:
            w.writerow([steps, len(returns), round(float(np.mean(returns[-100:])), 4) if returns else '', round(float(np.mean(wins[-100:])), 3) if wins else '',
                        round(stats['policy_loss'], 5), round(stats['value_loss'], 5), round(stats['entropy'], 4), round(time.time() - t0, 1)]); log.flush()
        if steps >= next_eval:
            next_eval += a.eval_every
            ev = evaluate(ac, forests)
            evlog.write(json.dumps({'timesteps': steps, 'wall_s': round(time.time() - t0, 1), 'eval': ev}) + '\n'); evlog.flush()
            open(os.path.join(a.out, f'checkpoint_{steps}.json'), 'w').write(ac.to_json())
            print(f'{steps} steps  {time.time() - t0:.0f}s  train win {np.mean(wins[-100:]):.2f}  eval', {k: v['win_rate'] for k, v in ev.items()}, flush=True)
    open(os.path.join(a.out, 'final_policy.json'), 'w').write(ac.to_json())
    summary = {'timesteps': steps, 'wall_s': round(time.time() - t0, 1), 'domain_randomisation': not a.no_dr, 'seed': a.seed,
               'imitation_accuracy': persona_acc, 'final_eval': evaluate(ac, forests, episodes=100, seed=991),
               'episodes': len(returns), 'final_train_win_rate_100': float(np.mean(wins[-100:])) if wins else None}
    json.dump(summary, open(os.path.join(a.out, 'summary.json'), 'w'), indent=1)
    print(json.dumps(summary, indent=1))

if __name__ == '__main__':
    main()
