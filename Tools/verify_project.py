"""Static checks for the Unity project (no Unity / C# compiler needed). python3 Tools/verify_project.py
- asmdef graph: names unique, references resolve, no cycles; editor code only in editor assemblies;
- noEngineReferences assemblies never mention UnityEngine/UnityEditor;
- every `using Kendo.X` and every Kendo type a file mentions is declared in the file's assembly or a referenced one
  (catches CS0246 / CS0012-style missing references);
- `Type.Member` on project types refers to a member name that exists in that type's declaration file;
- balanced (), [], {} after removing strings and comments; MonoBehaviour / ScriptableObject classes live in a file of
  the same name when they are the file's first type (Unity requirement for serialized components);
- duplicate top-level type names inside one namespace.
Exit code 1 on any error."""
import json, os, re, sys, glob, collections
from pygments.lexers import CSharpLexer
from pygments.token import Token

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
errors, warnings = [], []
lexer = CSharpLexer()

asmdefs = {}
for p in glob.glob(os.path.join(ROOT, 'Assets', '**', '*.asmdef'), recursive=True):
    d = json.load(open(p)); d['_dir'] = os.path.dirname(p); d['_path'] = p
    if d['name'] in asmdefs: errors.append(f"duplicate asmdef name {d['name']}")
    asmdefs[d['name']] = d
EXTERNAL = {'Unity.InputSystem', 'UnityEngine.UI', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime',
            'UnityEngine.TestRunner', 'UnityEditor.TestRunner', 'Unity.XR.Management', 'Unity.XR.OpenXR'}
for n, d in asmdefs.items():
    for r in d.get('references', []):
        if r not in asmdefs and r not in EXTERNAL: errors.append(f"{n}: unresolved reference {r}")
def cyc(n, stack):
    if n in stack: errors.append('asmdef cycle: ' + ' -> '.join(stack + [n])); return
    for r in asmdefs.get(n, {}).get('references', []):
        if r in asmdefs: cyc(r, stack + [n])
for n in asmdefs: cyc(n, [])

def owner(path):
    best = None
    for n, d in asmdefs.items():
        if path.startswith(d['_dir'] + os.sep) and (best is None or len(d['_dir']) > len(asmdefs[best]['_dir'])): best = n
    return best

def closure(n):
    out, todo = set(), [n]
    while todo:
        x = todo.pop()
        if x in out: continue
        out.add(x); todo += asmdefs.get(x, {}).get('references', [])
    return out

files = sorted(glob.glob(os.path.join(ROOT, 'Assets', '**', '*.cs'), recursive=True))
info = {}
TYPE_KW = {'class', 'struct', 'enum', 'interface'}
AMBIGUOUS = {'PlayerSettings'}  # Kendo.Campaign.PlayerSettings vs UnityEditor.PlayerSettings
for f in files:
    src = open(f, encoding='utf-8').read()
    toks = [(t, v) for t, v in lexer.get_tokens(src)]
    code = [(t, v) for t, v in toks if t not in Token.Comment and t not in Token.Literal.String and t is not Token.Text.Whitespace and not str(v).isspace()]
    ns = None; types = []; members = set()
    for i, (t, v) in enumerate(code):
        if v == 'namespace' and i + 1 < len(code):
            j = i + 1; parts = []
            while j < len(code) and code[j][1] not in ('{', ';'): parts.append(code[j][1]); j += 1
            ns = ''.join(parts)
        if v in TYPE_KW and i + 1 < len(code) and (i == 0 or code[i - 1][1] not in ('.',)):
            nm = code[i + 1][1]
            if re.match(r'^[A-Za-z_]\w*$', nm) and not (v == 'class' and i > 0 and code[i - 1][1] in ('new', 'where', ':')):
                types.append((v, nm))
        if t in Token.Name or t is Token.Name.Function or t is Token.Name.Class:
            members.add(v)
        if re.match(r'^[A-Za-z_]\w*$', str(v)): members.add(v)
    # bracket balance on code tokens (split punctuation tokens char-wise)
    stack = []
    pairs = {')': '(', ']': '[', '}': '{'}
    for t, v in code:
        if t in Token.Literal.String or t in Token.Comment: continue
        for ch in str(v) if t in Token.Punctuation or t in Token.Operator else '':
            if ch in '([{': stack.append(ch)
            elif ch in ')]}':
                if not stack or stack[-1] != pairs[ch]: errors.append(f"{os.path.relpath(f, ROOT)}: unbalanced '{ch}'"); stack = []; break
                stack.pop()
    if stack: errors.append(f"{os.path.relpath(f, ROOT)}: unclosed {''.join(stack)}")
    usings = re.findall(r'^\s*using\s+([\w\.]+)\s*;', src, re.M)
    info[f] = dict(src=src, code=code, ns=ns, types=types, members=members, usings=usings, asm=owner(f))

