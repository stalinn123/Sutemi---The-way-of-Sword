"""Paper pipeline (Fuchs et al. 2024): River Adaptive Random Forest imitates the player; Stable-Baselines3 A2C trains
against it. Exports the policy in the game's JSON format (Assets/_Project/Resources/AI/opponent_a2c.json).

  pip install stable-baselines3 gymnasium river torch
  python3 train_sb3.py --player-log <persistentDataPath>/Saves/kendo_player_behaviour.bin --timesteps 2000000 --out runs/sb3

Without --player-log the synthetic personas from train_a2c.py stand in for players. NOTE: this script could not be run
in the development workspace (no network to install these packages); train_a2c.py is the numpy equivalent that was run."""
import sys as _s, os as _o; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__)))
import argparse, json, os, struct, sys
import numpy as np

def read_behaviour_log(path):
    """Reads the game's PlayerBehaviourLog (magic KBL1, int count, then [byte action, byte len, float32*len])."""
    data = open(path, 'rb').read(); o = 0
    magic, n = struct.unpack_from('<ii', data, o); o += 8
    assert magic == 0x4B424C31, 'not a Kendo behaviour log'
    X, y = [], []
    for _ in range(n):
        a = data[o]; ln = data[o + 1]; o += 2
        X.append(struct.unpack_from('<%df' % ln, data, o)); o += 4 * ln; y.append(a)
    return X, y

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--player-log'); ap.add_argument('--timesteps', type=int, default=2_000_000)
    ap.add_argument('--seed', type=int, default=1); ap.add_argument('--no-dr', action='store_true'); ap.add_argument('--out', default='runs/sb3')
    a = ap.parse_args()
    from river import forest
    from stable_baselines3 import A2C
    from stable_baselines3.common.env_util import make_vec_env
    import duel_sim as D, train_a2c as T
    from kendo_env import KendoDuelEnv
    os.makedirs(a.out, exist_ok=True)
    models = []
    if a.player_log:
        X, y = read_behaviour_log(a.player_log)
        arf = forest.ARFClassifier(n_models=10, seed=a.seed)
        correct = 0
        for i, (xi, yi) in enumerate(zip(X, y)):
            d = {k: v for k, v in enumerate(xi)}
            if i > len(X) // 2 and arf.predict_one(d) == yi: correct += 1
            arf.learn_one(d, yi)
        print('River ARF accuracy on the second half of the stream:', correct / max(1, len(X) - len(X) // 2 - 1))
        models.append(arf)
    else:
        for i, name in enumerate(['aggressive_men', 'kote_mover']): models.append(T.fit_persona(name, 100 + i)[0])
    env = make_vec_env(lambda: KendoDuelEnv(seed=a.seed, domain_randomisation=not a.no_dr, imitation_models=models), n_envs=16, seed=a.seed)
    model = A2C('MlpPolicy', env, n_steps=10, ent_coef=0.01, seed=a.seed, verbose=1, policy_kwargs=dict(net_arch=dict(pi=[64, 64], vf=[64, 64])))
    model.learn(total_timesteps=a.timesteps)
    model.save(os.path.join(a.out, 'a2c_sb3'))
    pol = model.policy
    def layers(seq, head):
        lin = [m for m in seq if m.__class__.__name__ == 'Linear'] + [head]
        return [[m.weight.detach().cpu().numpy().tolist(), m.bias.detach().cpu().numpy().tolist()] for m in lin]
    out = {'obs': D.OBS_SIZE, 'actions': D.N_ACTIONS, 'hidden': 64,
           'pi': layers(pol.mlp_extractor.policy_net, pol.action_net), 'vf': layers(pol.mlp_extractor.value_net, pol.value_net)}
    json.dump(out, open(os.path.join(a.out, 'final_policy.json'), 'w'))
    print('exported', os.path.join(a.out, 'final_policy.json'))

if __name__ == '__main__':
    main()
