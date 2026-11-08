# 恐怖配乐：不是一条循环，而是几层分轨，由 AudioDirector 按危险程度叠上去。用法：python3 -I Tools/make_music.py
# drone 低频大二度拍频 / strings 不协和弦乐群 / choir 远处的合唱 / high 琴马边的尖细泛音 / pulse 缓慢的低频脉动
# 另有一次性：musicbox 走调的八音盒（从远处走廊传来）、swell 不协和上涌
import site, sys, pathlib, wave
sys.path.append(site.getusersitepackages())
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
SR = 44100
OUT = pathlib.Path(__file__).resolve().parents[1]/'Assets/_DLD/Audio/Music'; OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(13)
DUR = 72
def t_(d): return np.arange(int(d*SR))/SR
def lfo(t, f, lo, hi, ph=None):
    ph = rng.random()*6.28 if ph is None else ph
    return lo + (hi-lo)*(.5+.5*np.sin(2*np.pi*f*t+ph))
def smooth_noise(n, rate):
    k = int(n/SR*rate)+4; x = np.interp(np.arange(n), np.linspace(0, n, k), rng.standard_normal(k))
    return x/(np.max(np.abs(x))+1e-6)
def saw(freq, harm=9):
    ph = 2*np.pi*np.cumsum(freq)/SR; o = np.zeros_like(ph)
    for h in range(1, harm+1): o += np.sin(ph*h)/h
    return o
def pink(n):
    f = np.fft.rfft(rng.standard_normal(n)); f[1:] /= np.sqrt(np.arange(1, len(f))); x = np.fft.irfft(f, n); return x/np.std(x)
def lp(x, f): return sosfilt(butter(2, f, 'low', fs=SR, output='sos'), x, axis=0)
def bp(x, a, b): return sosfilt(butter(2, [a, b], 'band', fs=SR, output='sos'), x, axis=0)
def verb(x, t60, wet, lpf=2600):
    n = int(t60*SR); out = []
    for ch in range(2):
        ir = rng.standard_normal(n)*np.exp(-6.9*np.arange(n)/n); ir = lp(ir, lpf); ir /= np.sqrt(np.sum(ir**2))
        out.append(fftconvolve(x[:, ch], ir)[:len(x)])
    return x*(1-wet)+np.stack(out, 1)*wet
def st(l, r=None): return np.stack((l, l if r is None else r), 1)
def loop(x, xf=4.):
    k = int(xf*SR); w = np.linspace(0, 1, k)[:, None]; y = x[:-k].copy(); y[:k] = x[-k:]*(1-w)+x[:k]*w; return y
def save(name, x, peak=.85):
    x = x/(np.max(np.abs(x))+1e-6)*peak
    with wave.open(str(OUT/f'{name}.wav'), 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(x, -1, 1)*32767).astype(np.int16).tobytes())
    print(name, f'{len(x)/SR:.0f}s rms={np.sqrt(np.mean(x**2)):.3f}')

t = t_(DUR); n = len(t)
# ── drone：E1 和 F1 的小二度，每秒拍两下，贴着胸口 ──
d = np.sin(2*np.pi*41.2*t)*.9 + np.sin(2*np.pi*43.65*t+1)*.8 + np.sin(2*np.pi*61.74*t+2)*.3 + np.sin(2*np.pi*58.27*t)*.28
grit = lp(saw(82.4*(1+.002*smooth_noise(n, .2))) + saw(87.3*(1+.002*smooth_noise(n, .2))), 240)*lfo(t, .043, .1, .4)
air = lp(pink(n), 180)*.5*lfo(t, .03, .4, 1)
sw = lfo(t, .021, .55, 1)
save('m_drone', loop(st((d+grit*.5+air)*sw, (d+grit*.5*1.05+air*.9)*sw)))

# ── strings：E3 F3 Bb3 B3 的不协和群，每声部六条，缓慢往下滑 ──
L = np.zeros(n); R = np.zeros(n)
for base in (164.81, 174.61, 233.08, 246.94, 349.23, 493.88):
    gl = 1 - .03*(.5+.5*np.sin(2*np.pi*.017*t+rng.random()*6))           # 整体慢慢走音
    arc = lfo(t, 1/(19+rng.random()*14), 0, 1)**2.2
    for v in range(6):
        vib = 1 + .006*np.sin(2*np.pi*(4.6+rng.random())*t+rng.random()*6) + .004*smooth_noise(n, .4)
        x = saw(base*(1+(v-2.5)*.0035)*gl*vib, 8)*arc
        p = rng.random(); L += x*p; R += x*(1-p)
