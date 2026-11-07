"""Minimal binary FBX inspector (no SDK). Usage: python3 -I fbxinspect.py file.fbx [json_out]"""
import sys, struct, zlib, json, math, os
import numpy as np

class Node:
    __slots__ = ("name", "props", "children")
    def __init__(s, n, p, c): s.name, s.props, s.children = n, p, c
    def find(s, name):
        for c in s.children:
            if c.name == name: return c
    def findall(s, name): return [c for c in s.children if c.name == name]

ARR = {b'f': ('<f', 4), b'd': ('<d', 8), b'l': ('<q', 8), b'i': ('<i', 4), b'b': ('<B', 1)}
NPT = {b'f': '<f4', b'd': '<f8', b'l': '<i8', b'i': '<i4', b'b': 'u1'}

def read_prop(d, o):
    t = d[o:o+1]; o += 1
    if t == b'Y': return struct.unpack_from('<h', d, o)[0], o+2
    if t == b'C': return d[o] != 0, o+1
    if t == b'I': return struct.unpack_from('<i', d, o)[0], o+4
    if t == b'F': return struct.unpack_from('<f', d, o)[0], o+4
    if t == b'D': return struct.unpack_from('<d', d, o)[0], o+8
    if t == b'L': return struct.unpack_from('<q', d, o)[0], o+8
    if t in (b'S', b'R'):
        n = struct.unpack_from('<I', d, o)[0]; o += 4
        raw = d[o:o+n]; o += n
        if t == b'S': return raw.decode('utf-8', 'replace'), o
        return ('RAW', n), o
    if t in ARR:
        ln, enc, cl = struct.unpack_from('<III', d, o); o += 12
        raw = d[o:o+cl]; o += cl
        if enc == 1: raw = zlib.decompress(raw)
        return np.frombuffer(raw, dtype=NPT[t], count=ln), o
    raise ValueError(f"bad prop type {t} at {o}")

def read_node(d, o, v64):
    if v64:
        end, np_, pl = struct.unpack_from('<QQQ', d, o); o += 24
    else:
        end, np_, pl = struct.unpack_from('<III', d, o); o += 12
    nl = d[o]; o += 1
    if end == 0: return None, o
    name = d[o:o+nl].decode('ascii', 'replace'); o += nl
    props = []
    for _ in range(np_):
        p, o = read_prop(d, o); props.append(p)
    children = []
    sentinel = 25 if v64 else 13
    while o < end - sentinel or (o < end and end - o > sentinel):
        c, o = read_node(d, o, v64)
        if c is None: break
        children.append(c)
    return Node(name, props, children), end

def parse(path):
    d = open(path, 'rb').read()
    assert d[:20] == b'Kaydara FBX Binary  '
    ver = struct.unpack_from('<I', d, 23)[0]
    v64 = ver >= 7500
    o = 27; top = []
    while o < len(d):
        n, o = read_node(d, o, v64)
        if n is None: break
        top.append(n)
    return ver, Node('root', [], top)

def p70(node):
    out = {}
    p = node.find('Properties70') if node else None
    if not p: return out
    for P in p.findall('P'):
        out[P.props[0]] = P.props[4:] if len(P.props) > 4 else []
    return out

def euler_mat(r, order=0):
    rx, ry, rz = [math.radians(a) for a in r]
    X = np.array([[1,0,0],[0,math.cos(rx),-math.sin(rx)],[0,math.sin(rx),math.cos(rx)]])
    Y = np.array([[math.cos(ry),0,math.sin(ry)],[0,1,0],[-math.sin(ry),0,math.cos(ry)]])
    Z = np.array([[math.cos(rz),-math.sin(rz),0],[math.sin(rz),math.cos(rz),0],[0,0,1]])
    return Z @ Y @ X  # FBX default XYZ order

