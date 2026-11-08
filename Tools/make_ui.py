# 界面贴图：观片灯上的旧 X 光片 + 蜡笔手写。文字和图形是白色 + 透明通道，颜色、显影、光晕在 DLD/UI 里算；胶片和标题带 RGB。
# 贴图按统一比例换算成世界尺寸（PlayBuilder.MetersPerPixel），绝不单独拉伸。
# 字体 Noto Serif/Sans SC、Long Cang、Covered By Your Grace，都是 OFL 授权。用法：python3 -I Tools/make_ui.py
import site, sys, pathlib
sys.path.append(site.getusersitepackages())
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
R = pathlib.Path(__file__).resolve().parents[1]; SRC = R/'ArtSource/UI'; DST = R/'Assets/_DLD/Art/UI'; DST.mkdir(parents=True, exist_ok=True)
for old in DST.glob('*.png'): old.unlink()
for old in DST.glob('*.png.meta'): old.unlink()
SERIF = str(R/'ArtSource/Fonts/NotoSerifSC-SemiBold.otf'); SANS = str(R/'ArtSource/Fonts/NotoSansSC-Medium.otf')
SS = 3   # 超采样，字边不糙

def white(mask):
    im = Image.new('RGBA', mask.size, (255, 255, 255, 0)); im.putalpha(mask); return im

