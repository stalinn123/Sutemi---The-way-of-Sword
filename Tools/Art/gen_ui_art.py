"""UI art for the game (run: python3 -I gen_ui_art.py <mockup.png> <out_dir>).
- Master_Portrait.png: the master figure cut out of the supplied SUTEMI mock-up (background keyed out).
- FormerStudent_Portrait.png: a dark silhouette with a crimson rim (no art of the former student exists).
- Badge05_Label.png: the engraved "MASTER'S LEGACY" label for badge 5."""
import sys, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps
import numpy as np
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
im = Image.open(src).convert('RGB')
W, H = im.size
crop = im.crop((int(0.03*W), int(0.23*H), int(0.40*W), int(0.84*H)))
a = np.asarray(crop).astype(np.float32)
bg = np.array([0x00, 0x27, 0x3D], np.float32)
d = np.sqrt(((a - bg) ** 2).sum(-1))
alpha = np.clip((d - 18) / 30, 0, 1)
# remove the white page border if present
white = (a.min(-1) > 235)
alpha[white] = 0
alpha = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255
rgba = np.dstack([a, alpha * 255]).astype(np.uint8)
Image.fromarray(rgba, 'RGBA').save(os.path.join(out, 'Master_Portrait.png'))
# former student: dark, desaturated, crimson rim light from the right
g = a.mean(-1, keepdims=True)
dark = 0.35 * g + 0.15 * a
edge = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES).filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255
xs = np.linspace(0, 1, a.shape[1])[None, :, None]
rim = edge[..., None] * xs * np.array([255, 40, 40])
fs = np.clip(np.full_like(a, 12.0) + rim * 1.8, 0, 255)   # silhouette only: the former student is never shown as the master
Image.fromarray(np.dstack([fs, alpha * 255]).astype(np.uint8), 'RGBA').save(os.path.join(out, 'FormerStudent_Portrait.png'))
# badge label
L = Image.new('RGBA', (1024, 256), (0, 0, 0, 0))
dr = ImageDraw.Draw(L)
font = None
for f in ['/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf', '/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf']:
    if os.path.exists(f):
        size = 120
        while size > 20:
            font = ImageFont.truetype(f, size)
            bb = ImageDraw.Draw(L).textbbox((0, 0), "MASTER'S LEGACY", font=font)
            if bb[2] - bb[0] < 960: break
            size -= 4
        break
if font is None: font = ImageFont.load_default()
txt = "MASTER'S LEGACY"
bb = dr.textbbox((0, 0), txt, font=font)
x = (1024 - (bb[2] - bb[0])) // 2; y = (256 - (bb[3] - bb[1])) // 2 - bb[1]
dr.text((x + 4, y + 4), txt, font=font, fill=(60, 30, 5, 200))
dr.text((x, y), txt, font=font, fill=(250, 220, 130, 255))
L.save(os.path.join(out, 'Badge05_Label.png'))
print('ok', crop.size)
