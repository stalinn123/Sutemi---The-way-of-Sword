"""Gymnasium wrapper around the duel simulator for Stable-Baselines3 (paper pipeline). Requires gymnasium.
The learner sits in the opponent's seat; the adversary is the imitation agent (River ARF or the built-in ARF) or a
rule-based opponent; domain randomisation as in train_a2c.py. Decisions are only requested when the learner can act."""
import sys as _s, os as _o; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__)))
import numpy as np
import gymnasium as gym
from gymnasium import spaces
import duel_sim as D, policies as P
from krandom import KRandom
import train_a2c as T

class KendoDuelEnv(gym.Env):
    metadata = {'render_modes': []}
    def __init__(self, seed=0, domain_randomisation=True, imitation_models=None, imitation_fraction=0.5):
        super().__init__()
        self.observation_space = spaces.Box(-5.0, 5.0, shape=(D.OBS_SIZE,), dtype=np.float32)
        self.action_space = spaces.Discrete(D.N_ACTIONS)
        self.models = imitation_models or []
        self.frac = imitation_fraction
        self.inner = T.Env(seed, domain_randomisation, [])
        self.inner.forests = []
        self.r = KRandom(seed + 17)
    def _adversary(self):
        if self.models and self.r.value() < self.frac:
            m = self.models[self.r.int(len(self.models))]
            return T.ImitationAdversary(m, KRandom(self.r.int(1 << 30))) if hasattr(m, 'proba') else RiverAdversary(m, KRandom(self.r.int(1 << 30)))
        return None
    def reset(self, seed=None, options=None):
        obs = self.inner.reset()
        adv = self._adversary()
        if adv is not None: self.inner.adv = adv
        return np.asarray(obs, np.float32), {}
    def step(self, action):
        obs, r, done, info = self.inner.step(int(action))
        return np.asarray(obs, np.float32), float(r), bool(done), False, (info or {})

class RiverAdversary:
    """Agent 2 from a River ARFClassifier: samples the player's predicted action."""
    def __init__(self, model, rng): self.m, self.rng = model, rng
    def act(self, o):
        p = self.m.predict_proba_one({i: v for i, v in enumerate(o)})
        if not p: return 0
        keys = sorted(p); u = self.rng.value(); c = 0.0
        for k in keys:
            c += p[k]
            if u < c: return int(k)
        return int(keys[-1])