def text(txt, size, path=SERIF, track=0.):
    """track 是字距（em）。四周留足透明边，高层 mip 的光晕和模糊不会被裁掉"""
    f = ImageFont.truetype(path, size*SS); tr = track*size*SS
    w = sum(f.getlength(c) + tr for c in txt) - tr; a, d = f.getmetrics(); pad = int(size*.55)*SS
    m = Image.new('L', (int(w) + pad*2, a + d + pad*2), 0); dr = ImageDraw.Draw(m); x = pad
    for c in txt: dr.text((x, pad), c, font=f, fill=255); x += f.getlength(c) + tr
    return white(m.resize((m.width//SS, m.height//SS), Image.LANCZOS))

def save(im, name): im.save(DST/f'{name}.png')
def pair(name, zh, en, size, path=SERIF, tz=.12, te=.14, se=None, pe=None):
    save(text(zh, size, path, tz), f'{name}_zh'); save(text(en, se or size, pe or path, te), f'{name}_en')

# ── 蜡笔手写：字形来自 Long Cang / Covered By Your Grace（OFL），再做抖动和蜡质颗粒 ──
import numpy as np
from scipy import ndimage as ndi
HZ = str(R/'ArtSource/Fonts/LongCang-Regular.ttf'); HE = str(R/'ArtSource/Fonts/CoveredByYourGrace.ttf')
RNG = np.random.default_rng(23); TRIM = 26
def field(shape, sc):
    x = ndi.gaussian_filter(RNG.standard_normal(shape), sc); return x/(x.std() + 1e-6)
def wax(m, wob=1.4, grain=.42, sc=9):
    """m: 0..1 的形状。低频抖动让线条不直，高频颗粒让笔画像蜡笔蹭在胶片上"""
    h, w = m.shape; yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    o = ndi.map_coordinates(m, (yy + field(m.shape, sc)*wob, xx + field(m.shape, sc)*wob), order=1)
    g = field(m.shape, .75)*.6 + field(m.shape, 2.2)*.4 + field(m.shape, 12)*.45
    return np.clip(o*(1.02 + g*grain), 0, 1)
def to_img(a): return white(Image.fromarray((np.clip(a, 0, 1)*255).astype(np.uint8)))
def hand(txt, size, zh=True, track=.02):
    """逐字摆放，每个字有一点高低和旋转，不是一条直线排出来的"""
    f = ImageFont.truetype(HZ if zh else HE, int(size*SS*(1 if zh else 1.22))); tr = track*size*SS; pad = int(size*.5)*SS
    a, d = f.getmetrics(); adv = [f.getlength(c) + tr for c in txt]; W = int(sum(adv)) + pad*2; H = a + d + pad*2
    m = Image.new('L', (W, H), 0); x = pad; seed = np.random.default_rng(abs(hash_txt(txt)) % 2**31)
    for c, ad in zip(txt, adv):
        cw = int(ad + size*SS); g = Image.new('L', (cw*2, H), 0); ImageDraw.Draw(g).text((cw//2, pad), c, font=f, fill=255)
        g = g.rotate(float(seed.uniform(-2.6, 2.6)), resample=Image.BICUBIC, center=(cw//2 + ad/2, H/2))
        dy = int(seed.uniform(-.025, .025)*size*SS); m.paste(255, (int(x) - cw//2, dy), g); x += ad
    m = m.resize((W//SS, H//SS), Image.LANCZOS)
    # 裁到笔画本身再留固定的边：贴图中心就是字的中心，放进按钮才真正居中
    x0, y0, x1, y1 = m.getbbox(); e = TRIM; m = m.crop((max(0, x0 - e), max(0, y0 - e), min(m.width, x1 + e), min(m.height, y1 + e)))
    return to_img(wax(np.asarray(m).astype(np.float32)/255, wob=min(1.3, .5 + size/260), grain=.3, sc=max(7, size/5)))
def hash_txt(t):
    v = 7
    for c in t: v = (v*131 + ord(c)) & 0x7fffffff
    return v
def hp(name, zh, en, sz, se=None, tz=.03, te=.02):
    save(hand(zh, sz, True, tz), f'{name}_zh'); save(hand(en, se or sz, False, te), f'{name}_en')

# 标题：只有主标题还是印刷体，因为它是刻在金属上的
save(text('天底', 330, SERIF, .34), 'title_zh'); save(text('NADIR', 270, SERIF, .3), 'title_en')
hp('sub', '注意脚下', "mind your step", 92, 92, .24, .06)

# 标题字面：旧金属
def title_tex(key):
    im = Image.open(DST/f'{key}.png'); a = np.asarray(im)[..., 3].astype(np.float32)/255; h, w = a.shape
    rng = np.random.default_rng(11 if key.endswith('zh') else 12)
    def load(n):
        t = Image.open(R/f'ArtSource/TexV2/{n}.png').convert('L').resize((w, w)); return np.asarray(t).astype(np.float32)[:h, :w]/255
    steel, conc = load('steel'), load('concrete')
    def noise(sc):
        x = rng.standard_normal((h, w)); x = ndi.gaussian_filter(x, sc); return x/x.std()
    solid = a > .5; dist = ndi.distance_transform_edt(solid)
    chew = (noise(5)*.55 + noise(1.6)*.45)
    keep = (a*(1 - np.clip((2.4 - dist)/2.4, 0, 1)*np.clip(chew*.5 - .1, 0, 1))); keep = np.clip((keep - .35)/.3, 0, 1)
    height = ndi.gaussian_filter(np.minimum(dist, 11.)/11., 1.6) + (steel - .5)*.22 + (conc - .5)*.16
    gy, gx = np.gradient(height); light = np.clip(.5 + (-gy*.8 - gx*.25)*9, 0, 1)
    base = .5 + .5*steel*.62 + .3*conc*.38
    grime = np.clip(.82 + noise(26)*.17 + noise(7)*.07, .45, 1)
    scratches = np.clip(ndi.gaussian_filter(rng.standard_normal((h, w)), (.5, 13))*7.5 - 1.9, 0, 1)
    v = base*grime*(.72 + .75*light) + scratches*.14*light
    v *= .78 + .36*np.clip(1 - np.linspace(0, 1, h)[:, None]*.85, 0, 1); v = np.clip(v*1.1, 0, 1)
    rgb = np.stack((v*.965, v*.985, v), -1)*np.array([.98, .97, .93])
    save(Image.fromarray(np.dstack((rgb*255, keep*255)).astype(np.uint8)), key.replace('title', 'title_tex'))
title_tex('title_zh'); title_tex('title_en')

# 菜单
hp('l_mode', '难度', 'difficulty', 84, 72); hp('l_area', '活动范围', 'play area', 84, 72); hp('l_floor', '楼层', 'floor', 84, 72)
for i, (zh, en) in enumerate((('简单', 'Easy'), ('普通', 'Normal'), ('困难', 'Hard'))): hp(f'mode{i}', zh, en, 118, 104, .12, .03)
for i, m in enumerate(('2.5', '3', '3.75', '5')): hp(f'area{i}', f'{m}米', f'{m} m', 112, 104)
for i in range(1, 13): save(hand(str(i), 132, False), f'n{i}')
hp('begin', '开始', 'BEGIN', 156, 128, .3, .12)
hp('b_back', '返回', 'back', 96, 88, .2); hp('b_bound', '调整系统安全边界', 'adjust safe boundary', 84, 72)
save(hand('English', 84, False), 'lang_zh'); save(hand('中文', 92, True, .1), 'lang_en')
hp('c_area', '地上的亮线就是这次的活动范围', 'the line on the floor is your play area', 72, 60)
hp('c_nofit', '超出系统安全边界，无法选择', 'larger than your safe boundary', 72, 60)
hp('c_small', '安全区不够大：点右下角，选“自定义”，沿空地画一圈', 'safe area too small: tap below, choose custom, draw around your space', 72, 60)
hp('c_bound', '打不开，请到系统设置里手动调整安全边界', "couldn't open it. use system settings > boundary", 72, 60)
hp('c_hands', '请放下手柄，抬起双手', 'put the controllers down and raise your hands', 112, 92)

# 安全须知
hp('s_title', '开始之前', 'before you begin', 190, 150, .26, .05)
hp('s_r0', '请站起来玩，全程需要走动', 'please stand up, you will walk around', 96, 80); hp('s_r1', '清开四周，留足空地', 'clear the floor around you', 96, 80); hp('s_r2', '不舒服就立刻摘下头显', 'unwell? take the headset off', 96, 80); hp('s_r3', '含恐怖氛围与坠落画面', 'contains horror and falling', 96, 80)
hp('c_sit', '你好像还坐着，请站起来', 'you seem to be sitting. please stand up', 92, 80)
hp('b_lesson', '玩法介绍', 'how to play', 84, 72)
hp('b_pause', '暂停', 'pause', 96, 88, .2); hp('b_resume', '继续', 'resume', 118, 100, .3, .06); hp('b_title', '返回标题', 'back to title', 92, 76, .12); hp('pause_title', '已暂停', 'paused', 170, 140, .3, .08)
hp('b_quit', '退出游戏', 'exit game', 84, 72)
hp('b_enter', '进入', 'ENTER', 124, 104, .3, .1)
# 结局
hp('end_l1', '你出来了', 'you made it out', 190, 150, .3, .06)
hp('end_l2', '天亮了，脚下的地不会再塌了', 'the sun is up, and the ground will hold', 96, 80, .1, .03)
hp('c_m0', '能看到真实的房间，压力最小', 'you can still see your real room', 66, 58); hp('c_m1', '真实的房间变暗，更压抑', 'your real room goes dark', 66, 58); hp('c_m2', '完全置身废弃医院，最恐怖', 'you are fully inside the abandoned hospital', 66, 58)
hp('s_go', '用手指点这里继续', 'tap here to continue', 108, 96)

# 提示
HINTS = {'h_step': ('脚下的砖要塌了，走到白框里', 'the tile under you is breaking, walk to the white frame'), 'h_gaze': ('走过去碰一下灯，或盯住它捏合', 'walk over and touch the lamp, or look at it and pinch'), 'h_over': ('大步跨过去', 'stride across'), 'h_swat': ('伸手把触手拍开', 'swat the tentacle away'),
         'h_push': ('朝黑影猛推一掌，击退它', 'thrust your palm at the figure to repel it'), 'h_eye': ('巨眼睁开时别看破洞，或朝破洞横挥一掌', 'when the eye opens, look away, or swipe across the hole to slap it'), 'h_tear': ('走到裂隙前，双手捏住，往两边拉', 'go to the rift, pinch with both hands, pull apart'), 'h_move': ('别停在一块砖上', 'keep moving'), 'oob': ('回到亮线里', 'step back inside the line'),
         'goal1': ('脚下的石砖会塌，要一直注意', 'the stone tiles under you will break, watch your feet'), 'goal2': ('捡到发光的灯，撕开裂隙，才能离开', 'collect the glowing lamps and tear the rift to get out'), 'h_enter': ('走进裂隙', 'walk into the rift'), 'h_wrist': ('看一眼手背能看进度，用另一只手点一下手背就暂停', 'glance at the back of your hand for progress, tap it with your other hand to pause')}
for k, (zh, en) in HINTS.items(): hp('tip_'+k, zh, en, 112, 96)
# 收集进度：每层开场说一句要收几盏；集齐时说一句去撕开
for n in range(1, 13): hp(f'goal_n{n}', f'收集 {n} 盏灯，裂隙就会出现', f'collect {n} lamps and the rift will appear', 112, 96)
hp('goal_done', '灯齐了，去撕开裂隙', 'all lamps found, go tear the rift open', 112, 96)

# 进度栏上的小灯：手绘的安瓿瓶轮廓（空心）和点亮后的样子（实心），都是白色，颜色在着色器里给
def lamp_icon(filled):
    S_ = 4; W, H = 96, 160; m = Image.new('L', (W*S_, H*S_), 0); d_ = ImageDraw.Draw(m)
    body = [(48, 10), (58, 14), (58, 42), (74, 62), (78, 92), (76, 132), (64, 150), (32, 150), (20, 132), (18, 92), (22, 62), (38, 42), (38, 14)]
    pts = [(x*S_, y*S_) for x, y in body]
    if filled: d_.polygon(pts, fill=255)
    else: d_.line(pts + [pts[0]], fill=255, width=7*S_, joint='curve')
    d_.line([(34*S_, 22*S_), (62*S_, 22*S_)], fill=255, width=6*S_)      # 瓶颈上的那道刻痕
    m = m.resize((W, H), Image.LANCZOS)
    return to_img(wax(np.asarray(m).astype(np.float32)/255, .9, .3))
save(lamp_icon(False), 'lamp_off'); save(lamp_icon(True), 'lamp_on')
CN = '一二三四五六七八九十'
for i in range(1, 13):
    zh = f'第{CN[i-1] if i <= 10 else "十" + (CN[i-11] if i > 10 else "")}层'
    hp(f'floor{i}', zh, f'floor {i}', 240, 200, .22, .05)

# 图标：抠出旧搪瓷牌上的图形，再画成蜡笔的样子
def icon(name, size=420):
    src = Image.open(SRC/f'{name}.png').convert('L'); w, h = src.size
    src = src.crop((int(w*.1), int(h*.1), int(w*.9), int(h*.9))).resize((size*3, size*3), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.6))
    b = np.array(src.point(lambda v: 255 if v < 112 else 0).filter(ImageFilter.MedianFilter(7))) > 0
    for flip in (False, True):
        t = ~b if flip else b; lab, n_ = ndi.label(t); sz = ndi.sum(t, lab, range(1, n_ + 1))
        for i, v in enumerate(sz, 1):
            if v < 900: t[lab == i] = False
        b = ~t if flip else t
    m = Image.fromarray((b*255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.2))
    cut = m.crop(m.getbbox()); k = min(size*3*.8/cut.width, size*3*.8/cut.height); cut = cut.resize((int(cut.width*k), int(cut.height*k)), Image.LANCZOS)
    o = Image.new('L', (size*3, size*3), 0); o.paste(cut, ((o.width - cut.width)//2, (o.height - cut.height)//2))
    return to_img(wax(np.asarray(o.resize((size, size), Image.LANCZOS)).astype(np.float32)/255, 2.2, .4))
for k in ('h_step', 'h_gaze', 'h_over', 'h_swat', 'h_push', 'h_eye', 'h_tear', 'h_move', 's_space', 's_dizzy', 's_warn'): save(icon(k), 'ic_'+k)

# 胶片：裁得不规整的旧 X 光片，贴图比例和按钮一致。只有灰雾、划痕和观片灯的光，没有任何影像
def film(name, w_, h_, seed):
    W, H = int(w_*1500), int(h_*1500); r = np.random.default_rng(seed)
    S_ = 2; j = lambda a: float(r.uniform(-a, a)); e = 7
    cut = float(r.uniform(.04, .09))*min(W, H)
    pts = [(e + j(5) + cut, e + j(5)), (W - e + j(5), e + j(5)), (W - e + j(5), H - e + j(5)), (e + j(5), H - e + j(5)), (e + j(5), e + cut + j(5))]    # 左上角剪掉一个角，是片子的朝向标记
    m = Image.new('L', (W*S_, H*S_), 0); ImageDraw.Draw(m).polygon([(x*S_, y*S_) for x, y in pts], fill=255)
    a = np.asarray(m.resize((W, H), Image.LANCZOS)).astype(np.float32)/255
    def n(sc):
        x = ndi.gaussian_filter(r.standard_normal((H, W)), sc, mode='wrap'); return x/(x.std() + 1e-6)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # 观片灯：光从后面一块偏移的区域透过来，四周掉下去
    cx, cy = W*r.uniform(.4, .62), H*r.uniform(.36, .6); d = np.sqrt(((xx - cx)/(W*.72))**2 + ((yy - cy)/(H*.95))**2)
    lamp = np.clip(1.08 - d, 0, 1)**1.5
    fog = np.clip(.52 + n(max(W, H)*.11)*.2 + n(max(W, H)*.035)*.11 + n(5)*.03, 0, 1)
    sc = np.clip(ndi.gaussian_filter(r.standard_normal((H, W)), (.45, 15))*9.5 - 2.3, 0, 1) + np.clip(ndi.gaussian_filter(r.standard_normal((H, W)), (11, .45))*9 - 2.4, 0, 1)*.7
    specks = np.clip(ndi.gaussian_filter(r.standard_normal((H, W)), .7)*3.4 - 3.0, 0, 1)
    v = (.05 + .4*lamp*fog) + sc*.11*(.3 + lamp) + specks*.16
    edge = np.clip(a - ndi.gaussian_filter((ndi.binary_erosion(a > .5, iterations=3)).astype(np.float32), 1), 0, 1); v = v + edge*.2
    rgb = np.stack((v*.8, v*1.0, v*1.1), -1)
    alpha = a*(.8 + .14*lamp)
    o = Image.fromarray(np.dstack((np.clip(rgb, 0, 1)*255, alpha*255)).astype(np.uint8))
    save(o, name)
SZ = {'go': (.62, .16), 'start': (.36, .16), 'btn': (.293, .13), 'mode': (.293, .13), 'area': (.215, .13), 'floor': (.106, .12)}
for i, (k, (w_, h_)) in enumerate(SZ.items()):
    for v_ in range(3 if k in ('mode', 'area', 'floor', 'btn') else 1): film(f'f_{k}{v_}', w_, h_, 100 + i*10 + v_)
film('f_sheet', 1.24, .88, 77)

# 红蜡笔手画的圈：比按钮略小，起笔收笔不对齐，还多绕一点
def circle(name, w_, h_, seed):
    W, H = int(w_*1500), int(h_*1500); r = np.random.default_rng(seed); S_ = 3
    m = Image.new('L', (W*S_, H*S_), 0); dr = ImageDraw.Draw(m); N = 240; start = r.uniform(-2.6, -1.8); last = None
    p1, p2 = r.uniform(0, 6.28), r.uniform(0, 6.28)
    for i in range(N + 1):
        t = i/N; a_ = start + t*6.283*1.09
        rad = 1 + .035*np.sin(a_*2 + p1) + .018*np.sin(a_*5 + p2) + t*.045
        x = W/2 + np.cos(a_)*rad*(W/2 - 22); y = H/2 + np.sin(a_)*rad*(H/2 - 16)
        wd = int((3.6 + 2.2*np.sin(t*3.14))*S_)
        if last: dr.line((last[0]*S_, last[1]*S_, x*S_, y*S_), fill=255, width=wd)
        last = (x, y)
    save(to_img(wax(np.asarray(m.resize((W, H), Image.LANCZOS)).astype(np.float32)/255, 1.2, .55)), name)
for i, (k, (w_, h_)) in enumerate(SZ.items()): circle(f'mk_{k}', w_, h_, 300 + i)

# 刻痕：通关星级用正字似的小竖线，不用圆点
for c in range(4):
    m = Image.new('L', (420, 120), 0); d_ = ImageDraw.Draw(m); rr = np.random.default_rng(c + 40)
    for i in range(c):
        x0 = 150 + i*50 + rr.uniform(-4, 4); d_.line((x0, 22 + rr.uniform(-4, 4), x0 + rr.uniform(-9, 9), 98 + rr.uniform(-5, 5)), fill=255, width=11)
    save(to_img(wax(np.asarray(m).astype(np.float32)/255, 1, .35)), f'pip{c}')

# 危险方向的小箭头：手绘，朝 +X
m = Image.new('L', (256, 192), 0); d_ = ImageDraw.Draw(m); d_.line((22, 98, 214, 94), fill=255, width=22); d_.line((140, 30, 226, 94), fill=255, width=22); d_.line((146, 164, 226, 94), fill=255, width=22)
save(to_img(wax(np.asarray(m).astype(np.float32)/255, 1.3, .35)), 'arrow')

# 手画的线
n = 1024; m = Image.new('L', (n, 48), 0); d_ = ImageDraw.Draw(m); rr = np.random.default_rng(5); last = None
for i in range(0, n + 1, 8):
    y = 24 + np.sin(i*.009)*5 + rr.uniform(-.6, .6); wd = int(5 + 2*np.sin(i*.003))
    if last: d_.line((last[0], last[1], i, y), fill=255, width=wd)
    last = (i, y)
save(to_img(wax(np.asarray(m).astype(np.float32)/255, 1, .55)), 'line')
m = Image.new('L', (1024, 1024), 0); ImageDraw.Draw(m).ellipse((16, 16, 1008, 1008), outline=255, width=20); save(white(m.resize((512, 512), Image.LANCZOS)), 'ring')
s = 256; m = Image.new('L', (s, s), 0); px = m.load()
for y in range(s):
    for x in range(s):
        r_ = (((x + .5)/s - .5)**2 + ((y + .5)/s - .5)**2)**.5*2; v = max(0, min(1, (1 - r_)/.62)); px[x, y] = int(255*v*v*(3 - 2*v))
save(white(m), 'veil')
# 开场封面：由 make_cover.py 合成的成品缩到 2K
for t_ in ('zh', 'en'): Image.open(R/f'Docs/封面/NADIR_封面_16x9_{t_}.png').convert('RGBA').resize((2048, 1152), Image.LANCZOS).save(DST/f'cover_{t_}.png')
print('ok', len(list(DST.glob('*.png'))))
