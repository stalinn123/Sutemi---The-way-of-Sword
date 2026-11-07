"""Exact Python mirror of Assets/_Project/Scripts/Runtime/Rules (KendoEvaluationEngine, ShinpanPanel, KendoMatch, KendoReward).
Used by the Python tests, the duel simulator and the RL training scripts so that training rewards come from the same rules."""
from dataclasses import dataclass, field
import math

# enums (order must equal C#)
TARGETS = ['None','Men','MenRight','MenLeft','KoteRight','KoteLeft','DoRight','DoLeft','Tsuki']
T = {n: i for i, n in enumerate(TARGETS)}
KAMAE = ['Chudan','Jodan','Gedan','Hasso','Waki','Sageto','None']
K = {n: i for i, n in enumerate(KAMAE)}
CRITERIA = ['DeliberateStrike','ValidTarget','StrikingPart','Hasuji','Spirit','Posture','Zanshin','KiKenTaiItchi','Distance','Timing',
            'BodyDrive','Sharpness','Grip','KensenLine','LeftHand','Feet','Stillness','BowDepth','BowHold','Facing','ShinaiAtSide','Gaze',
            'OkuriAshi','KamaeKept','Accuracy','Control']
C = {n: i for i, n in enumerate(CRITERIA)}
INVALID = ['None','ContactOnly','TargetNotAllowed','NotMonouchi','WrongSide','BadHasuji','NoSpirit','BadPosture','NoZanshin','Blocked',
           'AiUchi','OpponentAlreadyDefenceless','Missed']
INV = {n: i for i, n in enumerate(INVALID)}
OPPORTUNITY = ['None','Debana','AfterBlock','OpponentRetreating','OpponentStatic','AfterOpponentStrike','IntoReadyKamae']
OPP = {n: i for i, n in enumerate(OPPORTUNITY)}
STRIKE_WEIGHTS = [10,10,12,10,10,8,10,10,6,5,4,5]

@dataclass
class RuleConfig:
    allowTsuki: bool = True
    leftKoteOnlyAgainstNonChudan: bool = True
    pointsToWin: int = 2
    matchSeconds: float = 300.0
    encho: bool = True
    enchoSeconds: float = 0.0
    leaderWinsAtTime: bool = True
    hantei: bool = True
    hansokuPerIppon: int = 2
    jogaiIsHansoku: bool = True
    droppedShinaiIsHansoku: bool = True
    oneFlagWithAbstentionsCounts: bool = True
    revokeOnCelebration: bool = True
    aiUchiNoPoint: bool = True

def fik_standard(): return RuleConfig()
def beginner_bout(): return RuleConfig(allowTsuki=False, matchSeconds=180.0, enchoSeconds=120.0)
def training_bout(): return RuleConfig(allowTsuki=True, matchSeconds=240.0, enchoSeconds=120.0)
def ippon_shobu(): return RuleConfig(pointsToWin=1)

@dataclass
class Profile:
    name: str = 'Intermediate'
    monouchiStart: float = 0.70
    maxHasujiDeg: float = 30.0
    minTipSpeed: float = 4.0
    goodTipSpeed: float = 8.0
    kiaiEarliest: float = -0.40
    kiaiLatest: float = 0.20
    kktiWindow: float = 0.15
    requireFumikomiForKkti: bool = True
    maxTorsoTiltDeg: float = 20.0
    minFollowThrough: float = 0.25
    maxKamaeRestore: float = 1.6
    idealDistanceMin: float = 1.55
    idealDistanceMax: float = 2.45
    minApproachSpeed: float = 0.25
    contactOnlyScoreCap: float = 20.0
    passScore: float = 70.0
    maxKensenOffset: float = 0.12
    maxKensenLateralDeg: float = 8.0
    leftHandForwardMin: float = 0.06
    leftHandForwardMax: float = 0.22
    leftHandHeightTol: float = 0.10
    stanceLengthMin: float = 0.10
    stanceLengthMax: float = 0.35
    stanceWidthMax: float = 0.20
    stillSeconds: float = 1.5
    bowTolDeg: float = 8.0
    bowHoldMin: float = 0.7
    facingTolDeg: float = 15.0
    markerTol: float = 0.18
    maxGazePitchDeg: float = 20.0

