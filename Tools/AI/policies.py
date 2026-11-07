"""Agent 1 (rule-based opponent) – exact mirror of Kendo.AI.RuleBasedPolicy."""
from duel_sim import A, PH, N_ACTIONS

class Style:
    def __init__(s, aggression=0.5, preferred_distance=2.2, reaction=0.3, defense=0.5, counter=0.4, mix=(0.45, 0.3, 0.2, 0.05),
                 jitter=0.15, retreat_after=0.3, feint=0.0, combo=0.0, debana=0.0, hiki=0.0):
        s.aggression, s.preferred_distance, s.reaction, s.defense = aggression, preferred_distance, reaction, defense
        s.counter, s.mix, s.jitter, s.retreat_after = counter, list(mix), jitter, retreat_after
        s.feint, s.combo, s.debana, s.hiki = feint, combo, debana, hiki

class RuleBasedPolicy:
    def __init__(s, style, rng):
        s.st, s.rng = style, rng
        s.react_timer = 0.0; s.seen_strike = False; s.last_was_strike = False; s.feinting = 0
    def pick_strike(s, d):
        m = list(s.st.mix)
        if d > 1.95: m[2] *= 0.3            # do needs to be close
        if d < 1.6: m[0] *= 0.6; m[1] *= 1.4
        tot = sum(m); r = s.rng.value() * tot
        for k, a in enumerate((A['Men'], A['Kote'], A['Do'], A['Tsuki'])):
            r -= m[k]
            if r <= 0: return a
        return A['Men']
    def act(s, o):
        st, rng = s.st, s.rng
        d = o[0] * 3.0
        my_phase = max(range(6), key=lambda k: o[4 + k])
        op_phase = max(range(6), key=lambda k: o[10 + k])
        if my_phase not in (PH['Ready'], PH['Blocking']): return A['Wait']
        counter_window = o[22] > 0.5
        # react to an incoming strike after the reaction time
        if op_phase == PH['Striking'] and o[17] < 0.45:
            if not s.seen_strike: s.seen_strike = True; s.react_timer = 0.0
            s.react_timer += 0.1
            if s.react_timer >= st.reaction:
                if st.debana > 0 and o[17] < 0.35 and rng.value() < st.debana and d < 2.3: return A['Kote']
                if rng.value() < st.defense:
                    return A['BlockDo'] if o[20] > 0.5 else A['BlockHigh']
        else:
            s.seen_strike = False
        if counter_window and rng.value() < st.counter:
            return s.pick_strike(d)
        # punish an opponent who is recovering from (or was deflected in) a strike: waza no tsukita tokoro
        if op_phase in (PH['Recovering'], PH['Stunned']) and 1.2 <= d <= 2.3 and rng.value() < st.counter:
            return s.pick_strike(d)
        if s.last_was_strike and st.combo > 0 and rng.value() < st.combo and d < 2.0:
            s.last_was_strike = False; return A['Men']
        s.last_was_strike = False
        in_range = 1.5 <= d <= 2.45
        if in_range and rng.value() < st.aggression * 0.35:
            if st.feint > 0 and s.feinting == 0 and rng.value() < st.feint:
                s.feinting = 2; return A['StepForward']
            s.last_was_strike = True; s.feinting = max(0, s.feinting - 1)
            return s.pick_strike(d)
        if d < 1.4:
            if st.hiki > 0 and rng.value() < st.hiki: s.last_was_strike = True; return A['Men']
            return A['StepBack'] if rng.value() < 0.6 else A['Wait']
        if s.feinting > 0: s.feinting -= 1; return s.pick_strike(d)
        if o[1] < -0.85: return A['StepForward']          # near my own back line
        if d > st.preferred_distance + 0.25 and rng.value() < 0.5 + st.aggression * 0.4: return A['StepForward']
        if d < st.preferred_distance - 0.35 and rng.value() < 0.4: return A['StepBack']
        if rng.value() < st.jitter:
            return [A['StepLeft'], A['StepRight'], A['StepForward'], A['StepBack']][rng.int(4)]
        return A['Wait']

def easy(): return Style(aggression=0.25, reaction=0.35, defense=0.2, counter=0.1, preferred_distance=2.4, jitter=0.1)
def medium(): return Style(aggression=0.45, reaction=0.2, defense=0.45, counter=0.35, preferred_distance=2.2)
def hard(): return Style(aggression=0.6, reaction=0.15, defense=0.6, counter=0.55, preferred_distance=2.1, combo=0.2)
def former_student(): return Style(aggression=0.75, reaction=0.1, defense=0.55, counter=0.65, preferred_distance=2.0,
                                   mix=(0.4, 0.35, 0.15, 0.10), feint=0.3, combo=0.4, debana=0.35, hiki=0.4, jitter=0.25)

from duel_sim import Technique
def tech_easy(): return Technique(speed=7.0, hasuji_sigma=18.0, kiai_prob=0.7, zanshin_prob=0.6, guard=0.15, timing_sigma=0.12)
def tech_medium(): return Technique(speed=8.0, hasuji_sigma=13.0, kiai_prob=0.85, zanshin_prob=0.75, guard=0.3, timing_sigma=0.09)
def tech_hard(): return Technique(speed=9.0, hasuji_sigma=10.0, kiai_prob=0.9, zanshin_prob=0.85, guard=0.4, timing_sigma=0.07)
def tech_boss(): return Technique(speed=10.0, hasuji_sigma=8.0, kiai_prob=0.95, zanshin_prob=0.9, guard=0.5, timing_sigma=0.05)
TIERS = {'easy': (easy, tech_easy), 'medium': (medium, tech_medium), 'hard': (hard, tech_hard), 'boss': (former_student, tech_boss)}
