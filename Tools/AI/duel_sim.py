"""Abstract kendo duel used to train and evaluate the opponent agents. Exact mirror of Kendo.AI.DuelSim (C#).
Timings and reaches equal the game's (Kendo.AI.KendoTimings); hits are judged by the shared rules engine mirror."""
import math, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'Rules'))
sys.path.insert(0, os.path.dirname(__file__))
import rules_ref as R
from krandom import KRandom

ACTIONS = ['Wait', 'StepForward', 'StepBack', 'StepLeft', 'StepRight', 'Men', 'Kote', 'Do', 'Tsuki', 'BlockHigh', 'BlockDo']
A = {n: i for i, n in enumerate(ACTIONS)}
N_ACTIONS = len(ACTIONS)
PHASES = ['Ready', 'Stepping', 'Striking', 'Recovering', 'Blocking', 'Stunned']
PH = {n: i for i, n in enumerate(PHASES)}
STRIKE_TARGET = {A['Men']: R.T['Men'], A['Kote']: R.T['KoteRight'], A['Do']: R.T['DoRight'], A['Tsuki']: R.T['Tsuki']}
OBS_SIZE = 26

# ---- KendoTimings (shared with the game) ----
STEP_S, SETTLE_S = 0.30, 0.06
STEP_FWD, STEP_BACK, STEP_SIDE = 0.30, 0.25, 0.25
RAISE = {A['Men']: 0.18, A['Kote']: 0.12, A['Do']: 0.18, A['Tsuki']: 0.10}
STRIKE = {A['Men']: 0.12, A['Kote']: 0.10, A['Do']: 0.14, A['Tsuki']: 0.14}
REACH = {A['Men']: (1.05, 1.80), A['Kote']: (1.00, 1.70), A['Do']: (0.95, 1.65), A['Tsuki']: (1.05, 1.75)}
LUNGE = 0.40
FOLLOW_S, RETURN_S = 0.40, 0.35
BLOCK_RAISE, BLOCK_HOLD = 0.08, 0.50
STUN_S, COUNTER_WINDOW = 0.30, 0.60
DECISION_DT, SUB_DT = 0.10, 0.05
BLOCK_COVERS = {A['BlockHigh']: {A['Men']: 1.0, A['Tsuki']: 1.0, A['Kote']: 0.5}, A['BlockDo']: {A['Do']: 1.0, A['Kote']: 0.5}}

class Technique:
    """Per-fighter technique quality (design values; randomised in training)."""
    def __init__(s, speed=8.5, hasuji_sigma=12.0, kiai_prob=0.85, timing_sigma=0.08, tilt_sigma=6.0, zanshin_prob=0.8, edge_prob=0.97, drive=1.0, guard=0.3):
        s.speed, s.hasuji_sigma, s.kiai_prob, s.timing_sigma = speed, hasuji_sigma, kiai_prob, timing_sigma
        s.tilt_sigma, s.zanshin_prob, s.edge_prob, s.drive = tilt_sigma, zanshin_prob, edge_prob, drive
        s.guard = guard

class EnvParams:
    def __init__(s, court_half=4.5, start_distance=2.8, tracking_noise=0.0, latency_steps=(0, 0), speed_scale=(1.0, 1.0),
                 move_scale=(1.0, 1.0), match_seconds=120.0, points_to_win=2, allow_tsuki=True, profile='Intermediate'):
        s.court_half, s.start_distance, s.tracking_noise = court_half, start_distance, tracking_noise
        s.latency_steps, s.speed_scale, s.move_scale = list(latency_steps), list(speed_scale), list(move_scale)
        s.match_seconds, s.points_to_win, s.allow_tsuki, s.profile = match_seconds, points_to_win, allow_tsuki, profile

class Fighter:
    def __init__(s, x, facing, tech):
        s.x, s.y, s.facing, s.tech = x, 0.0, facing, tech
        s.phase, s.t, s.dur = PH['Ready'], 0.0, 0.0
        s.action = A['Wait']; s.step_dx = 0.0; s.step_dy = 0.0
        s.strike_start_dist = 0.0; s.strike_opp = 0; s.impact_done = False
        s.block_kind = 0; s.counter_until = -1.0; s.last_action_time = -9.0
        s.impact_time = -9.0; s.pending = None