def beginner(): return Profile(name='Beginner', maxHasujiDeg=38.0, minTipSpeed=3.2, kktiWindow=0.22, maxTorsoTiltDeg=25.0,
    idealDistanceMin=1.45, idealDistanceMax=2.6, maxKamaeRestore=2.2, minFollowThrough=0.15, maxKensenOffset=0.16,
    maxKensenLateralDeg=11.0, passScore=65.0, bowTolDeg=10.0, markerTol=0.22)
def intermediate(): return Profile()
def shiai(): return Profile(name='Shiai', maxHasujiDeg=24.0, minTipSpeed=5.0, kktiWindow=0.12, maxTorsoTiltDeg=16.0,
    maxKamaeRestore=1.3, minFollowThrough=0.3, passScore=75.0)

@dataclass
class Evidence:
    target: int = 0
    contact: bool = False
    contactAlongBlade: float = 0.0
    edgeSide: bool = True
    hasujiErrorDeg: float = 0.0
    tipSpeed: float = 0.0
    strikeArc: bool = False
    approachSpeed: float = 0.0
    kiai: bool = False
    kiaiOffset: float = 99.0
    fumikomi: bool = False
    fumikomiOffset: float = 99.0
    bothHands: bool = True
    torsoTiltDeg: float = 0.0
    balanced: bool = True
    distance: float = 0.0
    opportunity: int = 0
    blocked: bool = False
    opponentDefenceless: bool = False
    simultaneousWithOpponent: bool = False
    zanshinObserved: bool = False
    followThrough: float = 0.0
    kamaeRestoredAfter: float = 99.0
    turnedAway: bool = False
    celebrated: bool = False

def clamp01(x): return 0.0 if x < 0 else (1.0 if x > 1 else x)
def ramp(x, bad, good):
    if abs(good - bad) < 1e-6: return 1.0 if x >= good else 0.0
    return clamp01((x - bad) / (good - bad))

class Result:
    def __init__(s):
        s.criteria = []  # (criterion, official, passed, score, detail)
        s.performanceScore = 0.0; s.passed = False
        s.yukoDatotsu = False; s.invalidReason = 0; s.target = 0; s.officialMargin = -1.0
    def add(s, c, official, passed, score, detail=''):
        s.criteria.append((C[c], official, bool(passed), clamp01(score), detail))
    def get(s, c):
        for r in s.criteria:
            if r[0] == C[c]: return r
    def failed(s): return [CRITERIA[r[0]] for r in s.criteria if not r[2]]

def target_allowed(t, rules, opp_kamae):
    if t == 0: return False
    if t == T['Tsuki']: return rules.allowTsuki
    if t == T['KoteLeft']: return not (rules.leftKoteOnlyAgainstNonChudan and opp_kamae == K['Chudan'])
    return True

def timing_score(o):
    if o in (OPP['Debana'], OPP['AfterBlock'], OPP['AfterOpponentStrike'], OPP['OpponentRetreating']): return 1.0
    if o == OPP['OpponentStatic']: return 0.6
    if o == OPP['IntoReadyKamae']: return 0.2
    return 0.5

