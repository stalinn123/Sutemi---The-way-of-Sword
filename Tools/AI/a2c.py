"""Minimal synchronous A2C (actor-critic, n-step returns, RMSprop) in numpy.
Hyper-parameters follow Stable-Baselines3 A2C defaults (lr 7e-4, n_steps 5, gamma 0.99, vf_coef 0.5, max_grad_norm 0.5,
RMSprop alpha 0.99 eps 1e-5, separate 64-64 tanh actor and critic). ent_coef 0.01 is a design choice (SB3 default 0.0).
Exact mirror of Kendo.AI.ActorCritic / A2CTrainer (C#), which the game uses for in-game background training."""
import numpy as np, json

class MLP:
    def __init__(s, sizes, rng, out_gain=1.0):
        s.W, s.b = [], []
        for i in range(len(sizes) - 1):
            g = out_gain if i == len(sizes) - 2 else np.sqrt(2.0)
            # orthogonal-ish init via scaled normal (deterministic from rng)
            w = rng.standard_normal((sizes[i + 1], sizes[i])) * (g / np.sqrt(sizes[i]))
            s.W.append(w.astype(np.float64)); s.b.append(np.zeros(sizes[i + 1]))
    def forward(s, x):
        acts = [x]; h = x
        for i in range(len(s.W)):
            z = h @ s.W[i].T + s.b[i]
            h = np.tanh(z) if i < len(s.W) - 1 else z
            acts.append(h)
        return h, acts
    def backward(s, acts, gout):
        gW, gb = [None] * len(s.W), [None] * len(s.W)
        g = gout
        for i in reversed(range(len(s.W))):
            gW[i] = g.T @ acts[i]; gb[i] = g.sum(0)
            if i > 0:
                g = (g @ s.W[i]) * (1 - acts[i] ** 2)
        return gW, gb
    def params(s): return s.W + s.b

def softmax(z):
    z = z - z.max(-1, keepdims=True); e = np.exp(z); return e / e.sum(-1, keepdims=True)

class ActorCritic:
    def __init__(s, obs, n_act, seed=0, hidden=64):
        rng = np.random.default_rng(seed)
        s.pi = MLP([obs, hidden, hidden, n_act], rng, out_gain=0.01)
        s.vf = MLP([obs, hidden, hidden, 1], rng, out_gain=1.0)
        s.obs, s.n_act, s.hidden = obs, n_act, hidden
    def policy(s, x):
        logits, _ = s.pi.forward(np.atleast_2d(x)); return softmax(logits)
    def value(s, x):
        v, _ = s.vf.forward(np.atleast_2d(x)); return v[:, 0]
    def to_json(s):
        return json.dumps({'obs': s.obs, 'actions': s.n_act, 'hidden': s.hidden,
            'pi': [[w.tolist(), b.tolist()] for w, b in zip(s.pi.W, s.pi.b)],
            'vf': [[w.tolist(), b.tolist()] for w, b in zip(s.vf.W, s.vf.b)]})
    @staticmethod
    def from_json(t):
        d = json.loads(t); ac = ActorCritic(d['obs'], d['actions'], 0, d['hidden'])
        ac.pi.W = [np.array(w) for w, b in d['pi']]; ac.pi.b = [np.array(b) for w, b in d['pi']]
        ac.vf.W = [np.array(w) for w, b in d['vf']]; ac.vf.b = [np.array(b) for w, b in d['vf']]
        return ac

class A2C:
    def __init__(s, ac, lr=7e-4, gamma=0.99, ent_coef=0.01, vf_coef=0.5, max_grad_norm=0.5, alpha=0.99, eps=1e-5):
        s.ac, s.lr, s.gamma, s.ent, s.vfc, s.mgn, s.alpha, s.eps = ac, lr, gamma, ent_coef, vf_coef, max_grad_norm, alpha, eps
        s.sq = [np.zeros_like(p) for p in ac.pi.params() + ac.vf.params()]
    def update(s, obs, actions, returns):
        """obs [N,F], actions [N], returns [N] (n-step bootstrapped). One RMSprop step. Returns stats."""
        ac = s.ac; N = len(actions)
        logits, pa = ac.pi.forward(obs); v, va = ac.vf.forward(obs); v = v[:, 0]
        p = softmax(logits); logp = np.log(p + 1e-12)
        adv = returns - v
        onehot = np.zeros_like(p); onehot[np.arange(N), actions] = 1
        ent = -(p * logp).sum(1)
        # loss = -mean(adv * logp[a]) - ent_coef * mean(ent) + vf_coef * mean((R - v)^2)
        g_logits = (-(adv[:, None]) * (onehot - p)) / N
        # entropy gradient wrt logits: d(-H)/dz = p * (logp + H)
        g_logits += s.ent * (p * (logp + ent[:, None])) / N
        g_v = (s.vfc * 2.0 * (v - returns) / N)[:, None]
        gWp, gbp = ac.pi.backward(pa, g_logits); gWv, gbv = ac.vf.backward(va, g_v)
        grads = gWp + gbp + gWv + gbv
        params = ac.pi.W + ac.pi.b + ac.vf.W + ac.vf.b
        norm = np.sqrt(sum((g ** 2).sum() for g in grads))
        scale = min(1.0, s.mgn / (norm + 1e-6))
        for k, (prm, g) in enumerate(zip(params, grads)):
            g = g * scale
            s.sq[k] = s.alpha * s.sq[k] + (1 - s.alpha) * g * g
            prm -= s.lr * g / (np.sqrt(s.sq[k]) + s.eps)
        pl = -(adv * logp[np.arange(N), actions]).mean()
        return {'policy_loss': float(pl), 'value_loss': float(((returns - v) ** 2).mean()), 'entropy': float(ent.mean()), 'grad_norm': float(norm)}

def sample(p, u):
    """Inverse-CDF sampling with an external uniform u (shared with C#)."""
    c = 0.0
    for i, x in enumerate(p):
        c += x
        if u < c: return i
    return len(p) - 1
