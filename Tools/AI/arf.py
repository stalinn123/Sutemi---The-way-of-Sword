"""ARF-style online classifier (Gomes et al. 2017): Poisson(6) online bagging of Hoeffding trees that split on random
feature subsets, accuracy-weighted voting and drift-triggered tree replacement. Mirror of Kendo.AI.AdaptiveRandomForest.
Simplification vs. River's ARFClassifier: drift is detected with DDM (warning 2σ / drift 3σ) instead of ADWIN, and leaves
predict the majority class. The River model itself is used by train_sb3.py when River is installed."""
import math
from krandom import KRandom

def entropy(counts):
    t = sum(counts)
    if t <= 0: return 0.0
    h = 0.0
    for c in counts:
        if c > 0: p = c / t; h -= p * math.log2(p)
    return h

def phi(z): return 0.5 * (1.0 + math.erf(z / math.sqrt(2.0)))

class Gauss:
    __slots__ = ('n', 'mean', 'm2', 'lo', 'hi')
    def __init__(s): s.n = 0.0; s.mean = 0.0; s.m2 = 0.0; s.lo = 1e30; s.hi = -1e30
    def add(s, x, w):
        s.n += w; d = x - s.mean; s.mean += w * d / s.n; s.m2 += w * d * (x - s.mean)
        if x < s.lo: s.lo = x
        if x > s.hi: s.hi = x
    def cdf(s, x):
        if s.n <= 0: return 0.0
        sd = math.sqrt(max(0.0, s.m2) / s.n) if s.n > 1 else 0.0
        if sd < 1e-6: return 1.0 if x >= s.mean else 0.0
        return phi((x - s.mean) / sd)

class Node:
    def __init__(s, k, feats, counts=None, depth=0):
        s.k = k; s.counts = list(counts) if counts else [0.0] * k; s.feats = feats; s.depth = depth
        s.stats = {f: [Gauss() for _ in range(k)] for f in feats}
        s.seen_since = 0.0; s.feature = -1; s.thr = 0.0; s.left = s.right = None
    def leaf(s): return s.feature < 0

class HoeffdingTree:
    def __init__(s, n_feat, k, rng, m, grace=50, delta=0.01, tau=0.05, max_depth=12, n_splits=10):
        s.n_feat, s.k, s.rng, s.m, s.grace, s.delta, s.tau, s.max_depth, s.n_splits = n_feat, k, rng, m, grace, delta, tau, max_depth, n_splits
        s.root = Node(k, s.subset())
    def subset(s):
        idx = list(range(s.n_feat))
        for i in range(s.m):  # partial Fisher-Yates
            j = i + s.rng.int(s.n_feat - i); idx[i], idx[j] = idx[j], idx[i]
        return sorted(idx[:s.m])
    def sort(s, x):
        n = s.root
        while not n.leaf(): n = n.left if x[n.feature] <= n.thr else n.right
        return n
    def predict(s, x):
        c = s.sort(x).counts
        return max(range(s.k), key=lambda i: (c[i], -i)) if sum(c) > 0 else 0
    def learn(s, x, y, w):
        n = s.sort(x)
        n.counts[y] += w
        for f in n.feats: n.stats[f][y].add(x[f], w)
        n.seen_since += w
        if n.seen_since >= s.grace and n.depth < s.max_depth:
            n.seen_since = 0.0; s.try_split(n)
    def try_split(s, n):
        tot = sum(n.counts)
        if sum(1 for c in n.counts if c > 0) < 2: return
        h0 = entropy(n.counts); best = (0.0, -1, 0.0, None, None); second = 0.0
        for f in n.feats:
            st = n.stats[f]
            lo = min(g.lo for g in st if g.n > 0); hi = max(g.hi for g in st if g.n > 0)
            if hi - lo < 1e-9: continue
            for j in range(1, s.n_splits + 1):
                thr = lo + (hi - lo) * j / (s.n_splits + 1)
                L = [n.counts[c] * st[c].cdf(thr) for c in range(s.k)]
                Rr = [n.counts[c] - L[c] for c in range(s.k)]
                tl, tr = sum(L), sum(Rr)
                if tl < 1e-9 or tr < 1e-9: continue
                gain = h0 - (tl / tot) * entropy(L) - (tr / tot) * entropy(Rr)
                if gain > best[0]: second = max(second, best[0]); best = (gain, f, thr, L, Rr)
                elif gain > second: second = gain
        if best[1] < 0: return
        R = math.log2(s.k)
        eps = math.sqrt(R * R * math.log(1.0 / s.delta) / (2.0 * tot))
        if best[0] - second > eps or eps < s.tau:
            n.feature, n.thr = best[1], best[2]
            n.left = Node(s.k, s.subset(), best[3], n.depth + 1); n.right = Node(s.k, s.subset(), best[4], n.depth + 1)
            n.stats = None

class DDM:
    def __init__(s, min_n=30): s.min_n = min_n; s.reset()
    def reset(s): s.n = 0; s.p = 1.0; s.pmin = 1e9; s.smin = 1e9; s.psmin = 1e9
    def update(s, err):
        s.n += 1; s.p += (err - s.p) / s.n
        sd = math.sqrt(s.p * (1 - s.p) / s.n)
        if s.n < s.min_n: return 0
        if s.p + sd < s.psmin: s.pmin, s.smin, s.psmin = s.p, sd, s.p + sd
        if s.p + sd >= s.pmin + 3 * s.smin: return 2
        if s.p + sd >= s.pmin + 2 * s.smin: return 1
        return 0

class Member:
    def __init__(s, forest):
        s.tree = forest.new_tree(); s.bg = None; s.det = DDM(); s.correct = 0; s.seen = 0

class AdaptiveRandomForest:
    def __init__(s, n_feat, k, seed=1, n_models=10, lam=6.0, grace=50):
        s.n_feat, s.k, s.lam, s.grace = n_feat, k, lam, grace
        s.m = max(1, int(round(math.sqrt(n_feat)))) + 1
        s.rng = KRandom(seed)
        s.members = [Member(s) for _ in range(n_models)]
        s.drifts = 0; s.samples = 0
    def new_tree(s): return HoeffdingTree(s.n_feat, s.k, s.rng, s.m, grace=s.grace)
    def proba(s, x):
        v = [0.0] * s.k; tot = 0.0
        for mb in s.members:
            w = (mb.correct / mb.seen) if mb.seen > 0 else 1.0
            v[mb.tree.predict(x)] += w; tot += w
        return [a / tot for a in v] if tot > 0 else [1.0 / s.k] * s.k
    def predict(s, x):
        p = s.proba(x); return max(range(s.k), key=lambda i: (p[i], -i))
    def learn(s, x, y):
        s.samples += 1
        for mb in s.members:
            ok = mb.tree.predict(x) == y
            mb.seen += 1; mb.correct += 1 if ok else 0
            lvl = mb.det.update(0.0 if ok else 1.0)
            if lvl >= 1 and mb.bg is None: mb.bg = s.new_tree()
            if lvl == 2:
                mb.tree = mb.bg if mb.bg is not None else s.new_tree(); mb.bg = None
                mb.det.reset(); mb.correct = 0; mb.seen = 0; s.drifts += 1
            w = s.rng.poisson(s.lam)
            if w > 0:
                mb.tree.learn(x, y, w)
                if mb.bg is not None: mb.bg.learn(x, y, w)