def local_matrix(props):
    t = props.get('Lcl Translation', [0,0,0]); r = props.get('Lcl Rotation', [0,0,0])
    s = props.get('Lcl Scaling', [1,1,1]); pre = props.get('PreRotation', [0,0,0])
    rp = props.get('RotationPivot', [0,0,0]); sp = props.get('ScalingPivot', [0,0,0])
    M = np.eye(4)
    R = euler_mat(pre) @ euler_mat(r)
    M[:3,:3] = R @ np.diag([float(x) for x in s])
    M[:3,3] = np.array(t, float) + np.array(rp, float) - (M[:3,:3] @ np.array(rp, float)) * 0  # pivots approx ignored
    return M

def inspect(path):
    ver, root = parse(path)
    info = {'file': os.path.basename(path), 'size_mb': round(os.path.getsize(path)/1e6, 2), 'fbx_version': ver}
    hdr = root.find('FBXHeaderExtension')
    if hdr:
        c = hdr.find('Creator'); info['creator'] = c.props[0] if c else None
        si = hdr.find('SceneInfo')
        if si:
            sp = p70(si)
            info['original_app'] = (sp.get('Original|ApplicationName', [''])[0] + ' ' + str(sp.get('Original|ApplicationVersion', [''])[0])).strip()
            info['original_file'] = sp.get('Original|FileName', [''])[0]
    gs = p70(root.find('GlobalSettings'))
    info['global'] = {k: (gs[k][0] if gs.get(k) else None) for k in
                      ['UpAxis','UpAxisSign','FrontAxis','FrontAxisSign','CoordAxis','UnitScaleFactor','OriginalUnitScaleFactor','TimeMode','CustomFrameRate','TimeSpanStop']}
    objs = root.find('Objects')
    byid = {}
    models, geoms, mats, texs, vids, deformers, stacks, layers, curvenodes, curves, poses = {}, {}, {}, {}, {}, {}, {}, {}, {}, {}, []
    for n in objs.children:
        oid = n.props[0] if n.props else None
        nm = n.props[1].split('\x00')[0] if len(n.props) > 1 and isinstance(n.props[1], str) else ''
        sub = n.props[2] if len(n.props) > 2 else ''
        byid[oid] = (n.name, nm, sub, n)
        if n.name == 'Model': models[oid] = (nm, sub, n)
        elif n.name == 'Geometry': geoms[oid] = (nm, sub, n)
        elif n.name == 'Material': mats[oid] = (nm, n)
        elif n.name == 'Texture': texs[oid] = (nm, n)
        elif n.name == 'Video': vids[oid] = (nm, n)
        elif n.name == 'Deformer': deformers[oid] = (nm, sub, n)
        elif n.name == 'AnimationStack': stacks[oid] = (nm, n)
        elif n.name == 'AnimationLayer': layers[oid] = (nm, n)
        elif n.name == 'AnimationCurveNode': curvenodes[oid] = (nm, n)
        elif n.name == 'AnimationCurve': curves[oid] = n
        elif n.name == 'Pose': poses.append((nm, sub))
    conns = root.find('Connections')
    parent_of, children_of, oo, op = {}, {}, [], []
    for c in conns.findall('C'):
        kind, src, dst = c.props[0], c.props[1], c.props[2]
        prop = c.props[3] if len(c.props) > 3 else None
        children_of.setdefault(dst, []).append((src, prop))
        if kind == 'OO': parent_of.setdefault(src, []).append(dst)
        op.append((kind, src, dst, prop))
    # model hierarchy + world matrices
    mprops = {mid: p70(m[2]) for mid, m in models.items()}
    def mparent(mid):
        for d in parent_of.get(mid, []):
            if d in models: return d
        return 0
    world = {}
    def wmat(mid):
        if mid in world: return world[mid]
        p = mparent(mid)
        M = local_matrix(mprops[mid])
        W = (wmat(p) @ M) if p else M
        world[mid] = W; return W
    # geometry stats
    gstats = []
    allpts = []
    for gid, (gn, gsub, g) in geoms.items():
        if gsub != 'Mesh':
            gstats.append({'name': gn, 'type': gsub}); continue
        V = g.find('Vertices'); PI = g.find('PolygonVertexIndex')
        verts = V.props[0].reshape(-1, 3).astype(float) if V else np.zeros((0, 3))
        pvi = PI.props[0] if PI else np.zeros(0, int)
        npoly = int((pvi < 0).sum())
        # triangle count
        tris = 0; cnt = 0
        for x in pvi:
            cnt += 1
            if x < 0: tris += max(cnt - 2, 0); cnt = 0
        uvl = [ (l.find('Name').props[0] if l.find('Name') else '') for l in g.findall('LayerElementUV')]
        st = {'name': gn, 'verts': len(verts), 'polys': npoly, 'tris': tris,
              'normals': bool(g.findall('LayerElementNormal')), 'tangents': bool(g.findall('LayerElementTangent')),
              'uv_sets': uvl, 'vertex_colors': bool(g.findall('LayerElementColor')),
              'material_slots': None}
        lm = g.find('LayerElementMaterial')
        if lm and lm.find('Materials'):
            st['material_slots'] = int(len(np.unique(lm.find('Materials').props[0])))
        bs = [c for c, _ in children_of.get(gid, []) if c in deformers and deformers[c][1] == 'Skin']
        st['skinned'] = bool(bs)
        owners = [d for d in parent_of.get(gid, []) if d in models]
        if owners and len(verts):
            W = wmat(owners[0])
            wp = (W[:3, :3] @ verts.T).T + W[:3, 3]
            allpts.append(np.vstack([wp.min(0), wp.max(0)]))
            st['model'] = models[owners[0]][0]
        if len(verts):
            st['local_bounds_min'] = verts.min(0).round(3).tolist(); st['local_bounds_max'] = verts.max(0).round(3).tolist()
        gstats.append(st)
    info['meshes'] = gstats
    info['totals'] = {'mesh_count': sum(1 for g in gstats if g.get('type') is None),
                      'verts': int(sum(g.get('verts', 0) for g in gstats)), 'tris': int(sum(g.get('tris', 0) for g in gstats))}
    if allpts:
        a = np.vstack(allpts); mn, mx = a.min(0), a.max(0)
        usf = info['global']['UnitScaleFactor'] or 1.0
        info['world_bounds_fbx_units'] = {'min': mn.round(3).tolist(), 'max': mx.round(3).tolist(), 'size': (mx - mn).round(3).tolist()}
        info['world_size_meters_est'] = ((mx - mn) * usf / 100.0).round(3).tolist()
    # model types
    types = {}
    for mid, (nm, sub, n) in models.items(): types[sub] = types.get(sub, 0) + 1
    info['model_types'] = types
    # top-level hierarchy
    roots = [mid for mid in models if mparent(mid) == 0]
    def tree(mid, depth, out, maxd=3):
        nm, sub, _ = models[mid]
        kids = [c for c, _ in children_of.get(mid, []) if c in models]
        out.append('  ' * depth + f"{nm} [{sub}]" + (f" ({len(kids)} children)" if kids and depth >= maxd else ''))
        if depth < maxd:
            for k in kids[:25]: tree(k, depth + 1, out, maxd)
            if len(kids) > 25: out.append('  ' * (depth + 1) + f"... +{len(kids)-25} more")
    lines = []
    for r in roots[:40]: tree(r, 0, lines)
    if len(roots) > 40: lines.append(f"... +{len(roots)-40} more roots")
    info['hierarchy'] = lines[:200]
    info['root_transforms'] = []
    for r in roots[:15]:
        p = mprops[r]
        info['root_transforms'].append({'name': models[r][0], 'T': [round(float(x), 3) for x in p.get('Lcl Translation', [0,0,0])],
                                        'R': [round(float(x), 2) for x in p.get('Lcl Rotation', [0,0,0])],
                                        'S': [round(float(x), 4) for x in p.get('Lcl Scaling', [1,1,1])]})
    # skeleton
    bones = [nm for mid, (nm, sub, n) in models.items() if sub == 'LimbNode']
    info['skeleton'] = {'bone_count': len(bones), 'bones': bones[:120]}
    info['skin'] = {'skins': sum(1 for d in deformers.values() if d[1] == 'Skin'),
                    'clusters': sum(1 for d in deformers.values() if d[1] == 'Cluster'),
                    'blendshapes': sum(1 for d in deformers.values() if d[1] == 'BlendShape')}
    info['poses'] = poses
    # materials
    ml = []
    for mid, (nm, n) in mats.items():
        pp = p70(n)
        sm = n.find('ShadingModel')
        tex_links = []
        for c, prop in children_of.get(mid, []):
            if c in texs:
                tp = texs[c][1]
                fn = tp.find('RelativeFilename') or tp.find('FileName')
                tex_links.append({'slot': prop, 'texture': texs[c][0], 'file': fn.props[0] if fn else None})
        def col(k):
            v = pp.get(k); return [round(float(x), 3) for x in v[:3]] if v else None
        ml.append({'name': nm, 'shading': sm.props[0] if sm else None, 'diffuse': col('DiffuseColor'),
                   'emissive': col('EmissiveColor'), 'opacity': (round(float(pp['Opacity'][0]), 3) if pp.get('Opacity') else None),
                   'textures': tex_links})
    info['materials'] = ml
    tl = []
    for tid, (nm, n) in texs.items():
        fn = n.find('FileName'); rf = n.find('RelativeFilename')
        tl.append({'name': nm, 'file': fn.props[0] if fn else None, 'relative': rf.props[0] if rf else None})
    info['textures'] = tl
    vl = []
    for vid, (nm, n) in vids.items():
        c = n.find('Content'); fn = n.find('RelativeFilename') or n.find('Filename')
        emb = c.props[0][1] if c and c.props and isinstance(c.props[0], tuple) else 0
        vl.append({'name': nm, 'file': fn.props[0] if fn else None, 'embedded_bytes': emb})
    info['videos'] = vl
    # animation
    tick = 46186158000
    al = []
    for sid, (nm, n) in stacks.items():
        pp = p70(n)
        ls, le = pp.get('LocalStart', [0])[0], pp.get('LocalStop', [0])[0]
        lyr = [c for c, _ in children_of.get(sid, []) if c in layers]
        ncn = 0; keys = 0; tmax = 0; animated = set()
        for l in lyr:
            for cn, _ in children_of.get(l, []):
                if cn in curvenodes:
                    ncn += 1
                    for d in parent_of.get(cn, []):
                        if d in models: animated.add(models[d][0])
                    for cv, _ in children_of.get(cn, []):
                        if cv in curves:
                            kt = curves[cv].find('KeyTime')
                            if kt is not None and len(kt.props[0]):
                                keys += len(kt.props[0]); tmax = max(tmax, int(kt.props[0].max()))
        al.append({'name': nm, 'start_s': round(ls / tick, 3), 'stop_s': round(le / tick, 3), 'duration_s': round((le - ls) / tick, 3),
                   'layers': len(lyr), 'curve_nodes': ncn, 'total_keys': keys, 'last_key_s': round(tmax / tick, 3),
                   'animated_nodes': len(animated), 'animated_sample': sorted(animated)[:12]})
    info['animations'] = al
    return info

if __name__ == '__main__':
    r = inspect(sys.argv[1])
    s = json.dumps(r, indent=1, default=lambda o: o.tolist() if hasattr(o, 'tolist') else str(o))
    if len(sys.argv) > 2: open(sys.argv[2], 'w').write(s)
    else: print(s)
