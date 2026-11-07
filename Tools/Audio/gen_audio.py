"""Procedural sound for the game (no audio assets were supplied). python3 -I gen_audio.py <Assets/_Project/Audio>
Deterministic (seeded). Music: Karplus-Strong koto plucks in the miyako-bushi scale, breathy shakuhachi-like tones,
taiko patterns. SFX: bamboo shinai clacks, fumikomi stamp, sliding feet, temple bell, UI blips. Voices: formant-
synthesised kiai ("Men!")."""
import sys, os, wave, numpy as np
OUT = sys.argv[1]
SR = 22050
rng = np.random.default_rng(42)

def save(rel, x, sr=SR):
    path = os.path.join(OUT, rel); os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.asarray(x, np.float64); peak = np.max(np.abs(x)) + 1e-9
    x = np.clip(x / peak * 0.89, -1, 1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes((x * 32767).astype('<i2').tobytes())
    return path

def t(sec): return np.arange(int(sec * SR)) / SR
def env(n, a, d):
    e = np.ones(n); na = max(1, int(a * SR)); e[:na] = np.linspace(0, 1, na)
    e *= np.exp(-np.arange(n) / (d * SR)); return e

def biquad_bp(x, f, q):
    w0 = 2 * np.pi * f / SR; al = np.sin(w0) / (2 * q)
    b0, b1, b2 = al, 0, -al; a0, a1, a2 = 1 + al, -2 * np.cos(w0), 1 - al
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = (b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, v, y1, o; y[i] = o
    return y

def lowpass(x, f):
    a = np.exp(-2 * np.pi * f / SR); y = np.zeros_like(x); s = 0.0
    for i, v in enumerate(x): s = (1 - a) * v + a * s; y[i] = s
    return y

def clack(level=1.0, ring=0.06):
    n = int(0.25 * SR); x = rng.standard_normal(n) * env(n, 0.0005, 0.004)
    y = sum(biquad_bp(x, f, 12) * g for f, g in ((1750, 1.0), (3150, 0.7), (5200, 0.4)))
    tt = np.arange(n) / SR
    y += 0.6 * np.sin(2 * np.pi * 190 * tt) * np.exp(-tt / ring) * (tt < 0.2)
    return y * level

def pluck(freq, dur, bright=0.5):
    n = int(dur * SR); p = max(2, int(SR / freq)); buf = rng.uniform(-1, 1, p) * 0.5
    buf = lowpass(buf, 2000 + 6000 * bright)
    out = np.zeros(n); idx = 0
    for i in range(n):
        out[i] = buf[idx]; nxt = (idx + 1) % p
        buf[idx] = 0.996 * 0.5 * (buf[idx] + buf[nxt]); idx = nxt
    return out

def breath_tone(freq, dur, vib=5.0):
    tt = t(dur); f = freq * (1 + 0.006 * np.sin(2 * np.pi * vib * tt))
    ph = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.08 * np.sin(3 * ph)
    noise = lowpass(rng.standard_normal(len(tt)), 3000) * 0.15
    e = np.minimum(1, tt / 0.25) * np.minimum(1, (dur - tt) / 0.4)
    return (tone + noise) * np.clip(e, 0, 1)

def taiko(level=1.0, f0=95, dur=1.1):
    tt = t(dur); f = f0 * (0.65 + 0.35 * np.exp(-tt / 0.08))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / 0.35)
    skin = biquad_bp(rng.standard_normal(len(tt)) * np.exp(-tt / 0.02), 400, 2)
    return (body + 0.3 * skin) * level

def kiai(f0a, f0b, dur=0.55, breath=0.08):
    tt = t(dur); f0 = np.interp(tt, [0, 0.08, dur], [f0a * 0.9, f0a, f0b])
    ph = np.cumsum(f0) / SR; src = 2 * (ph % 1) - 1  # sawtooth glottal source
    m = tt < 0.07  # "m" onset: nasal, closed
    out = np.zeros_like(tt)
    for F, g, q in ((530, 1.0, 6), (1840, 0.55, 9), (2480, 0.35, 10)):  # vowel /e/
        out += biquad_bp(src, F, q) * g
    nasal = biquad_bp(src, 250, 4)
    out = np.where(m, nasal * 0.6, out)
    out += breath * biquad_bp(rng.standard_normal(len(tt)), 2500, 1.5)
    e = np.minimum(1, tt / 0.015) * np.exp(-np.maximum(0, tt - 0.35) / 0.08)
    out = np.tanh(out * 2.2) * e
    # "n" release
    return out

SCALE = [293.66, 311.13, 392.0, 440.0, 466.16]  # D Eb G A Bb (miyako-bushi)
def note(i):
    o, k = divmod(i, 5); return SCALE[k] * (2 ** o)

def music(seconds, tempo, kind):
    n = int(seconds * SR); mix = np.zeros(n + SR * 3)
    beat = 60.0 / tempo
    if kind == 'dojo':
        drone = breath_tone(146.83, seconds, 4.0) * 0.12
        mix[:len(drone)] += drone
        tpos = 0.0; i = 0
        phrase = [0, 2, 3, 4, 3, 2, 1, 0, 2, 4, 5, 4, 3, 2, 0, -1]
        while tpos < seconds - 2:
            k = phrase[i % len(phrase)] + (5 if (i // 16) % 2 else 0)
            p = pluck(note(k) / 2, 2.5, 0.4) * (0.5 if i % 4 else 0.75)
            s = int(tpos * SR); mix[s:s + len(p)] += p
            tpos += beat * (2 if i % 8 == 7 else 1); i += 1
        for s0 in np.arange(4, seconds - 6, 12):
            b = breath_tone(note(rng.integers(5, 10)), 4.5) * 0.18
            s = int(s0 * SR); mix[s:s + len(b)] += b
    else:
        drone = breath_tone(73.4 if kind == 'battle' else 61.7, seconds, 3.0) * 0.18
        mix[:len(drone)] += drone
        pattern = [1, 0, 0.5, 0, 1, 0.5, 0, 0.6] if kind == 'battle' else [1, 0.4, 0.7, 0, 1, 0.5, 0.8, 0.5]
        steps = int(seconds / (beat / 2))
        for j in range(steps):
            lv = pattern[j % len(pattern)]
            if lv <= 0: continue
            d = taiko(lv * 0.8, 92 if j % 8 == 0 else 120)
            s = int(j * beat / 2 * SR); mix[s:s + len(d)] += d
        for s0 in np.arange(2, seconds - 4, 8 if kind == 'battle' else 6):
            k = rng.integers(3, 9)
            b = breath_tone(note(k) * (0.5 if kind == 'final' else 1), 2.5) * 0.15
            s = int(s0 * SR); mix[s:s + len(b)] += b
    # seamless loop: fold the tail back onto the start
    tail = mix[n:]; mix = mix[:n]; mix[:len(tail)] += tail
    return mix

def ambience(seconds, temple):
    n = int(seconds * SR)
    wind = lowpass(rng.standard_normal(n), 400 if temple else 600)
    wind *= 0.5 + 0.5 * np.sin(2 * np.pi * np.arange(n) / SR / 9.0)
    out = wind * 0.3
    for s0 in rng.uniform(1, seconds - 2, 10 if not temple else 4):
        tt = t(0.25); f = (3200 if not temple else 900) + 900 * np.sin(2 * np.pi * 6 * tt)
        chirp = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / 0.08) * (0.08 if not temple else 0.12)
        s = int(s0 * SR); out[s:s + len(chirp)] += chirp
    return out

def bell():
    tt = t(5.0); f = 220.0; out = np.zeros_like(tt)
    for r, g, d in ((1, 1, 3.5), (2.0, 0.6, 2.5), (2.76, 0.5, 2.0), (5.4, 0.3, 1.2), (8.93, 0.2, 0.7)):
        out += g * np.sin(2 * np.pi * f * r * tt) * np.exp(-tt / d)
    return out * np.minimum(1, tt / 0.004)

def blip(f, dur, f2=None):
    tt = t(dur); fr = np.linspace(f, f2 or f, len(tt))
    return np.sin(2 * np.pi * np.cumsum(fr) / SR) * env(len(tt), 0.002, dur / 3)

if __name__ == '__main__':
    files = []
    files.append(save('SFX/shinai_hit.wav', np.concatenate([clack(1.0), np.zeros(200)])))
    files.append(save('SFX/shinai_clash.wav', np.concatenate([clack(1.0, 0.1)[:2200], clack(0.8, 0.12)])))
    files.append(save('SFX/shinai_touch.wav', clack(0.25, 0.02)[:3000] * np.linspace(1, 0, 3000)))
    tt = t(0.4)
    files.append(save('SFX/fumikomi.wav', np.sin(2 * np.pi * np.cumsum(110 * (0.6 + 0.4 * np.exp(-tt / 0.03))) / SR) * np.exp(-tt / 0.09)
                      + 0.4 * biquad_bp(rng.standard_normal(len(tt)) * np.exp(-tt / 0.02), 900, 1.5)))
    files.append(save('SFX/foot_slide.wav', lowpass(rng.standard_normal(int(0.3 * SR)), 1800) * np.hanning(int(0.3 * SR))))
    files.append(save('SFX/bell.wav', bell()))
    files.append(save('SFX/taiko.wav', taiko(1.0, 85, 1.4)))
    files.append(save('SFX/ui_click.wav', blip(1400, 0.05)))
    files.append(save('SFX/ui_confirm.wav', blip(900, 0.12, 1500)))
    files.append(save('SFX/success.wav', np.concatenate([pluck(note(5), 0.18), pluck(note(7), 0.18), pluck(note(9), 1.0)])))
    files.append(save('SFX/fail.wav', np.concatenate([pluck(note(3) / 2, 0.25), pluck(note(1) / 2, 1.0)])))
    tt = t(0.35)
    files.append(save('SFX/flag.wav', biquad_bp(rng.standard_normal(len(tt)), 700, 0.8) * (0.5 + 0.5 * np.sin(2 * np.pi * 18 * tt)) * np.hanning(len(tt))))
    files.append(save('SFX/ambience_dojo.wav', ambience(40, False)))
    files.append(save('SFX/ambience_temple.wav', ambience(40, True)))
    files.append(save('Dialogue/kiai_player.wav', kiai(210, 170)))
    files.append(save('Dialogue/kiai_opponent.wav', kiai(150, 120)))
    files.append(save('Dialogue/kiai_boss.wav', kiai(115, 92, 0.65, 0.12)))
    files.append(save('Music/music_dojo.wav', music(64, 72, 'dojo')))
    files.append(save('Music/music_battle.wav', music(48, 100, 'battle')))
    files.append(save('Music/music_final.wav', music(48, 118, 'final')))
    for f in files: print(os.path.relpath(f, OUT), os.path.getsize(f) // 1024, 'KB')
