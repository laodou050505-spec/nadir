# 离线合成环境音（替换来源不明的素材库音频）。用法：python3 -I Tools/make_amb.py [名称...]
# 每条都按原素材的时长、响度和频段分布来做；循环类首尾交叉淡化，可无缝循环。
# 输出到 Audio/Amb（环境层）和 Audio/Sfx/end_dawn.wav（结局的鸟鸣和微风）。
import site, sys, pathlib
sys.path.append(site.getusersitepackages())
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from scipy.io import wavfile

SR = 44100
ROOT = pathlib.Path(__file__).resolve().parents[1]
MAIN = next(p.parent.parent.parent for p in (ROOT / 'Assets').rglob('Logic/Game.cs'))
import os
OUT = os.environ.get('AMB_OUT')                      # 试听时可以先输出到别的目录
AMB = pathlib.Path(OUT) if OUT else MAIN / 'Audio' / 'Amb'
SFX = pathlib.Path(OUT) if OUT else MAIN / 'Audio' / 'Sfx'
rng = np.random.default_rng(11)


def white(n):
    return rng.standard_normal(n)


def pink(n):
    f = np.fft.rfft(white(n))
    f[1:] /= np.sqrt(np.arange(1, len(f)))
    x = np.fft.irfft(f, n)
    return x / np.std(x)


def brown(n):
    f = np.fft.rfft(white(n))
    f[1:] /= np.arange(1, len(f))
    x = np.fft.irfft(f, n)
    return x / np.std(x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [max(lo, 10), min(hi, SR / 2 - 100)], 'band', fs=SR, output='sos'), x)


def lp(x, f, order=2):
    return sosfilt(butter(order, f, 'low', fs=SR, output='sos'), x)


def hp(x, f, order=2):
    return sosfilt(butter(order, f, 'high', fs=SR, output='sos'), x)


def slow(n, period, depth, seed):
    """缓慢起伏的包络：几条不同周期的正弦叠加，均值 1"""
    r = np.random.default_rng(seed)
    t = np.arange(n) / SR
    e = np.zeros(n)
    for k in range(3):
        p = period * (1 + k * .61)
        e += np.sin(2 * np.pi * t / p + r.uniform(0, 6.28)) / (k + 1)
    return 1 + depth * e / 1.83


def periodic_slow(n, cycles, depth, seed):
    """整数个周期的起伏，首尾相接（给循环用）"""
    r = np.random.default_rng(seed)
    t = np.arange(n) / n
    e = np.zeros(n)
    for k, c in enumerate(cycles):
        e += np.sin(2 * np.pi * c * t + r.uniform(0, 6.28)) / (k + 1)
    return 1 + depth * e / sum(1 / (k + 1) for k in range(len(cycles)))


def drone(freqs, n, detune=.15, seed=0):
    """一组正弦，各自带一点点缓慢的音高漂移和明暗"""
    r = np.random.default_rng(seed)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for f in freqs:
        for d in (-detune, detune):
            drift = .3 * np.sin(2 * np.pi * t / r.uniform(17, 31) + r.uniform(0, 6))
            ph = 2 * np.pi * np.cumsum(f + d + drift) / SR
            out += np.sin(ph) * slow(n, r.uniform(9, 16), .5, r.integers(1 << 30)) / len(freqs)
    return out


def verb(x, sec=2.5, mix=.35, seed=3):
    r = np.random.default_rng(seed)
    m = int(sec * SR)
    ir = r.standard_normal(m) * np.exp(-6.9 * np.arange(m) / m)
    ir = lp(ir, 5000)
    ir /= np.sqrt(np.sum(ir ** 2))
    w = fftconvolve(x, ir)[:len(x)]
    return x * (1 - mix) + w * mix


def loopify(x, xf):
    """把多做的 xf 秒尾巴交叉淡化到开头，得到可无缝循环的片段"""
    k = int(xf * SR)
    body, tail = x[:-k].copy(), x[-k:]
    a = np.linspace(0, 1, k)
    body[:k] = body[:k] * np.sqrt(a) + tail * np.sqrt(1 - a)
    return body


def fit_rms(x, rms, peak=.95, cut=30):
    if cut: x = hp(x, cut, 4)                           # 去掉耳机放不出来、只会吃掉余量的超低频
    for _ in range(4):                                   # 对齐响度；极少数尖峰用软限幅压住，再重新对齐
        x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * rms
        if np.max(np.abs(x)) <= peak: break
        x = np.tanh(x / peak) * peak
    return x