bow = bp(pink(n), 500, 3000)*.5
L = bp(L+bow*lfo(t, .08, 0, .2), 90, 3000); R = bp(R+bow*lfo(t, .07, 0, .2), 90, 3000)
save('m_strings', loop(verb(st(L, R), 3.8, .45)))

# ── choir：三个共振峰扫过的“啊”，听起来是很远的人声，不成词 ──
L = np.zeros(n); R = np.zeros(n)
for base, g in ((110, 1), (116.54, .9), (164.81, .7), (155.56, .6)):
    src = sum(saw(base*(1+(v-2)*.004)*(1+.005*np.sin(2*np.pi*5.1*t+v)), 22) for v in range(5))
    arc = lfo(t, 1/(23+rng.random()*12), 0, 1)**2
    y = sum(bp(src, f*.9, f*1.1)*a for f, a in ((650, 1), (1080, .55), (2650, .22)))
    p = rng.random(); L += y*arc*g*p; R += y*arc*g*(1-p)
save('m_choir', loop(verb(st(L, R), 5, .7, 2000)))

# ── high：琴马边的尖细泛音，发抖，不稳定 ──
L = np.zeros(n); R = np.zeros(n)
for f0 in (2093, 2217, 3136, 3322, 4186):
    fr = f0*(1+.012*smooth_noise(n, .25)); trem = 1+.5*np.sin(2*np.pi*(7+rng.random()*3)*t)
    x = np.sin(2*np.pi*np.cumsum(fr)/SR)*trem*lfo(t, 1/(9+rng.random()*9), 0, 1)**4
    p = rng.random(); L += x*p; R += x*(1-p)
hiss = bp(pink(n), 5000, 11000)*lfo(t, .09, 0, .12)
save('m_high', loop(verb(st(L+hiss, R+hiss), 4, .55, 6000)), .6)

# ── pulse：像很远的机器，也像心跳，一下一下压过来，起音很软 ──
y = np.zeros(n); pos = 0.
while pos < DUR-3:
    i = int(pos*SR); m = int(2.2*SR); tt = np.arange(m)/SR
    hit = np.sin(2*np.pi*(46-10*tt/2.2)*tt)*np.minimum(tt/.05, 1)*np.exp(-tt*3.2)
    y[i:i+m] += hit[:len(y[i:i+m])]; pos += 1.42 + .08*rng.random()
save('m_pulse', loop(verb(st(y), 2.5, .35, 500)))

# ── swell：13 秒不协和上涌，给眼睛和熄灯的预兆用 ──
tt = t_(13); e = (tt/13)**2.6*np.minimum((13-tt)/1.2, 1); L = np.zeros(len(tt)); R = np.zeros(len(tt))
for f0 in (110, 116.5, 155.6, 164.8, 233.1, 246.9, 311.1):
    for v in range(4):
        x = saw(f0*(1+(v-1.5)*.005)*(1+.05*(tt/13)), 7)*e; p = rng.random(); L += x*p; R += x*(1-p)
save('swell', verb(st(lp(L, 2600), lp(R, 2600)), 3, .4), .7)

# ── 八音盒：原创小调旋律，音高发飘、越来越慢，放在远处 ──
notes = [(69, 1), (72, 1), (76, 2), (75, 1), (72, 1), (69, 2), (68, 1), (71, 1), (74, 2), (72, 1), (68, 1), (69, 4)]
out = np.zeros(int(19*SR)); pos = 0.; speed = .42
for i, (m, b) in enumerate(notes):
    f = 440*2**((m-69)/12)*(1+.006*rng.standard_normal())
    k = int(3.2*SR); q = np.arange(k)/SR
    tone = sum(a*np.sin(2*np.pi*f*r*q)*np.exp(-q*d) for r, a, d in ((1, 1, 2.4), (3.01, .4, 6), (5.4, .22, 11), (9.1, .1, 19)))
    j = int(pos*SR); out[j:j+k] += tone[:len(out[j:j+k])]; pos += b*speed; speed *= 1.035   # 发条在变松
save('musicbox', verb(st(out), 3.2, .6, 3200), .55)