def evaluate_strike(e, rules, p, opp_kamae=0):
    ev = Result(); ev.target = e.target
    if not e.contact or e.target == 0:
        ev.add('ValidTarget', True, False, 0.0)
        ev.invalidReason = INV['Missed']; ev.performanceScore = 0.0; ev.officialMargin = -1.0
        return ev
    speed_ramp = ramp(e.tipSpeed, 0.6 * p.minTipSpeed, p.minTipSpeed)
    deliberate = e.strikeArc and e.tipSpeed >= 0.6 * p.minTipSpeed
    ev.add('DeliberateStrike', True, deliberate, 0.5 + 0.5 * speed_ramp if deliberate else 0.2 * speed_ramp)
    allowed = target_allowed(e.target, rules, opp_kamae)
    ev.add('ValidTarget', True, allowed, 1.0 if allowed else 0.0)
    monouchi = e.contactAlongBlade >= p.monouchiStart
    ev.add('StrikingPart', True, monouchi and e.edgeSide, ramp(e.contactAlongBlade, p.monouchiStart - 0.2, p.monouchiStart) * (1.0 if e.edgeSide else 0.3))
    hasuji = e.hasujiErrorDeg <= p.maxHasujiDeg
    ev.add('Hasuji', True, hasuji, clamp01(1 - e.hasujiErrorDeg / (2 * p.maxHasujiDeg)))
    spirit = e.kiai and p.kiaiEarliest <= e.kiaiOffset <= p.kiaiLatest
    ev.add('Spirit', True, spirit, 1.0 if spirit else (0.4 if e.kiai else 0.0))
    posture = e.bothHands and e.balanced and e.torsoTiltDeg <= p.maxTorsoTiltDeg
    ev.add('Posture', True, posture, (0.5 if e.bothHands else 0) + (0.2 if e.balanced else 0) + 0.3 * clamp01(1 - e.torsoTiltDeg / (2 * p.maxTorsoTiltDeg)))
    zanshin = (e.zanshinObserved and e.followThrough >= p.minFollowThrough and e.kamaeRestoredAfter <= p.maxKamaeRestore
               and not e.turnedAway and not (e.celebrated and rules.revokeOnCelebration))
    z1 = clamp01(e.followThrough / max(0.01, p.minFollowThrough))
    z2 = 1.0 if e.kamaeRestoredAfter <= p.maxKamaeRestore else clamp01(1 - (e.kamaeRestoredAfter - p.maxKamaeRestore) / p.maxKamaeRestore)
    sz = (0.4 * z1 + 0.4 * z2 + (0.0 if (e.turnedAway or e.celebrated) else 0.2)) if e.zanshinObserved else 0.0
    ev.add('Zanshin', True, zanshin, sz)
    kiai_in = e.kiai and abs(e.kiaiOffset) <= p.kktiWindow
    foot_in = e.fumikomi and abs(e.fumikomiOffset) <= p.kktiWindow
    kkti = kiai_in and (foot_in or not p.requireFumikomiForKkti)
    ev.add('KiKenTaiItchi', False, kkti, 1.0 if kkti else (0.5 if (kiai_in or foot_in) else 0.0))
    inside = p.idealDistanceMin <= e.distance <= p.idealDistanceMax
    outside = p.idealDistanceMin - e.distance if e.distance < p.idealDistanceMin else max(0.0, e.distance - p.idealDistanceMax)
    ev.add('Distance', False, inside, 1.0 if inside else clamp01(1 - outside / 0.6))
    st = timing_score(e.opportunity)
    ev.add('Timing', False, st >= 0.5, st)
    ev.add('BodyDrive', False, e.approachSpeed >= p.minApproachSpeed, clamp01(e.approachSpeed / (2 * p.minApproachSpeed)))
    ev.add('Sharpness', False, e.tipSpeed >= p.minTipSpeed, ramp(e.tipSpeed, 0.5 * p.minTipSpeed, p.goodTipSpeed))
    if not deliberate: r = 'ContactOnly'
    elif not allowed: r = 'TargetNotAllowed'
    elif not monouchi: r = 'NotMonouchi'
    elif not e.edgeSide: r = 'WrongSide'
    elif not hasuji: r = 'BadHasuji'
    elif not spirit: r = 'NoSpirit'
    elif not posture: r = 'BadPosture'
    elif not zanshin: r = 'NoZanshin'
    elif e.blocked: r = 'Blocked'
    else: r = 'None'
    ev.invalidReason = INV[r]
    ev.yukoDatotsu = r == 'None'
    total = 0.0; mn = 1.0
    for (c, off, ok, sc, _) in ev.criteria:
        total += STRIKE_WEIGHTS[c] * sc
        if off and sc < mn: mn = sc
    if e.blocked: total *= 0.6
    if r == 'ContactOnly': total = min(total, p.contactOnlyScoreCap)
    ev.performanceScore = round(total, 2)
    ev.officialMargin = mn - 0.5 if ev.yukoDatotsu else -1.0
    ev.passed = ev.yukoDatotsu and ev.performanceScore >= p.passScore
    return ev

def _finish(ev, weights, pass_score):
    total = 0.0; allok = True
    for i, (c, off, ok, sc, _) in enumerate(ev.criteria):
        total += weights[i] * sc; allok = allok and ok
    ev.performanceScore = round(total, 2); ev.passed = allok and ev.performanceScore >= pass_score
    return ev

@dataclass
class KamaeEvidence:
    kamae: int = 0
    bothHands: bool = True
    kensenOffset: float = 0.0
    kensenLateralDeg: float = 0.0
    leftHandForward: float = 0.12
    leftHandHeightError: float = 0.0
    torsoTiltDeg: float = 0.0
    stanceLength: float = 0.2
    stanceWidth: float = 0.1
    heldSeconds: float = 2.0

