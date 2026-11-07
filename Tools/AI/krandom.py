"""Deterministic RNG identical to Kendo.AI.KRandom (xorshift64*). Python ints are masked to 64 bits."""
import math
M64 = (1 << 64) - 1
class KRandom:
    def __init__(s, seed):
        s.state = (seed * 0x9E3779B97F4A7C15 + 0x2545F4914F6CDD1D) & M64
        if s.state == 0: s.state = 0x2545F4914F6CDD1D
    def next_u64(s):
        x = s.state
        x ^= (x >> 12); x ^= (x << 25) & M64; x ^= (x >> 27)
        s.state = x & M64
        return (x * 0x2545F4914F6CDD1D) & M64
    def value(s):            # [0,1)
        return (s.next_u64() >> 11) * (1.0 / 9007199254740992.0)
    def range(s, a, b): return a + (b - a) * s.value()
    def int(s, n): return int(s.value() * n) % n
    def gauss(s):
        u1 = max(1e-12, s.value()); u2 = s.value()
        return math.sqrt(-2.0 * math.log(u1)) * math.cos(2 * math.pi * u2)
    def poisson(s, lam):
        L = math.exp(-lam); k = 0; p = 1.0
        while True:
            p *= s.value()
            if p <= L: return k
            k += 1
