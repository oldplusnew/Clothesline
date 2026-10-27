import os, math
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'docs')
os.makedirs(OUT, exist_ok=True)

W, H = 1600, 560
FONT_DIR = r'C:\Windows\Fonts'

def font(name, size):
    p = os.path.join(FONT_DIR, name)
    try:
        return ImageFont.truetype(p, size)
    except Exception:
        return ImageFont.load_default()

# ---------- background: warm paper gradient ----------
img = Image.new('RGB', (W, H), '#F6F1E8')
d = ImageDraw.Draw(img)
for y in range(H):
    t = y / H
    r = int(0xF7 + (0xE9 - 0xF7) * t)
    g = int(0xF2 + (0xE1 - 0xF2) * t)
    b = int(0xE9 + (0xD6 - 0xE9) * t)
    d.line([(0, y), (W, y)], fill=(r, g, b))

# ---------- rope with sag ----------
ROPE = (0xA1, 0x84, 0x5E)
def rope_y(x):
    xn = (x - W / 2) / (W / 2)
    return 150 + 24 * (1 - xn * xn)

pts = [(x, rope_y(x)) for x in range(-10, W + 11, 3)]
d.line(pts, fill=ROPE, width=4, joint='curve')

# posts at both ends
POST = (0x8B, 0x5E, 0x3C)
d.rounded_rectangle([20, 96, 34, 196], radius=7, fill=POST)
d.rounded_rectangle([W - 34, 96, W - 20, 196], radius=7, fill=POST)
d.ellipse([17, 88, 37, 108], fill=POST)
d.ellipse([W - 37, 88, W - 17, 108], fill=POST)

# ---------- one hanging frame ----------
def make_frame(cw, ch, palette, ui=True):
    pad_l, pad_t = 60, 90
    im = Image.new('RGBA', (cw + pad_l * 2, ch + pad_t + 40), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    x0, y0 = pad_l, pad_t

    # glass frame
    dr.rounded_rectangle([x0 - 9, y0 - 9, x0 + cw + 9, y0 + ch + 9], radius=12,
                         fill=(255, 255, 255, 110), outline=(255, 255, 255, 230), width=3)

    # screenshot content: vertical gradient
    for yy in range(ch):
        t = yy / ch
        c = tuple(int(palette[0][i] + (palette[1][i] - palette[0][i]) * t) for i in range(3))
        dr.line([(x0, y0 + yy), (x0 + cw, y0 + yy)], fill=c + (255,))

    if ui:
        # fake window chrome + text lines
        dr.rectangle([x0, y0, x0 + cw, y0 + 26], fill=(255, 255, 255, 60))
        for i, cx in enumerate((14, 30, 46)):
            dr.ellipse([x0 + cx - 5, y0 + 8, x0 + cx + 5, y0 + 18], fill=(255, 255, 255, 150))
        line_col = (255, 255, 255, 120)
        y = y0 + 46
        widths = [0.62, 0.78, 0.44, 0.70, 0.52]
        for wfrac in widths:
            if y > y0 + ch - 20:
                break
            dr.rounded_rectangle([x0 + 18, y, x0 + 18 + int((cw - 36) * wfrac), y + 8], radius=4, fill=line_col)
            y += 22

    # clothespin
    cx = x0 + cw / 2
    dr.rounded_rectangle([cx - 11, y0 - 44, cx + 11, y0 + 4], radius=6,
                         fill=(0xD8, 0xA1, 0x63), outline=(0x9C, 0x69, 0x32), width=2)
    dr.ellipse([cx - 6, y0 - 39, cx + 6, y0 - 27], fill=(0x7A, 0x4F, 0x26))
    return im

frames = [
    (200, 210, ((0xFF, 0xE8, 0xC7), (0xF0, 0xB6, 0x7A)), -3.2, 590),
    (250, 240, ((0xE7, 0xF1, 0xFA), (0xA8, 0xC8, 0xE8)), 1.6, 800),
    (200, 210, ((0xEF, 0xE6, 0xF7), (0xC0, 0xA8, 0xDE)), 3.4, 1010),
]

for cw, ch, pal, ang, cx in frames:
    fr = make_frame(cw, ch, pal)
    fr = fr.rotate(ang, resample=Image.BICUBIC, expand=True)
    # anchor: pin sits at rope height for that x
    ry = rope_y(cx)
    px = int(cx - fr.width / 2)
    py = int(ry - 86)
    img.paste(fr, (px, py), fr)

# ---------- text ----------
f_title = font('seguisb.ttf', 62)
f_sub = font('segoeui.ttf', 25)
f_credit = font('segoeui.ttf', 19)
f_cn = font('msyh.ttc', 22)

d.text((80, 396), 'Clothesline', font=f_title, fill=(0x2B, 0x25, 0x1F))
d.text((82, 470), 'Screenshots, hung out to dry. Now on Windows.', font=f_sub, fill=(0x5B, 0x51, 0x45))
d.text((82, 508), 'An independent Windows reimplementation of Tendedero by Alejandro Bujan  ·  MIT', font=f_credit, fill=(0x8A, 0x7E, 0x6E))

path = os.path.join(OUT, 'hero.png')
img.save(path, optimize=True)
print('saved', path, img.size)

# ---------- extract 256px PNG from the ICO for the repo logo ----------
ico_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Clothesline.ico')
data = open(ico_path, 'rb').read()
import struct
count = struct.unpack('<H', data[4:6])[0]
best = None
for i in range(count):
    off = 6 + i * 16
    w = data[off] or 256
    size, offset = struct.unpack('<II', data[off + 8:off + 16])
    if best is None or w > best[0]:
        best = (w, size, offset)
w, size, offset = best
png = data[offset:offset + size]
logo = os.path.join(OUT, 'icon.png')
open(logo, 'wb').write(png)
print('saved', logo, 'from ico entry', w, 'px')