def evaluate_kamae(k, p):
    ev = Result(); chudan = k.kamae == K['Chudan']
    ev.add('Grip', False, chudan and k.bothHands, 1.0 if (chudan and k.bothHands) else 0.0)
    line = k.kensenOffset <= p.maxKensenOffset and k.kensenLateralDeg <= p.maxKensenLateralDeg
    ev.add('KensenLine', False, line, 0.5 * clamp01(1 - k.kensenOffset / (2 * p.maxKensenOffset)) + 0.5 * clamp01(1 - k.kensenLateralDeg / (2 * p.maxKensenLateralDeg)))
    hand = p.leftHandForwardMin <= k.leftHandForward <= p.leftHandForwardMax and abs(k.leftHandHeightError) <= p.leftHandHeightTol
    ev.add('LeftHand', False, hand, 1.0 if hand else 0.3)
    ev.add('Posture', False, k.torsoTiltDeg <= p.maxTorsoTiltDeg, clamp01(1 - k.torsoTiltDeg / (2 * p.maxTorsoTiltDeg)))
    feet = p.stanceLengthMin <= k.stanceLength <= p.stanceLengthMax and k.stanceWidth <= p.stanceWidthMax
    ev.add('Feet', False, feet, 1.0 if feet else 0.3)
    ev.add('Stillness', False, k.heldSeconds >= p.stillSeconds, clamp01(k.heldSeconds / p.stillSeconds))
    return _finish(ev, [20, 25, 15, 15, 15, 10], p.passScore)

@dataclass
class ReiEvidence:
    bowAngleDeg: float = 0.0
    targetAngleDeg: float = 15.0
    holdSeconds: float = 0.0
    facingErrorDeg: float = 0.0
    shinaiAtSide: bool = True
    requireGaze: bool = False
    gazeOnPartner: bool = True
    feetStill: bool = True

def evaluate_rei(r, p):
    ev = Result(); err = abs(r.bowAngleDeg - r.targetAngleDeg)
    ev.add('BowDepth', False, err <= p.bowTolDeg, clamp01(1 - err / (2 * p.bowTolDeg)))
    ev.add('BowHold', False, r.holdSeconds >= p.bowHoldMin, clamp01(r.holdSeconds / p.bowHoldMin))
    ev.add('Facing', False, r.facingErrorDeg <= p.facingTolDeg, clamp01(1 - r.facingErrorDeg / (2 * p.facingTolDeg)))
    ev.add('ShinaiAtSide', False, r.shinaiAtSide, 1.0 if r.shinaiAtSide else 0.0)
    gaze = (not r.requireGaze) or r.gazeOnPartner
    ev.add('Gaze', False, gaze, 1.0 if gaze else 0.0)
    ev.add('Stillness', False, r.feetStill, 1.0 if r.feetStill else 0.0)
    return _finish(ev, [30, 15, 20, 15, 10, 10], p.passScore)

@dataclass
class SuriashiEvidence:
    steps: int = 0
    stepsWithOkuriAshi: int = 0
    crossedFeet: int = 0
    maxGazePitchDeg: float = 0.0
    kamaeKeptFraction: float = 1.0
    markerError: float = 0.0
    extraSteps: int = 0
    hops: int = 0

def evaluate_suriashi(s, p):
    ev = Result()
    ok = s.steps > 0 and s.stepsWithOkuriAshi == s.steps
    ev.add('OkuriAshi', False, ok, s.stepsWithOkuriAshi / s.steps if s.steps > 0 else 0.0)
    clean = s.crossedFeet == 0 and s.hops == 0
    ev.add('Feet', False, clean, 1.0 if clean else 0.0)
    ev.add('Gaze', False, s.maxGazePitchDeg <= p.maxGazePitchDeg, clamp01(1 - s.maxGazePitchDeg / (2 * p.maxGazePitchDeg)))
    ev.add('KamaeKept', False, s.kamaeKeptFraction >= 0.9, s.kamaeKeptFraction)
    ev.add('Accuracy', False, s.markerError <= p.markerTol, clamp01(1 - s.markerError / (3 * p.markerTol)))
    ev.add('Control', False, s.extraSteps <= 1, 1.0 if s.extraSteps <= 1 else clamp01(1 - (s.extraSteps - 1) / 4))
    return _finish(ev, [25, 10, 15, 20, 20, 10], p.passScore)

