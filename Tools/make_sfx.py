# 离线合成全部一次性音效和环境循环。用法：python3 -I Tools/make_sfx.py
# 原则：不用尖叫、重击；起音都放缓；恐惧靠低频、距离和混响
import site, sys, pathlib, wave
sys.path.append(site.getusersitepackages())
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
SR = 44100
OUT = pathlib.Path(__file__).resolve().parents[1]/'Assets/_DLD/Audio/Sfx'; OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(7)

def env(n, attack, gain):
    t = np.arange(n)/SR; a = max(attack, .001); d = n/SR
    return gain*np.minimum(t/a, 1)**1.5*np.exp(-5.5*np.maximum(t-a, 0)/max(d-a, .001))

def white(n): return rng.standard_normal(n)
def pink(n):
    f = np.fft.rfft(white(n)); f[1:] /= np.sqrt(np.arange(1, len(f))); x = np.fft.irfft(f, n); return x/np.std(x)

def band(x, lo, hi):
    lo = max(lo, 15); hi = min(hi, SR/2-100)
    return sosfilt(butter(2, [lo, hi], 'band', fs=SR, output='sos'), x)

def noise(dur, freq, q=1., gain=.3, attack=.003, sweep=None, pink_=False, kind='band'):
    n = int(dur*SR); x = pink(n) if pink_ else white(n)
    if sweep:   # 分段滤波做扫频
        out = np.zeros(n); seg = 2048
        fs = np.geomspace(freq, sweep, n//seg+2)
        for i in range(0, n, seg):
            f = fs[i//seg]; bw = f/q
            s = sosfilt(butter(2, [max(f-bw/2, 20), min(f+bw/2, 20000)], 'band', fs=SR, output='sos'), x[i:i+seg+512])
            w = min(seg, n-i); out[i:i+w] = s[:w]
        x = out
    elif kind == 'low': x = sosfilt(butter(2, freq, 'low', fs=SR, output='sos'), x)
    elif kind == 'high': x = sosfilt(butter(2, freq, 'high', fs=SR, output='sos'), x)
    else: bw = freq/q; x = band(x, freq-bw/2, freq+bw/2)
    x = x/(np.max(np.abs(x))+1e-6)
    return x*env(n, attack, gain)

def tone(f0, f1, dur, gain=.3, attack=.005, wave_='sine'):
    n = int(dur*SR); f = np.geomspace(f0, max(f1, 20), n); ph = 2*np.pi*np.cumsum(f)/SR
    x = {'sine': np.sin(ph), 'tri': 2/np.pi*np.arcsin(np.sin(ph)), 'saw': 2*(ph/(2*np.pi) % 1)-1}[wave_]
    return x*env(n, attack, gain)

def mix(*items, tail=0.):
    """items: (信号, 延迟秒)"""
    n = max(int(d*SR)+len(x) for x, d in items)+int(tail*SR); o = np.zeros(n)
    for x, d in items: i = int(d*SR); o[i:i+len(x)] += x
    return o

def verb(x, t60=2.2, wet=.35, lp=2400):
    n = int(t60*SR); ir = white(n)*np.exp(-6.9*np.arange(n)/n)
    ir = sosfilt(butter(1, lp, 'low', fs=SR, output='sos'), ir); ir /= np.sqrt(np.sum(ir**2))
    y = fftconvolve(np.concatenate((x, np.zeros(n))), ir)[:len(x)+n]
    d = np.concatenate((x, np.zeros(n)))
    return d*(1-wet)+y*wet

def save(name, x, peak=.8):
    x = x/(np.max(np.abs(x))+1e-6)*peak
    fade = min(int(.02*SR), len(x)//4); x[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(str(OUT/f'{name}.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(x, -1, 1)*32767).astype(np.int16).tobytes())

def loop(x, xf=1.5):
    """首尾交叉淡化，循环无缝"""
    k = int(xf*SR); a = x[:k]; b = x[-k:]; w = np.linspace(0, 1, k)
    y = x[:-k].copy(); y[:k] = b*(1-w)+a*w
    return y

# ── 地板 ──
for i in range(3):
    ticks = [(noise(.05+rng.random()*.03, 2200+rng.random()*1800, 5, .5), .02+k*.07+rng.random()*.03) for k in range(5)]
    save(f'crack{i}', verb(mix((tone(170+i*20, 110, .14, .3, .004, 'tri'), 0), (noise(.5, 700, .8, .25, .01, pink_=True), 0), *ticks), 1.0, .25), .7)
for i in range(3):
    body = mix((noise(1.3, 900, .7, .5, .04, sweep=130), 0), (tone(105, 44, .6, .4, .05), 0), (noise(.25, 1800, 1, .35, .005), .02), (noise(.18, 2600, 1.2, .25, .004), .16+i*.05))
    save(f'fall{i}', verb(body, 2.4, .4), .8)
save('fall_echo', verb(mix((noise(1.2, 160, 1, .6, .05, kind='low', pink_=True), 0), (tone(58, 40, 1, .5, .04), 0)), 3.6, .7, 900), .5)
save('rise', mix((noise(1.1, 200, 1, .5, .45, sweep=900), 0), (tone(70, 110, 1.1, .35, .5), 0)), .5)
save('restore', mix((tone(330, 660, .45, .3, .03), 0), (noise(.4, 600, 1, .3, .05, sweep=2000), 0)), .4)
save('stay', mix((tone(240, 230, .09, .5, .004, 'tri'), 0), (tone(240, 230, .09, .5, .004, 'tri'), .17)), .3)
# ── 碎片 ──
save('shard_spawn', verb(mix(*[(tone(f, f*.995, 1.4, .3, .02), i*.12) for i, f in enumerate((1318, 1760, 2637))]), 1.8, .5, 5000), .3)
save('shard_get', verb(mix(*[(tone(f, f, .6, .4, .006), i*.05) for i, f in enumerate((1046, 1318, 1568, 2093))]), 1.4, .4, 6000), .5)
save('shard_fade', verb(tone(1568, 700, .7, .4, .02), 1.2, .5), .3)
save('gaze_lock', tone(880, 1320, .3, .5, .02), .2)
# ── 传送门 ──
save('portal_open', verb(mix((noise(2.2, 400, 2, .5, .6, sweep=1600), 0), *[(tone(f, f, 2.6, .25, .5), .3+i*.25) for i, f in enumerate((262, 392, 523))]), 2.5, .45), .6)
n = int(9*SR); t = np.arange(n)/SR
pad = sum(np.sin(2*np.pi*f*t+rng.random()*6)*g for f, g in ((131, .5), (196.4, .35), (262, .25), (393, .1)))*(1+.25*np.sin(2*np.pi*.17*t))
save('portal_loop', loop(pad+band(pink(n), 600, 3200)*.12), .5)
save('clear', verb(mix(*[(tone(f, f, 1.8, .3, .2), i*.12) for i, f in enumerate((392, 523, 659, 784, 1046))], (noise(2.6, 3000, 1, .15, .8), 0)), 3, .5, 5000), .5)
save('teleport', verb(mix((noise(2.6, 300, 1.2, .6, 1.2, sweep=4200, pink_=True), 0), *[(tone(f, f*1.003, 3.2, .3, 1.2), i*.12) for i, f in enumerate((196, 262, 392, 523, 784))], (tone(70, 140, 2.4, .4, 1)  , 0)), 3.2, .5, 5000), .75)
n = int(4*SR); cr = band(white(n), 1400, 6000)*np.clip(smooth if False else (rng.random(n) > .985)*1., 0, 1)
cr = sosfilt(butter(2, 3000, 'low', fs=SR, output='sos'), cr)*3
save('tear_loop', loop(cr+band(pink(n), 300, 1600)*.55, .4), .5)
save('tear_crack', verb(mix((noise(.5, 1800, 1, .4, .02, sweep=500, pink_=True), 0), (tone(240, 90, .5, .25, .03), 0)), 1.6, .4), .5)
# ── 触手 ──
# 预警要够响、够长、够低：地下有东西在往上蹭，一听就知道要来了
_n = int(2.9*SR); _t = np.arange(_n)/SR; _rise = np.minimum(_t/2.4, 1)**1.4
_sc = band(pink(_n), 180, 1100)*(.45 + .55*np.abs(np.sin(2*np.pi*(1.7 + _t*.9)*_t)))
save('tentacle_omen', verb(mix((_sc*_rise*.8, 0), (np.sin(2*np.pi*np.cumsum(46 + 26*_t/2.9)/SR)*_rise*.75, 0), *[(noise(.2, 500+i*120, 4, .12+i*.05, .02, pink_=True), .5+i*.42) for i in range(5)]), 1.6, .3, 1600), .78)
save('tentacle_back', verb(mix((noise(.65, 900, 1.2, .55, .03, sweep=120, pink_=True), 0), (tone(120, 40, .7, .5, .02), 0)), 1.5, .35, 1800), .6)
# 黑影：赤脚踩在水磨石上的闷响 + 一点布料拖过。不用皮鞋声，它不是人
for _i in range(4):
    _q = rng.random()
    save(f'fig_step{_i}', verb(mix((tone(92 + _q*22, 44, .3, .95, .006), 0), (noise(.14, 320 + _q*160, .8, .5, .004, pink_=True), 0), (noise(.55, 1300 + _q*700, .9, .13, .1, sweep=450, pink_=True), .07)), .9, .3, 1800), .78)
# 黑影走近：很低的一口气，起音很慢，不吓人
_n = int(2.6*SR); _t = np.arange(_n)/SR; _e = np.sin(np.pi*np.minimum(_t/2.6, 1))**1.6
save('fig_near', verb((band(pink(_n), 130, 620)*_e*.8 + np.sin(2*np.pi*58*_t)*_e*.5), 1.8, .35, 1500), .72)
n = int(1.6*SR); fib = band(pink(n), 500, 2400)*(.5+.5*np.sin(2*np.pi*(5+rng.random()*3)*np.arange(n)/SR))
save('tentacle_rise', verb(mix((fib*env(n, .5, .5), 0), (noise(1.4, 260, 1, .4, .4, sweep=1000, pink_=True), 0)), 1.4, .35), .55)
save('tentacle_lunge', mix((noise(.4, 500, 1.5, .5, .1, sweep=1800, pink_=True), 0)), .35)
n = int(8*SR); m = .5+.5*np.sin(2*np.pi*1.3*np.arange(n)/SR+1.5*np.sin(2*np.pi*.31*np.arange(n)/SR))
save('tentacle_loop', loop(band(pink(n), 300, 1800)*m**2), .4)
save('grab', mix((tone(80, 60, 1.1, .5, .3), 0), (noise(.9, 300, 1, .3, .3, pink_=True), 0)), .55)
for i in range(2): save(f'swat{i}', mix((noise(.09, 1100+i*300, .7, .6, .002), 0), (tone(170, 70, .13, .5, .003), 0)), .75)
save('swat_kill', verb(noise(.7, 500, 1, .4, .02, sweep=80), 1.5, .4), .45)
# ── 眼睛 ──
save('eye_warn', mix((tone(36, 30, 4.6, .6, 2.6, 'saw'), 0), (noise(4.6, 120, 1, .7, 2.8, kind='low', pink_=True), 0)), .7)
save('eye_open', verb(mix((noise(1.1, 200, 2, .6, .3, sweep=60, pink_=True), 0), (tone(55, 52, 1.6, .6, .35), 0)), 2.5, .4, 700), .7)
save('eye_close', noise(.7, 300, 1, .5, .1, sweep=80), .35)
save('seen', tone(600, 1400, .9, .4, .4), .14)
save('spotted', mix((tone(55, 40, 3.2, .7, 1.1), 0), (noise(3.2, 140, 1, .7, 1.3, kind='low', pink_=True), 0)), .75)
# ── 惊吓（都是远处、轻的） ──
save('whisper', verb(mix(*[(noise(.17+rng.random()*.2, 900+rng.random()*1500, 7, .1+i*.05, .03), i*.22+rng.random()*.05) for i in range(7)]), 1.1, .35), .32)
save('omen_whisper', noise(2.4, 900, 3, .4, 1.4, pink_=True), .18)
save('knock', verb(mix(*[(mix((noise(.09, 320, 1, .5, .004, kind='low'), 0), (tone(110, 60, .12, .4, .004), 0)), d) for d in (0, .34, .62)]), 1.3, .35, 900), .5)
save('omen_knock', noise(.09, 260, 1, .5, .005, kind='low'), .22)
save('beep', mix(*[(tone(880, 880, .14, .3+i*.1, .006), d) for i, d in enumerate((0, 1.4, 2.8, 4.2))]), .14)
save('omen_beep', tone(880, 880, .12, .5, .006), .08)
save('steps_end', noise(1.3, 700, 1, .5, .5, sweep=300, pink_=True), .35)
save('blackout', mix((tone(90, 60, 1.7, .5, .6), 0)), .35)
save('omen_black', tone(120, 90, 4.1, .5, 1.9), .3)
# 坠落更久时垫一层持续的风声，长度对应普通 / 困难的坠落时长，结尾已经淡到很小
for nm, du in (('fall_mid', 3.0), ('fall_long', 4.2)):
    x_ = mix((noise(du, 300, .5, .5, .9, sweep=1100, pink_=True), 0), (noise(du, 90, .6, .35, 1.1, sweep=170, pink_=True), 0))
    x_ *= np.minimum(np.linspace(0, 6, len(x_)), 1)*np.minimum(np.linspace(4, 0, len(x_)), 1)
    save(nm, x_, .5)
# 灯管接触不良：一声干脆的电弧咔嗒
save('lamp_tick', verb(mix((noise(.05, 3200, 1.5, .8, .001), 0), (noise(.09, 1100, 2, .35, .002), .018), (tone(100, 100, .12, .22, .002, 'saw'), 0)), .5, .18, 5000), .5)
# 巨眼被拍回井里：不叫，是一个很大的东西猛地沉下去
save('eye_recoil', verb(mix((tone(70, 28, 1.3, .9, .01), 0), (noise(1.1, 620, .8, .6, .015, sweep=75, pink_=True), 0)), 2.6, .45, 1500), .72)
# 施法：一口气吸进来，再一股气浪推出去，尾巴是冷的玻璃泛音。玩家自己出手，起音不算吓人
save('cast', verb(mix((noise(.22, 700, 1.1, .45, .12, sweep=2600, pink_=True), 0), (noise(.8, 2400, .8, .5, .012, sweep=170, pink_=True), .17), (tone(120, 44, .7, .55, .02), .17),
    (tone(1760, 1690, 1.1, .08, .02), .19), (tone(2637, 2590, .9, .05, .02), .2)), 1.8, .38, 4200), .62)
# 界面：很轻的玻璃轻响，都不抢恐怖氛围
save('ui_tick', verb(tone(1975, 1900, .07, .3, .002), .5, .2, 5000), .28)
save('ui_press', verb(mix((tone(620, 410, .16, .4, .003), 0), (tone(1244, 1200, .4, .15, .004), .012)), 1.1, .3, 3800), .42)
save('ui_begin', verb(mix((noise(1.1, 300, .7, .4, .35, sweep=70, pink_=True), 0), (tone(110, 55, 1.2, .5, .25), 0), (tone(880, 840, 1.0, .05, .3), .1)), 2.6, .45, 2200), .55)
save('figure_push', verb(mix((noise(.7, 900, .9, .6, .06, sweep=140, pink_=True), 0), (tone(95, 42, .75, .45, .08), 0)), 1.6, .35, 900), .6)
# ── 玩家 ──
save('fall_me', noise(2.2, 1400, .5, .5, .5, sweep=150), .5)
save('wake', mix((tone(196, 392, 1.3, .4, .3), 0), (noise(1, 3000, 1, .1, .4, kind='high'), 0)), .35)
save('lost', mix(*[(tone(f, f*.98, 1.1, .4, .1, 'tri'), i*.4) for i, f in enumerate((220, 207, 196, 185))]), .4)
save('oob', mix((tone(660, 660, .15, .5, .006), 0), (tone(660, 660, .15, .5, .006), .25)), .3)
save('heart', mix((tone(60, 40, .15, .7, .01), 0), (tone(55, 38, .13, .5, .01), .22)), .8)
# ── 环境循环 ──
n = int(11*SR); t = np.arange(n)/SR
hum = sum(np.sin(2*np.pi*f*t)*g for f, g in ((100, .5), (200, .25), (300, .12), (1200, .02)))+band(white(n), 3000, 7000)*.012
save('hum_loop', loop(hum*(1+.06*np.sin(2*np.pi*.13*t))), .4)
n = int(16*SR); t = np.arange(n)/SR
wind = band(pink(n), 40, 260)*(1+.4*np.sin(2*np.pi*.11*t))+band(pink(n), 300, 900)*.1*(1+.8*np.sin(2*np.pi*.07*t+1))
save('abyss_loop', loop(wind+np.sin(2*np.pi*43*t)*.25, 2.5), .7)
print('ok', len(list(OUT.glob('*.wav'))))