def save(path, x):
    path.parent.mkdir(parents=True, exist_ok=True)
    wavfile.write(path, SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
    print(f'{path.name:16s} {len(x) / SR:5.1f}s rms={np.sqrt(np.mean(x ** 2)):.3f} peak={np.max(np.abs(x)):.2f}')


# ── 普通模式底层：低沉的空间感，低频持续音 + 中频空气声缓慢涨落 ──
def void_dread():
    dur, xf = 64, 6
    n = int((dur + xf) * SR)
    low = drone([41.2, 55, 61.7], n, .2, 1) * .9
    air = bp(pink(n), 250, 900) * slow(n, 23, .7, 2) * 2.2
    mid = bp(brown(n), 90, 240) * slow(n, 17, .5, 3) * 2.4
    # 偶尔远处一声很轻的金属共鸣（像管道在热胀冷缩），起音很慢
    t = np.arange(n) / SR
    ring = np.zeros(n)
    for at in (11, 29, 47, 61):
        e = np.clip((t - at) / 2.5, 0, 1) ** 2 * np.exp(-np.clip(t - at - 2.5, 0, None) / 4)
        ring += np.sin(2 * np.pi * 311 * t + np.sin(2 * np.pi * .7 * t)) * e * .35
    x = verb(low + air + mid + ring, 3.5, .4)
    return fit_rms(loopify(x, xf), .091)


# ── 困难模式底层：更压抑，集中在 80–250 Hz 的低音提琴般的不协和持续音 ──
def bleak():
    dur, xf = 59, 6
    n = int((dur + xf) * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    r = np.random.default_rng(5)
    for f in (98, 103.8, 146.8, 155.6):         # 小二度相撞，慢慢拍动
        ph = 2 * np.pi * np.cumsum(f + .4 * np.sin(2 * np.pi * t / r.uniform(13, 27))) / SR
        saw = sum(np.sin(k * ph) / k for k in range(1, 9))
        out += saw * slow(n, r.uniform(10, 19), .6, r.integers(1 << 30))
    out = lp(out, 420, 4)
    sub = drone([49, 51.9], n, .1, 6) * .25
    x = verb(out * .35 + sub + bp(brown(n), 60, 200) * .3 * slow(n, 21, .5, 7), 4, .45)
    return fit_rms(loopify(x, xf), .124)


# ── 困难模式第二层：次低频轰鸣（25–60 Hz），像大楼深处的机器 ──
def atmos_sub():
    dur, xf = 75, 6
    n = int((dur + xf) * SR)
    x = drone([27.5, 32.7, 36.7], n, .25, 8) * slow(n, 14, .55, 9)
    x += lp(brown(n), 55, 4) * .6 * slow(n, 19, .6, 10)
    x += bp(brown(n), 60, 120) * .08
    return fit_rms(loopify(x, xf), .17, cut=18)


# ── 普通模式第二层：低频底 + 玻璃般的泛音慢慢浮现、消失 ──
def harmonic():
    dur, xf = 66, 6
    n = int((dur + xf) * SR)
    t = np.arange(n) / SR
    low = drone([36.7, 55], n, .2, 12) * 1.1
    gl = np.zeros(n)
    r = np.random.default_rng(13)
    for f in (293.7, 440, 587.3, 659.3, 880):
        e = np.clip(slow(n, r.uniform(15, 30), 1.4, r.integers(1 << 30)) - .9, 0, None) ** 1.5
        trem = 1 + .25 * np.sin(2 * np.pi * r.uniform(.15, .4) * t)
        gl += np.sin(2 * np.pi * f * t + 2 * np.sin(2 * np.pi * .05 * t)) * e * trem
    gl = gl / (np.max(np.abs(gl)) + 1e-9)
    x = verb(low * .6 + gl * 1.3 + bp(pink(n), 300, 1200) * .5 + bp(brown(n), 90, 240) * .4, 4, .5)
    return fit_rms(loopify(x, xf), .068)


# ── 风：低沉的风声，阵风起伏；5 秒无缝循环 ──
def wind():
    dur = 5
    n = int(dur * SR)
    x = np.fft.irfft(np.fft.rfft(pink(n)), n)      # 先做成首尾自然相接的周期噪声
    f = np.fft.rfft(x)
    fr = np.fft.rfftfreq(n, 1 / SR)
    f *= 1 / (1 + (fr / 420) ** 2) * (fr > 70)
    x = np.fft.irfft(f, n)
    x *= periodic_slow(n, (1, 2, 3), .45, 14)
    return fit_rms(x, .2, .99)


# ── 地砖开裂时的木头吱呀：粘滑摩擦（一串间隔变化的小脉冲）通过几个木头共振 ──
def creak():
    dur = 3.1
    n = int(dur * SR)
    t = np.arange(n) / SR
    rate = 35 + 55 * (np.sin(2 * np.pi * t / 2.2 + .5) * .5 + .5) + 15 * np.sin(2 * np.pi * 1.3 * t)
    ph = np.cumsum(rate) / SR
    pulses = np.diff(np.floor(ph), prepend=0)
    pulses = pulses * (1 + .5 * white(n))
    env = np.clip(t / .25, 0, 1) * np.clip((dur - t) / .4, 0, 1) * (.6 + .4 * np.sin(2 * np.pi * t / 1.7) ** 2)
    x = pulses * env
    y = bp(x, 600, 850, 2) * 1.0 + bp(x, 1300, 1700, 2) * .9 + bp(x, 2500, 3200, 2) * .6 + bp(x, 90, 260) * 1.2
    y = lp(y, 5000, 4)
    return fit_rms(verb(y, .5, .2), .039, .83)


# ── 碎石：一长串小石块、碎屑落下的声音（游戏里随机截取 1.3 秒用）──
def rocks():
    dur = 21.5
    n = int(dur * SR)
    x = np.zeros(n)
    r = np.random.default_rng(15)
    t = 0.1
    while t < dur - .3:
        dens = .5 + .5 * np.sin(t / 3.1) ** 2
        k = int(t * SR)
        m = int(r.uniform(.02, .12) * SR)
        f0 = r.uniform(300, 1400)
        g = r.uniform(.2, 1) ** 2
        hit = bp(white(m), f0 * .6, f0 * 1.4) * np.exp(-np.arange(m) / (m * .25)) * g
        x[k:k + m] += hit[:max(0, min(m, n - k))]
        if r.random() < .15:                            # 偶尔一块大一点的，低频闷响
            mm = int(.25 * SR)
            th = bp(white(mm), 120, 400) * np.exp(-np.arange(mm) / (mm * .2)) * r.uniform(.3, .8)
            x[k:k + mm] += th[:max(0, min(mm, n - k))]
        t += r.exponential(.09 / dens)
    x += bp(pink(n), 300, 2500) * .04
    return fit_rms(verb(x, 1.2, .3), .032, .41)


# ── 脚步：远处的两三步，鞋底闷响加一点摩擦 ──
def footsteps():
    dur = 1.72
    n = int(dur * SR)
    x = np.zeros(n)
    r = np.random.default_rng(16)
    for i, at in enumerate((.08, .62, 1.16)):
        k = int(at * SR)
        m = int(.28 * SR)
        a = np.arange(m) / SR
        thump = np.sin(2 * np.pi * (95 + 40 * np.exp(-a * 30)) * a) * np.exp(-a * 22)
        heel = bp(white(m), 150, 600) * np.exp(-a * 35) * 2.2
        scuff = bp(white(m), 400, 1400) * np.exp(-((a - .07) / .04) ** 2) * 1.6
        s = (thump + heel + scuff) * r.uniform(.7, 1)
        x[k:k + m] += s[:max(0, min(m, n - k))]
    return fit_rms(verb(x, 1.5, .35), .049, .5)


# ── “有东西在”：近乎耳语的气声，在 1–4 kHz 里慢慢移动，起音很慢，不吓人 ──
def presence():
    dur = 5.9
    n = int(dur * SR)
    t = np.arange(n) / SR
    src = pink(n)
    y = np.zeros(n)
    for f0, f1, g in ((1300, 1700, 1), (2300, 2700, .8), (3200, 3600, .5)):   # 三个随时间漂移的“口型”共振峰
        seg = 2048
        for i in range(0, n, seg):
            c = (f0 + f1) / 2 + (f1 - f0) / 2 * np.sin(2 * np.pi * (i / SR) / 2.3 + f0)
            s = bp(src[i:i + seg + 512], c * .85, c * 1.15)
            w = min(seg, n - i)
            y[i:i + w] += s[:w] * g
    env = np.clip(t / 1.6, 0, 1) ** 2 * np.clip((dur - t) / 1.8, 0, 1) * (.7 + .3 * np.sin(2 * np.pi * t / .9) ** 2)
    return fit_rms(verb(y * env, 2.2, .45), .076, .4)


# ── 结局：清晨的鸟鸣 + 微风，46.5 秒循环 ──
def bird_note(kind, r):
    """一声鸟叫：kind 决定形状（上挑、下滑、颤音、双音）"""
    if kind == 'tweet':
        d = r.uniform(.07, .14)
        n = int(d * SR)
        a = np.linspace(0, 1, n)
        f = r.uniform(3000, 4200) + r.uniform(800, 1600) * np.sin(np.pi * a)
    elif kind == 'down':
        d = r.uniform(.12, .22)
        n = int(d * SR)
        a = np.linspace(0, 1, n)
        f = r.uniform(4500, 5600) - r.uniform(1500, 2400) * a ** .7
    elif kind == 'trill':
        d = r.uniform(.35, .7)
        n = int(d * SR)
        a = np.arange(n) / SR
        base = r.uniform(3200, 4400)
        f = base + 700 * np.sin(2 * np.pi * r.uniform(18, 26) * a)
    else:   # 'two'：两个音高的短句，像“啾-咿”
        d = r.uniform(.25, .35)
        n = int(d * SR)
        a = np.linspace(0, 1, n)
        f = np.where(a < .45, r.uniform(2600, 3000), r.uniform(3600, 4300)) + 300 * np.sin(np.pi * a)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) + .18 * np.sin(2 * ph) + .05 * np.sin(3 * ph)
    m = len(x)
    e = np.sin(np.pi * np.linspace(0, 1, m)) ** 1.5
    if kind == 'trill':
        e *= .6 + .4 * np.sin(2 * np.pi * 22 * np.arange(m) / SR) ** 2
    return x * e


def end_dawn():
    dur, xf = 46.5, 4
    n = int((dur + xf) * SR)
    r = np.random.default_rng(21)
    birds = np.zeros(n)
    # 三只“不同的鸟”，各有自己的叫法、远近和节奏
    voices = [(['tweet', 'tweet', 'two'], .9, 3.2, 1.0), (['down', 'trill'], .55, 5.5, .97), (['two', 'tweet', 'down'], .35, 7, 1.08)]
    for kinds, gain, gap, pitch in voices:
        t = r.uniform(.5, 3)
        while t < dur + xf - 1:
            phrase = r.integers(1, 4)
            for _ in range(phrase):
                note = bird_note(kinds[r.integers(len(kinds))], r) * gain * r.uniform(.6, 1)
                if pitch != 1:
                    idx = np.arange(0, len(note) - 1, pitch)
                    note = np.interp(idx, np.arange(len(note)), note)
                k = int(t * SR)
                birds[k:k + len(note)] += note[:max(0, min(len(note), n - k))]
                t += len(note) / SR + r.uniform(.06, .2)
            t += r.exponential(gap)
    birds = verb(hp(birds, 1500), 1.8, .3)               # 在开阔处、有点距离
    breeze = bp(pink(n), 250, 1100) * slow(n, 7, .6, 22) * 1.0 + bp(pink(n), 80, 250) * .9 * slow(n, 11, .5, 23)
    x = birds * .9 + breeze
    return fit_rms(loopify(x, xf), .073, .8)


JOBS = {
    'void_dread': (void_dread, AMB / 'void_dread.wav'),
    'bleak': (bleak, AMB / 'bleak.wav'),
    'atmos_sub': (atmos_sub, AMB / 'atmos_sub.wav'),
    'harmonic': (harmonic, AMB / 'harmonic.wav'),
    'wind': (wind, AMB / 'wind.wav'),
    'creak': (creak, AMB / 'creak.wav'),
    'rocks': (rocks, AMB / 'rocks.wav'),
    'footsteps': (footsteps, AMB / 'footsteps.wav'),
    'presence': (presence, AMB / 'presence.wav'),
    'end_dawn': (end_dawn, SFX / 'end_dawn.wav'),
}

if __name__ == '__main__':
    for name in (sys.argv[1:] or JOBS):
        fn, path = JOBS[name]
        save(path, fn())