class DuelSim:
    def __init__(s, seed, params=None, tech=(None, None)):
        s.rng = KRandom(seed); s.p = params or EnvParams()
        s.rules = R.RuleConfig(allowTsuki=s.p.allow_tsuki, pointsToWin=s.p.points_to_win, matchSeconds=s.p.match_seconds, enchoSeconds=30.0)
        s.profile = {'Beginner': R.beginner, 'Intermediate': R.intermediate, 'Shiai': R.shiai}[s.p.profile]()
        s.tech = [tech[0] or Technique(), tech[1] or Technique()]
        s.match = R.Match(s.rules); s.time = 0.0
        s.history = [[], []]  # observation history for latency
        s.last_eval = [None, None]; s.last_decision = [None, None]; s.shaped = [0.0, 0.0]
        s.reset_positions(); s.match.hajime()

    def reset_positions(s):
        d = s.p.start_distance
        s.f = [Fighter(-d / 2, 1.0, s.tech[0]), Fighter(d / 2, -1.0, s.tech[1])]
        s.history = [[], []]

    def distance(s):
        a, b = s.f
        return math.hypot(b.x - a.x, b.y - a.y)

    # ---------------------------------------------------------------- observations
    def raw_obs(s, i):
        me, op = s.f[i], s.f[1 - i]
        o = [0.0] * OBS_SIZE
        o[0] = s.distance() / 3.0
        o[1] = (me.x * me.facing) / s.p.court_half          # how far I am from my own back line (-1 = at my end)
        o[2] = (op.x * me.facing) / s.p.court_half
        o[3] = (op.y - me.y) / 1.0
        o[4 + me.phase] = 1.0
        o[10 + op.phase] = 1.0
        o[16] = min(1.0, me.t / me.dur) if me.dur > 0 else 0.0
        o[17] = min(1.0, op.t / op.dur) if op.dur > 0 else 0.0
        if op.phase == PH['Striking']:
            k = {A['Men']: 18, A['Kote']: 19, A['Do']: 20, A['Tsuki']: 21}.get(op.action)
            if k: o[k] = 1.0
        o[22] = 1.0 if s.time < me.counter_until else 0.0
        o[23] = (s.match.points[i] - s.match.points[1 - i]) / 2.0
        o[24] = s.match.time_remaining / max(1.0, s.rules.matchSeconds)
        o[25] = 1.0 if op.phase == PH['Blocking'] else 0.0
        return o

    def observe(s, i):
        """Observation with tracking noise and input latency (domain randomisation)."""
        o = s.raw_obs(i)
        if s.p.tracking_noise > 0:
            o[0] += s.rng.gauss() * s.p.tracking_noise / 3.0
            o[3] += s.rng.gauss() * s.p.tracking_noise
        h = s.history[i]; h.append(o)
        lat = s.p.latency_steps[i]
        if len(h) > lat + 1: del h[0]
        return h[0]

    # ---------------------------------------------------------------- actions
    def can_act(s, f): return f.phase in (PH['Ready'], PH['Blocking'])

    def apply(s, i, a):
        f = s.f[i]
        if not s.can_act(f):
            return
        if a == A['Wait']:
            if f.phase == PH['Blocking'] and f.t >= BLOCK_HOLD: f.phase = PH['Ready']; f.dur = 0.0
            return
        f.action = a; f.t = 0.0; f.last_action_time = s.time
        ms = s.p.move_scale[i]
        if a in (A['StepForward'], A['StepBack'], A['StepLeft'], A['StepRight']):
            f.phase = PH['Stepping']; f.dur = (STEP_S + SETTLE_S) / ms
            f.step_dx = {A['StepForward']: STEP_FWD, A['StepBack']: -STEP_BACK}.get(a, 0.0) * f.facing
            f.step_dy = {A['StepLeft']: STEP_SIDE, A['StepRight']: -STEP_SIDE}.get(a, 0.0) * f.facing
        elif a in STRIKE_TARGET:
            sp = s.p.speed_scale[i]
            f.phase = PH['Striking']; f.dur = (RAISE[a] + STRIKE[a]) / sp + FOLLOW_S
            f.impact_at = (RAISE[a] + STRIKE[a]) / sp
            f.strike_start_dist = s.distance(); f.impact_done = False
            f.opp_at_start = s.opportunity(i)
        else:
            f.phase = PH['Blocking']; f.block_kind = a; f.dur = BLOCK_HOLD

    def opportunity(s, i):
        me, op = s.f[i], s.f[1 - i]
        if s.time < me.counter_until: return R.OPP['AfterBlock']
        if op.phase == PH['Striking'] and op.t < 0.12: return R.OPP['Debana']
        if op.phase == PH['Stepping'] and op.step_dx * op.facing > 0 and op.t < 0.15: return R.OPP['Debana']
        if op.phase == PH['Recovering'] or (op.phase == PH['Striking'] and op.impact_done): return R.OPP['AfterOpponentStrike']
        if op.phase == PH['Stepping'] and op.step_dx * op.facing < 0: return R.OPP['OpponentRetreating']
        if op.phase == PH['Stunned']: return R.OPP['AfterBlock']
        if op.phase == PH['Blocking']: return R.OPP['IntoReadyKamae']
        return R.OPP['IntoReadyKamae'] if s.distance() < 2.3 else R.OPP['OpponentStatic']

    # ---------------------------------------------------------------- physics
    def sub_step(s, dt):
        for i in (0, 1):
            f = s.f[i]
            f.t += dt
            if f.phase == PH['Stepping']:
                frac = dt / f.dur
                f.x += f.step_dx * frac; f.y += f.step_dy * frac
                if f.t >= f.dur: f.phase = PH['Ready']; f.dur = 0.0
            elif f.phase == PH['Striking']:
                if not f.impact_done:
                    lunge = LUNGE * f.tech.drive * dt / f.impact_at
                    if s.distance() > 0.9: f.x += lunge * f.facing
                    if f.t >= f.impact_at: f.impact_done = True; f.impact_time = s.time + dt; s.impact(i)
                elif f.phase == PH['Striking']:
                    f.x += 0.5 * dt * f.facing if s.distance() > 0.8 else 0.0  # follow through past / alongside
                if f.phase == PH['Striking'] and f.t >= f.dur: f.phase = PH['Recovering']; f.t = 0.0; f.dur = RETURN_S
            elif f.phase in (PH['Recovering'], PH['Stunned']):
                if f.t >= f.dur: f.phase = PH['Ready']; f.dur = 0.0
            elif f.phase == PH['Blocking']:
                pass
        # separation: fighters cannot pass through each other
        a, b = s.f
        if b.x - a.x < 0.6:
            mid = (a.x + b.x) / 2; a.x = mid - 0.3; b.x = mid + 0.3
        s.resolve_pending()
        for i in (0, 1):
            if abs(s.f[i].x) > s.p.court_half and s.match.phase == R.Match.Running:
                s.match.hansoku_(i, 'jogai'); s.after_point()
                return
        s.match.tick(dt); s.time += dt

    def impact(s, i):
        f, op = s.f[i], s.f[1 - i]
        a = f.action
        d = s.distance()
        lo, hi = REACH[a]
        lateral = abs(op.y - f.y)
        contact = lo <= d <= hi and lateral < (0.45 if a in (A['Kote'], A['Do']) else 0.35)
        rng = s.rng; tq = f.tech
        e = R.Evidence(target=STRIKE_TARGET[a], contact=contact)
        e.contactAlongBlade = min(1.0, max(0.0, 0.45 + 0.5 * (d - lo) / (hi - lo) + 0.05 * rng.gauss()))
        e.edgeSide = rng.value() < tq.edge_prob
        e.hasujiErrorDeg = abs(rng.gauss()) * tq.hasuji_sigma + lateral * 20.0
        e.tipSpeed = tq.speed * s.p.speed_scale[i] * (1.0 + 0.1 * rng.gauss())
        e.strikeArc = True
        e.approachSpeed = LUNGE * tq.drive / f.impact_at
        e.kiai = rng.value() < tq.kiai_prob
        e.kiaiOffset = rng.gauss() * tq.timing_sigma
        e.fumikomi = tq.drive > 0.2
        e.fumikomiOffset = rng.gauss() * tq.timing_sigma
        e.torsoTiltDeg = abs(rng.gauss()) * tq.tilt_sigma
        e.balanced = True
        e.distance = f.strike_start_dist
        e.opportunity = f.opp_at_start
        cover = BLOCK_COVERS.get(op.block_kind, {}).get(a, 0.0) if op.phase == PH['Blocking'] and op.t >= BLOCK_RAISE else 0.0
        passive = op.tech.guard if (op.phase == PH['Ready'] and f.opp_at_start == R.OPP['IntoReadyKamae']) else 0.0
        if contact and ((cover > 0 and rng.value() < cover) or (passive > 0 and rng.value() < passive)):
            e.blocked = True
            f.phase = PH['Stunned']; f.t = 0.0; f.dur = STUN_S
            op.counter_until = s.time + COUNTER_WINDOW
            op.phase = PH['Ready']; op.dur = 0.0
        if rng.value() < tq.zanshin_prob:
            e.zanshinObserved = True; e.followThrough = rng.range(0.35, 0.8); e.kamaeRestoredAfter = rng.range(0.5, 1.3)
        else:
            e.zanshinObserved = rng.value() < 0.5; e.followThrough = rng.range(0.0, 0.2); e.kamaeRestoredAfter = rng.range(1.5, 3.0)
            e.turnedAway = rng.value() < 0.5
        ev = R.evaluate_strike(e, s.rules, s.profile, R.K['Chudan'])
        dec = R.panel_decide(ev, s.rules)
        s.last_eval[i] = ev; s.last_decision[i] = dec
        if not dec['awarded']:
            s.shaped[i] += R.WHIFF if ev.invalidReason == R.INV['Missed'] else R.reward_own_strike(ev, dec)
        if f.pending is None: f.pending = (s.time, ev, dec)

    def resolve_pending(s):
        a, b = s.f
        ready = [f.pending is not None for f in s.f]
        if not any(ready): return
        # wait one sub-step for a possible simultaneous strike (ai-uchi window 0.1 s)
        now = s.time
        for i in (0, 1):
            f = s.f[i]
            if f.pending is None: continue
            t, ev, dec = f.pending
            other = s.f[1 - i].pending
            if other is not None and abs(other[0] - t) <= 0.1 and dec['awarded'] and other[2]['awarded']:
                s.match.log('AiUchi'); a.pending = b.pending = None; return
            if now - t >= 0.1 or other is not None:
                f.pending = None
                if dec['awarded'] and s.match.phase == R.Match.Running:
                    s.match.award_ippon(i, ev.target); s.after_point(); return

    def after_point(s):
        if s.match.phase != R.Match.Finished:
            s.reset_positions(); s.match.hajime()

    def step(s, a0, a1):
        """Advance one decision step (0.1 s). Returns rewards for both fighters."""
        p0, p1 = list(s.match.points), list(s.match.hansoku)
        s.apply(0, a0); s.apply(1, a1)
        t = 0.0
        while t < DECISION_DT - 1e-9 and s.match.phase != R.Match.Finished:
            s.sub_step(SUB_DT); t += SUB_DT
        r = [R.STEP + s.shaped[0], R.STEP + s.shaped[1]]; s.shaped = [0.0, 0.0]
        for i in (0, 1):
            gained = s.match.points[i] - p0[i]
            r[i] += gained * R.IPPON; r[1 - i] += gained * R.CONCEDED
            if s.match.hansoku[i] > p1[i]: r[i] += R.HANSOKU
        if s.match.phase == R.Match.Finished:
            for i in (0, 1): r[i] += (0.0 if s.match.winner < 0 else (R.WIN if s.match.winner == i else R.LOSS))
        return r

    @property
    def done(s): return s.match.phase == R.Match.Finished
