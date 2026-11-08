# 封面合成：调色、压黑、排标题，导出商店封面和应用图标。用法：python3 -I Tools/make_cover.py
# 底图 ArtSource/Cover/base.png（Tripo banana2 生成），字体 Noto Serif/Sans SC（OFL）
import site, sys, pathlib
sys.path.append(site.getusersitepackages())
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
R = pathlib.Path(__file__).resolve().parents[1]; OUT = R/'Docs/封面'; OUT.mkdir(exist_ok=True); ART = R/'Assets/_DLD/Art/Cover'; ART.mkdir(exist_ok=True)
SERIF = str(R/'ArtSource/Fonts/NotoSerifSC-SemiBold.otf'); SANS = str(R/'ArtSource/Fonts/NotoSansSC-Medium.otf')
BONE = (232, 226, 212); DIM = (168, 164, 154); rng = np.random.default_rng(3)

base = Image.open(R/'ArtSource/Cover/base.png').convert('RGB')
bw, bh = base.size; FIG = (.5003, .6488)       # 人物在底图上的位置

def grade(crop, size, eye_uv):
    """把雾压下去：黑场抬回真正的黑，四周和底部沉进暗部，只留中间一条窄窄的亮度和一点琥珀"""
    im = np.asarray(base.crop(crop).resize(size, Image.LANCZOS)).astype(np.float32)/255
    h, w, _ = im.shape; y, x = np.mgrid[0:h, 0:w]; u = x/w; v = y/h
    lum = im[..., 0]*.2126 + im[..., 1]*.7152 + im[..., 2]*.0722; warm = np.clip((im[..., 0] - im[..., 2])*6, 0, 1)
    fu = (FIG[0]*bw - crop[0])/(crop[2] - crop[0]); fv = (FIG[1]*bh - crop[1])/(crop[3] - crop[1]); scale = bh/(crop[3] - crop[1])
    near = np.exp(-(((u - fu)*w/h)**2 + (v - fv)**2)/(.02*scale)**2); warm *= 1 - near     # 人物不留红头
    grey = lum[..., None]*np.array([.92, 1.0, .97])
    im = grey*(1 - warm[..., None]) + im*warm[..., None]
    raw = grey.copy(); im = np.clip((im - .1)/.8, 0, 1)**1.38
    im = im*(1 - near[..., None]) + raw*.72*near[..., None]     # 人物不压黑，留住衣服和头顶的细节
    asp = w/h; du = (u - eye_uv[0])*asp/max(asp, 1); dv = v - eye_uv[1]
    r = np.sqrt(du**2 + dv**2)
    vig = np.clip(1.16 - (r/.62)**2.2, .04, 1)
    bottom = 1 - np.clip((v - .74)/.24, 0, 1)**1.3*.97
    im = im*vig[..., None]*bottom[..., None]
    im = np.clip(im + rng.normal(0, .012, (h, w, 1)), 0, 1)
    return Image.fromarray((im*255).astype(np.uint8))

def text(d, xy, txt, size, path, track, fill):
    f = ImageFont.truetype(path, size); t = track*size
    w = sum(f.getlength(c) + t for c in txt) - t; x = xy[0] - w/2
    for c in txt: d.text((x, xy[1]), c, font=f, fill=fill, anchor='ls'); x += f.getlength(c) + t
    return w

def title(im, zh, y, k, both=False):
    """y 是主标题基线。标题下面是一根细线和小字"""
    W, H = im.size; S = 3
    lay = Image.new('RGBA', (W*S, H*S), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    main, sub, ms, ss, mt, st = ('天底', '注 意 脚 下', 300, 62, .42, .1) if zh else ('NADIR', "MIND YOUR STEP", 240, 52, .34, .34)
    if both: sub, ss, st = 'N A D I R', 62, .12
    w = text(d, (W*S/2, y*S), main, int(ms*k*S), SERIF, mt, BONE + (255,))
    ly = (y + 62*k)*S; d.line((W*S/2 - 150*k*S, ly, W*S/2 + 150*k*S, ly), fill=DIM + (150,), width=int(2*S*k) or 1)
    text(d, (W*S/2, (y + 146*k)*S), sub, int(ss*k*S), SANS, st, DIM + (255,))
    lay = lay.resize((W, H), Image.LANCZOS)
    glow = lay.filter(ImageFilter.GaussianBlur(26*k)); arr = np.asarray(glow).copy(); arr[..., :3] = (0, 0, 0); shadow = Image.fromarray(arr)
    im = im.convert('RGBA'); im.alpha_composite(shadow); im.alpha_composite(shadow); im.alpha_composite(lay)
    return im.convert('RGB')

wide = int(bh*16/9)
c16 = ((bw - wide)//2, 0, (bw + wide)//2, bh)
g = grade(c16, (3840, 2160), (.5, .49))
g.save(R/'ArtSource/Cover/graded_16x9.png')
for zh, tag in ((True, 'zh'), (False, 'en')):
    title(g, zh, 1985, .62).save(OUT/f'NADIR_封面_16x9_{tag}.png')
    p34 = grade(((bw - bh*3//4)//2, 0, (bw + bh*3//4)//2, bh), (1800, 2400), (.5, .49))
    title(p34, zh, 2195, .62).save(OUT/f'NADIR_封面_3x4_{tag}.png')
    sq = grade(((bw - bh)//2, 0, (bw + bh)//2, bh), (2048, 2048), (.5, .49))
    title(sq, zh, 1880, .6).save(OUT/f'NADIR_封面_1x1_{tag}.png')
# 头显启动画面：系统显示，不分语言，中英文标题同时放；SDK 要求不超过 1024
# 头显启动画面只放纯黑：封面由游戏内播放一次，不要出现两次；SDK 要求不超过 1024
Image.new('RGB', (1024, 1024), (0, 0, 0)).save(ART/'splash.png')
# 图标：不放字，只留眼和站着人的那块砖
s = int(bh*.72); ix = (bw - s)//2; iy = int(bh*.27)
ic = grade((ix, iy, ix + s, iy + s), (1024, 1024), (.5, .3))
ic.save(OUT/'NADIR_图标_1024.png'); ic.resize((432, 432), Image.LANCZOS).save(ART/'icon.png')
print('ok')