STRICTNESS = [0.00, 0.06, 0.12]
def panel_decide(ev, rules, view=None, strictness=STRICTNESS, min_view=0.3):
    flags = abst = 0
    for i, s in enumerate(strictness):
        v = view[i] if view is not None and i < len(view) else 1.0
        if v < min_view: abst += 1; continue
        if ev.yukoDatotsu and ev.officialMargin >= s + (1 - v) * 0.2: flags += 1
    awarded = flags >= 2 or (rules.oneFlagWithAbstentionsCounts and flags == 1 and abst == 2)
    return {'awarded': awarded, 'flags': flags, 'abstentions': abst}

class Match:
    NotStarted, Running, Yame, Finished = range(4)
    def __init__(s, rules):
        s.rules = rules; s.points = [0, 0]; s.hansoku = [0, 0]; s.events = []
        s.phase = Match.NotStarted; s.in_encho = False; s.time_remaining = rules.matchSeconds; s.elapsed = 0.0
        s.winner = -1; s.end_reason = None; s.hantei_score = None
    def log(s, kind, side=-1, detail=''): s.events.append((round(s.elapsed, 3), kind, side, detail))
    def hajime(s):
        if s.phase == Match.Finished: return
        s.phase = Match.Running; s.log('Hajime')
    def yame(s, why=''):
        if s.phase != Match.Running: return
        s.phase = Match.Yame; s.log('Yame', -1, why)
    def tick(s, dt):
        if s.phase != Match.Running: return
        s.elapsed += dt
        if s.in_encho and s.rules.enchoSeconds <= 0: return
        s.time_remaining -= dt
        if s.time_remaining <= 0: s.time_remaining = 0.0; s._time_up()
    def award_ippon(s, side, target=0):
        if s.phase != Match.Running: return
        s.points[side] += 1; s.log('Ippon', side); s._after_point()
    def torikeshi(s, side):
        if s.points[side] <= 0: return
        s.points[side] -= 1; s.log('Torikeshi', side)
    def hansoku_(s, side, reason=''):
        if s.phase in (Match.Finished, Match.NotStarted): return
        s.hansoku[side] += 1; s.log('Hansoku', side, reason)
        if s.rules.hansokuPerIppon > 0 and s.hansoku[side] % s.rules.hansokuPerIppon == 0:
            s.points[1 - side] += 1; s.log('HansokuIppon', 1 - side); s.phase = Match.Running; s._after_point()
    def _after_point(s):
        leader = 0 if s.points[0] > s.points[1] else (1 if s.points[1] > s.points[0] else -1)
        if s.points[0] >= s.rules.pointsToWin or s.points[1] >= s.rules.pointsToWin or (s.in_encho and leader >= 0):
            s._finish(leader, 'Points'); return
        s.phase = Match.Yame
    def _time_up(s):
        s.log('TimeUp')
        leader = 0 if s.points[0] > s.points[1] else (1 if s.points[1] > s.points[0] else -1)
        if not s.in_encho and leader >= 0 and s.rules.leaderWinsAtTime: s._finish(leader, 'TimeLeader'); return
        if not s.in_encho and s.rules.encho:
            s.in_encho = True; s.time_remaining = s.rules.enchoSeconds; s.phase = Match.Yame; s.log('Encho'); return
        if s.rules.hantei and s.hantei_score is not None:
            a, b = s.hantei_score(0), s.hantei_score(1)
            if abs(a - b) > 1e-3:
                w = 0 if a > b else 1; s.log('Hantei', w); s._finish(w, 'Hantei'); return
        s._finish(-1, 'Hikiwake')
    def _finish(s, w, reason):
        s.winner = w; s.end_reason = reason; s.phase = Match.Finished; s.log('Shobuari' if w >= 0 else 'Hikiwake', w, reason)

IPPON, CONCEDED, WIN, LOSS, HANSOKU, STEP, PARTIAL, WHIFF = 1.0, -1.0, 1.0, -1.0, -0.3, -0.001, 0.05, -0.02
def reward_own_strike(ev, decision):
    if decision['awarded']: return IPPON
    if ev is None or ev.invalidReason in (INV['ContactOnly'], INV['Missed']): return 0.0
    off = [r for r in ev.criteria if r[1]]
    return PARTIAL * sum(1 for r in off if r[2]) / len(off) if off else 0.0
