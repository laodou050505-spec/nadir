# 给 P2 模型的 albedo 生成法线和遮罩（R 光泽 G 缝隙）。用法：python3 -I Tools/p2_maps.py [名称...]
import site, sys, pathlib
sys.path.append(site.getusersitepackages())
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, sobel
R = pathlib.Path(__file__).resolve().parents[1]/'ArtSource/ModelsP2'
names = sys.argv[1:] or [p.name for p in R.iterdir() if (p/'final/albedo.png').exists()]
for name in names:
    d = R/name/'final'
    a = np.asarray(Image.open(d/'albedo.png').convert('RGB')).astype(np.float32)/255
    l = a.mean(2)
    hf = l - gaussian_filter(l, 1.5); mf = gaussian_filter(l, 1.5) - gaussian_filter(l, 9)
    h = hf*.9 + mf*1.3
    dx = sobel(h, axis=1)/8; dy = sobel(h, axis=0)/8
    n = np.dstack((-dx*11, dy*11, np.ones_like(l))); n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n*.5+.5)*255).astype(np.uint8)).save(d/'albedo_n.png')
    cav = np.clip(1 + (hf*2 + mf*2.6).clip(-1, 0)*2.2, 0, 1)
    gl = np.clip(.2 + gaussian_filter(l, 24)*.5 - np.abs(mf)*5, 0, 1)
    Image.fromarray((np.dstack((gl, cav, np.zeros_like(l)))*255).astype(np.uint8)).save(d/'albedo_m.png')
    print('ok', name, a.shape[:2])
