# 第二版材质处理：2K 无缝 + 多尺度强法线 + 遮罩（R 光泽，G 缝隙暗部）。用法：python3 -I Tools/process_tex3.py [名称...]
import site, sys, pathlib
sys.path.append(site.getusersitepackages())
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, sobel
R = pathlib.Path(__file__).resolve().parents[1]
SRC = R/'ArtSource/TexV2'; DST = R/'Assets/_DLD/Art/Textures'
SIZE = 2048

def tile(a):
    """和错开半张的自己交叉淡化做无缝；大尺度明暗只压一半，老化的层次要留下"""
    n = a.shape[0]
    b = gaussian_filter(a.mean(2), n/7, mode='reflect')[..., None] + 1e-3
    a = a * (b.mean()/b)**.5
    # 分两轴融合：每次只错开一个方向，权重只看这个方向到边的距离。
    # 同时错开两个方向时，错开图的十字接缝会和原图的边相交，留下硬边
    x = np.arange(n, dtype=np.float32)
    w = np.clip((np.minimum(x, n-1-x)/(n/2) - .02)/.46, 0, 1); w = w*w*(3-2*w)
    for axis in (1, 0):
        s = np.roll(a, n//2, axis)
        k = w[None, :, None] if axis == 1 else w[:, None, None]
        a = a*k + s*(1-k)
    return a

def period(p):
    p = p-p.mean(); f = np.abs(np.fft.rfft(p)); k = 4+int(np.argmax(f[4:len(f)//2])); return len(p)/k

def grid(a, count):
    """网格贴图：裁成整数格再缩放，平铺才对缝"""
    l = a.mean(2); T = max(period(l.mean(0)), period(l.mean(1)))
    def first(p):
        return min(range(int(T)), key=lambda o: np.mean([p[int(o+i*T)] for i in range(int(len(p)/T)-1)]))
    x0, y0 = first(l.mean(0)), first(l.mean(1))
    n = min(count, int((a.shape[1]-x0)/T), int((a.shape[0]-y0)/T))
    c = a[y0:int(y0+n*T), x0:int(x0+n*T)]
    print('  网格周期', round(T, 1), '格数', n)
    return np.asarray(Image.fromarray(np.clip(c, 0, 255).astype(np.uint8)).resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32), n

def maps(a, name, fine, broad, gloss_lo, gloss_hi):
    l = a.mean(2)/255
    hf = l - gaussian_filter(l, 2.0, mode='wrap')
    mf = gaussian_filter(l, 2.0, mode='wrap') - gaussian_filter(l, 14, mode='wrap')
    lf = gaussian_filter(l, 14, mode='wrap') - gaussian_filter(l, 70, mode='wrap')
    h = hf*fine + mf*broad + lf*broad*.5
    dx = sobel(h, axis=1, mode='wrap')/8; dy = sobel(h, axis=0, mode='wrap')/8
    k = 13
    n = np.dstack((-dx*k, dy*k, np.ones_like(l))); n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n*.5+.5)*255).astype(np.uint8)).save(DST/f'{name}_n.png')
    # 光泽：缝隙和掉漆处哑，表面完好处亮一点；再叠一层低频，一片亮一片哑
    cav = np.clip(1 + (hf*2.2 + mf*3.0).clip(-1, 0)*2.4, 0, 1)
    tone = np.clip(.5 + lf*7, 0, 1)
    gloss = np.clip(gloss_lo + (gloss_hi-gloss_lo)*tone*(.35+.65*np.clip(1-np.abs(mf)*14, 0, 1)), 0, 1)
    Image.fromarray((np.dstack((gloss, cav, np.zeros_like(l)))*255).astype(np.uint8)).save(DST/f'{name}_m.png')

SPEC = {  # 名称: (细节法线, 中频法线, 光泽下限, 光泽上限, 网格数)
    'wall_paint': (1.0, 2.2, .04, .4, 0), 'terrazzo': (.45, 1.1, .08, .85, 0), 'concrete': (1.3, 2.8, .0, .15, 0),
    'ceiling': (.9, 2.2, .02, .22, 0), 'tentacle3': (.9, 1.8, .35, .9, 0), 'door_wood': (1.0, 1.8, .12, .6, 0), 'steel': (1.0, 1.6, .1, .75, 0), 'wall_tile': (.7, 1.6, .22, .95, 8),
}
for name in (sys.argv[1:] or SPEC):
    f, b, lo, hi, g = SPEC[name]
    a = np.asarray(Image.open(SRC/f'{name}.png').convert('RGB').resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32)
    if g: a, _ = grid(a, g)
    else: a = tile(a)
    a = np.clip(a, 0, 255)
    Image.fromarray(a.astype(np.uint8)).save(DST/f'{name}.png')
    maps(a, name, f, b, lo, hi)
    print('ok', name)
