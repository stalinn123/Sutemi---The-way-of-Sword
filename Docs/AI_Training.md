# Three-agent opponent AI: method, training procedure, results

## What comes from the paper and what was added
Fuchs, Gieseke & Dockhorn (2024), *Personalized Dynamic Difficulty Adjustment – Imitation Learning Meets Reinforcement Learning*.

| | Paper | This project |
|---|---|---|
| Agent 1 | rule-based starting opponent | `Kendo.AI.RuleBasedPolicy` with configurable aggression, preferred distance, reaction time, defence, counter rate, attack mix, movement jitter, feints, renzoku (combo), debana and hiki-waza; tiers easy/medium/hard/former student |
| Agent 2 | observes the player and imitates them with River's Adaptive Random Forest | in-game: `Kendo.AI.AdaptiveRandomForest` (ARF-style: Poisson(6) online bagging of Hoeffding trees on random feature subsets, accuracy-weighted vote, drift-triggered tree replacement). **Simplified vs River:** DDM drift detection instead of ADWIN, majority-class leaves. Offline: `train_sb3.py` uses River's `ARFClassifier` |
| Agent 3 | Stable-Baselines3 A2C trained against Agent 2 in a background environment | in-game: `Kendo.AI.A2CTrainer` fine-tunes the shipped policy against Agent 2 inside `DuelSim`, ~2 ms per frame during Level 4 and the final battle. Offline: `train_a2c.py` (numpy A2C, run here) and `train_sb3.py` (SB3, *not run here – packages could not be installed*) |
| Swap | the opponent is replaced by the RL agent at fixed intervals | replaced at every restart after a point, once Agent 2 has ≥ 200 observations and the trainer has ≥ 2,000 steps |
| Reward | + for reducing the opponent's hit points, − for losing own | ±1 per ippon (awarded by the shared rules engine and three-shinpan panel), ±1 for winning/losing, −0.3 hansoku, −0.02 for a strike that reaches nothing, +0.05 × fraction of official criteria met for a non-awarded strike, −0.001 per step (design values, `KendoReward`) |
| Observation | relative positions, actions, player input (images for RL) | 26 features: distance, court positions, lateral offset, both fighters' phase one-hots and progress, the incoming strike's target, counter window, score difference, time left, opponent blocking. **The game computes the same vector from the real scene** (`GameObservation`) |
| Domain randomisation | not in the paper | opponent aggression, attack speed, reaction time, attack timing/mix, movement speed, start distance, defensive behaviour, court size, tracking noise, input latency, own technique |

Hyper-parameters (SB3 A2C defaults): lr 7e-4 RMSprop (α 0.99, ε 1e-5), γ 0.99, n-steps 10, 16 envs, value coef 0.5, max grad norm 0.5, separate 64-64 tanh actor and critic. Entropy coefficient 0.01 is a design choice (SB3 default 0.0). The policy is only asked for a decision when its fighter can act; without this, a 600k-step run did not learn at all (entropy stayed at 2.37 of a maximum 2.40).

## Real results (all numbers from `Tools/AI/runs/*`, produced in this workspace)
Imitation accuracy of the ARF on the held-out half of each stream: 0.86 / 0.81 / 0.80 for three synthetic player personas. There was no human player data offline: in the game, Agent 2 learns from the real player.

| Policy | Steps | Wall time | Fixed eval: easy / medium / hard / former student | Held-out persona | Held-out randomised conditions |
|---|---|---|---|---|---|
| untrained | 0 | – | 0.82 / 0.62 / 0.51 / 0.11 | 0.01 | 0.59 |
| fixed env, seed 1 (**shipped**) | 3.0 M | 335 s | 1.00 / 0.99 / 1.00 / 0.76 | 0.91 | 0.64 |
| fixed env, seed 2 | 3.0 M | 314 s | 1.00 / 0.98 / 0.99 / 0.79 | 0.96 | 0.63 |
| domain randomisation | 3.0 M | 369 s | 0.90 / 0.77 / 0.57 / 0.42 | 0.00 | 0.61 |
| domain randomisation (10 M run, stopped by the workspace at 4 M) | 4.0 M | 418 s | 0.92 / 0.67 / 0.43 / 0.27 | 0.00 | **0.65** |

Fixed evaluation: 100 bouts per opponent, held-out seeds, the learner uses medium technique. Randomised: 300 bouts with randomised conditions and opponent styles never used in training.

**Interpretation.**
- Training in the fixed environment converges within about 2 M steps, and two seeds agree. The policy generalises to an imitation persona it never trained against (0.91).
- Domain randomisation did not pay off within 3–4 M steps on the fixed suite. It is only marginally best on the randomised held-out suite (0.65 vs 0.64), which is within noise.
- I ship the seed-1 fixed-env policy (`Assets/_Project/Resources/AI/opponent_a2c.json`). A longer domain-randomisation run is the obvious next experiment.
- These are simulator results. How the agents play against a human in the 3D game has **not** been measured.

Curves: `Docs/Generated/A2C_Training.png`. Logs: `runs/<run>/progress.csv` (episodes, mean return, train win rate, losses, entropy, wall time), `eval.jsonl` (periodic fixed evaluation), `checkpoint_*.json`, `summary.json`, `runs/evaluation.json`.

## Reproduce
```
cd Tools/AI
python3 -I train_a2c.py --timesteps 3000000 --seed 1 --no-dr --out runs/a2c_fixed      # fixed environment
python3 -I train_a2c.py --timesteps 3000000 --seed 1 --out runs/a2c_dr                 # domain randomisation
python3 -I eval_policies.py runs/a2c_fixed/final_policy.json runs/a2c_dr/final_policy.json
cp runs/a2c_fixed/final_policy.json ../../Assets/_Project/Resources/AI/opponent_a2c.json
python3 -m unittest test_ai.py ../Rules/test_rules.py
```
Paper stack (needs `pip install stable-baselines3 gymnasium river torch`; not run here):
```
python3 train_sb3.py --player-log "<Application.persistentDataPath>/Saves/kendo_player_behaviour.bin" --timesteps 2000000 --out runs/sb3
```
It exports the same JSON format, which the game loads unchanged.

## How the agents run in the game
- **Level 1–2:** Agent 1 (easy / medium).
- **Level 3:** Agent 2 fights: it plays like the player, sampling from the ARF trained on everything the player did in earlier bouts. It needs 200 observations; until then it is rule-based.
- **Level 4 and the final battle:** the paper's loop:
  1. Agent 1 starts (the boss uses its own style, scaled by the DDA controller).
  2. Agent 2 learns from the player every 0.1 s decision.
  3. Agent 3 trains against Agent 2 in the background.
  4. After each point the live opponent becomes the current Agent 3.
- The fine-tuned policy and the behaviour log are saved: `kendo_personal_policy.json` and `kendo_player_behaviour.bin`, next to the save file.
- AI fighters execute their actions through the same commands, motion and strike sensor as the player. Their technique imperfections are real changes to their motion and timing: hasuji roll noise, kiai and fumikomi timing, and dropping the guard instead of zanshin.