# declared types per assembly
decl = collections.defaultdict(dict)  # type -> {asm: file}
ns_types = collections.defaultdict(list)
for f, d in info.items():
    for kind, nm in d['types']:
        decl[nm].setdefault(d['asm'], f)
        if d['ns']: ns_types[(d['ns'], nm)].append(f)
for (ns, nm), fl in ns_types.items():
    if len(set(fl)) > 1:
        # partial classes are allowed only when declared partial
        if not all(re.search(r'partial\s+(class|struct)\s+' + nm, info[x]['src']) for x in fl):
            errors.append(f"type {ns}.{nm} declared in several files: {[os.path.relpath(x, ROOT) for x in fl]}")
asm_namespaces = collections.defaultdict(set)
for f, d in info.items():
    if d['ns']: asm_namespaces[d['asm']].add(d['ns'])

for f, d in info.items():
    rel = os.path.relpath(f, ROOT); asm = d['asm']; a = asmdefs.get(asm, {})
    refs = closure(asm) if asm else set()
    editor_asm = 'Editor' in a.get('includePlatforms', [])
    if a.get('noEngineReferences') and re.search(r'\bUnity(Engine|Editor)\b', re.sub(r'//.*|/\*.*?\*/|"[^"]*"', '', d['src'], flags=re.S)):
        errors.append(f"{rel}: {asm} has noEngineReferences but mentions UnityEngine/UnityEditor")
    if not editor_asm and re.search(r'^\s*using\s+UnityEditor', d['src'], re.M):
        errors.append(f"{rel}: runtime assembly {asm} imports UnityEditor")
    for u in d['usings']:
        if u.startswith('Kendo'):
            owners = [x for x, nss in asm_namespaces.items() if u in nss]
            if owners and not any(o in refs for o in owners): errors.append(f"{rel}: using {u} but {asm} does not reference {owners}")
            if not owners: errors.append(f"{rel}: using {u} – namespace not declared anywhere")
    # project types used but not referenced
    code = d['code']
    used = set()
    for i, (t, v) in enumerate(code):
        if not re.match(r'^[A-Z]\w*$', str(v)): continue
        prev = code[i - 1][1] if i else ''
        nxt = code[i + 1][1] if i + 1 < len(code) else ''
        if prev == '.' or nxt in ('(', '=', '=>') or prev in ('int', 'string', 'float', 'bool', 'double', 'var', 'const', 'void'): continue   # member access / call / declaration – not a type use
        used.add(v)
    for nm in used:
        if nm in decl and not any(x in refs for x in decl[nm]):
            # ignore if the name is also a member/variable declared locally
            if re.search(r'\b(class|struct|enum|interface)\s+' + nm + r'\b', d['src']): continue
            if re.search(r'\b(int|string|float|bool|double|var|const|readonly|static)\s+(\w+\s+)?' + nm + r'\s*[=;]', d['src']): continue
            if re.search(r'"' + nm + r'"', d['src']) and not re.search(r'[\s(<,]' + nm + r'[\s>\[]', re.sub(r'"[^"]*"', '', d['src'])): continue
            errors.append(f"{rel}: uses {nm} (declared in {list(decl[nm])}) without an assembly reference")
    # Type.Member checks for project static/enum members
    code = d['code']
    for i in range(len(code) - 2):
        t0, v0 = code[i]; _, v1 = code[i + 1]; t2, v2 = code[i + 2]
        if v0 in AMBIGUOUS and re.search(r'^\s*using\s+UnityEditor', d['src'], re.M): continue
        if v1 == '.' and v0 in decl and re.match(r'^[A-Za-z_]\w*$', str(v2)) and (i == 0 or code[i - 1][1] not in ('.',)):
            if any(x in refs for x in decl[v0]):
                defining = [info[ff] for asmname, ff in decl[v0].items() if asmname in refs]
                if defining and not any(v2 in dd['members'] for dd in defining):
                    errors.append(f"{rel}: {v0}.{v2} – no member '{v2}' in {v0}'s file")
    # Unity serialisation rule
    m = re.search(r'class\s+(\w+)\s*:\s*(MonoBehaviour|ScriptableObject)\b', d['src'])
    if m and d['types'] and d['types'][0][1] == m.group(1) and os.path.splitext(os.path.basename(f))[0] != m.group(1):
        errors.append(f"{rel}: {m.group(1)} ({m.group(2)}) must live in {m.group(1)}.cs")
    for mm in re.finditer(r'class\s+(\w+)\s*:\s*(MonoBehaviour|ScriptableObject)\b', d['src']):
        if mm.group(1) != os.path.splitext(os.path.basename(f))[0]:
            warnings.append(f"{rel}: {mm.group(1)} is a {mm.group(2)} in a file of another name (fine for AddComponent at runtime; not assignable in the inspector)")

print(f"C# files: {len(files)}, asmdefs: {len(asmdefs)}")
for w in warnings: print('warning:', w)
for e in errors: print('ERROR:', e)
print('OK' if not errors else f'{len(errors)} error(s)')
sys.exit(1 if errors else 0)
