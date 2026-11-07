"""World-space mesh loader for the supplied binary FBX files (no SDK).
Returns per-model vertices/triangles in Unity coordinates: Unity imports a right-handed Y-up FBX by
negating X; file scale (cm) * Blender object scale 100 = metres, times the ModelImporter global scale."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from fbxinspect import parse, p70, local_matrix

def load(path, global_scale=1.0, want_tris=True):
    ver, root = parse(path)
    objs = root.find('Objects')
    models, geoms = {}, {}
    for n in objs.children:
        if n.name == 'Model': models[n.props[0]] = (n.props[1].split('\x00')[0], n.props[2], n)
        elif n.name == 'Geometry' and n.props[2] == 'Mesh': geoms[n.props[0]] = n
    parent, gowner = {}, {}
    for c in root.find('Connections').findall('C'):
        if c.props[0] != 'OO': continue
        s, d = c.props[1], c.props[2]
        if s in models and d in models: parent[s] = d
        if s in geoms and d in models: gowner.setdefault(s, d)
    props = {m: p70(v[2]) for m, v in models.items()}
    W = {}
    def wm(m):
        if m in W: return W[m]
        M = local_matrix(props[m]); p = parent.get(m)
        W[m] = (wm(p) @ M) if p else M
        return W[m]
    def path_of(m):
        out = []
        while m: out.append(models[m][0]); m = parent.get(m)
        return '/'.join(reversed(out))
    out = []
    for gid, g in geoms.items():
        mid = gowner.get(gid)
        if mid is None: continue
        V = g.find('Vertices').props[0].reshape(-1, 3).astype(float)
        M = wm(mid)
        P = (M[:3, :3] @ V.T).T + M[:3, 3]
        P = P * 0.01 * global_scale
        P[:, 0] *= -1
        tris = None
        if want_tris:
            pvi = g.find('PolygonVertexIndex').props[0].astype(np.int64)
            ends = np.where(pvi < 0)[0]
            idx = pvi.copy(); idx[ends] = -idx[ends] - 1
            starts = np.concatenate([[0], ends[:-1] + 1])
            t = []
            for s, e in zip(starts, ends):
                for k in range(s + 1, e): t.append((idx[s], idx[k], idx[k + 1]))
            tris = np.array(t, dtype=np.int64).reshape(-1, 3)
        out.append({'name': models[mid][0], 'path': path_of(mid), 'verts': P, 'tris': tris})
    return out
