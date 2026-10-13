"""itch 커버 시안 A(파괴 순간형): 정적 PNG와 GIF 커버를 만든다.

사용법:
  python -I Docs/Tools/make_thumbnail_a.py <Fix_Resource 폴더> <출력 폴더>

- 드래곤은 4단계 파츠를 dragon_part.csv(grw10104) 배치대로 조립하고 아래턱을 벌린다(인게임 DragonRig·DragonAnimator와 같은 규칙).
- GIF: 12fps 36프레임(3초) 반복. 입 벌림 → 브레스 → 랜드마크 흔들림 → 붕괴·연기·골드 → 땅에서 다시 솟아 처음으로.
"""
import math
import random
import sys

from PIL import Image, ImageDraw, ImageOps

R, OUT = sys.argv[1], sys.argv[2]
W, H = 630, 500
GROUND = H - 43
FPS, FRAMES = 12, 36


def L(p):
    return Image.open(f"{R}/{p}").convert("RGBA")


def trim(im):
    return im.crop(im.getbbox())


def fit_h(im, h):
    return im.resize((max(1, int(im.width * h / im.height)), h), Image.LANCZOS)


def scale(im, s):
    return im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)


def load_seq(folder, n):
    """프레임 묶음을 같은 영역으로 잘라 위치가 흔들리지 않게 한다."""
    frames = [L(f"{folder}/frame_{i:02d}.png") for i in range(n)]
    box = frames[0].getbbox()
    for f in frames[1:]:
        b = f.getbbox()
        if b:
            box = (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return [f.crop(box) for f in frames]


# ───── 드래곤 조립 (dragon_part.csv grw10104) ─────
# (파일, layer_order, is_back, pivot, parent, attach). 1 = 맨 앞
PARTS = {
    "body":      ("body.png",      8, 0, None,       None,      None),
    "wing":      ("wing.png",      1, 0, (50, 785),  "body",    (427, 359)),
    "back_leg":  ("back_leg.png",  2, 0, (165, 110), "body",    (654, 524)),
    "tail":      ("tail.png",      3, 0, (90, 450),  "body",    (667, 348)),
    "front_leg": ("front_leg.png", 4, 0, (180, 100), "body",    (280, 476)),
    "neck":      ("neck.png",      7, 0, (225, 665), "body",    (165, 405)),
    "up_head":   ("up_head.png",   5, 0, (320, 400), "neck",    (208, 188)),
    "down_head": ("down_head.png", 6, 0, (280, 35),  "up_head", (288, 477)),
    "front_leg2": ("front_leg.png", 9, 1, (180, 100), "body",   (131, 464)),
    "back_leg2":  ("back_leg.png", 10, 1, (165, 110), "body",   (506, 514)),
    "wing2":      ("wing.png",     11, 1, (50, 785),  "body",   (280, 374)),
}
DRAGON_DIR = "드래곤/불 드래곤/4단계"
_part_img = {k: L(f"{DRAGON_DIR}/{v[0]}") for k, v in PARTS.items()}
for k, v in PARTS.items():
    if v[2]:  # 반대편 파츠는 어둡게 (Tuning.BackPartTint 0.6)
        im = _part_img[k]
        rgb = im.convert("RGB").point(lambda c: int(c * 0.6))
        rgb.putalpha(im.getchannel("A"))
        _part_img[k] = rgb


def _origins():
    org = {"body": (0, 0)}
    pending = [k for k in PARTS if k != "body"]
    while pending:
        for k in list(pending):
            _, _, _, pivot, parent, attach = PARTS[k]
            if parent in org:
                po = org[parent]
                org[k] = (po[0] + attach[0] - pivot[0], po[1] + attach[1] - pivot[1])
                pending.remove(k)
    return org


ORIGINS = _origins()
PAD = 600


def dragon(jaw=0.0, head_bob=0.0):
    """왼쪽을 보는 드래곤 전신. jaw 0~1이면 아래턱이 최대 30도 벌어진다. 반환: (그림, 입 위치)"""
    canvas = Image.new("RGBA", (1800, 1500))
    order = sorted(PARTS, key=lambda k: -PARTS[k][1])  # 뒤(layer 큰 것)부터 그린다
    mouth = None
    for k in order:
        im = _part_img[k]
        ox, oy = ORIGINS[k]
        if k in ("up_head", "down_head"):
            oy += head_bob
        if k == "down_head" and jaw > 0:
            px, py = PARTS[k][3]
            padded = Image.new("RGBA", (im.width + 2 * PAD, im.height + 2 * PAD))
            padded.paste(im, (PAD, PAD))
            im = padded.rotate(30 * jaw, center=(PAD + px, PAD + py), resample=Image.BICUBIC)
            ox, oy = ox - PAD, oy - PAD
        canvas.alpha_composite(im, (int(ox) + 400, int(oy) + 400))
        if k == "up_head":  # DragonAnimator.Mouth: 윗머리 아래에서 8%, 주둥이 끝에서 12%
            mouth = (ox + 400 + im.width * 0.12, oy + 400 + im.height * 0.92)
    box = canvas.getbbox()
    return canvas.crop(box), (mouth[0] - box[0], mouth[1] - box[1])


# ───── 리소스 ─────
BG_TILE = L("배경/bg_st5_1_01.png")
BG_TILE = BG_TILE.resize((BG_TILE.width // 2, BG_TILE.height // 2))
LANDMARK = fit_h(trim(L("건물/building_lm_st5_1_01.png")), 280)
BUILDINGS = [fit_h(trim(L(f"건물/building_atk_st5_1_0{i}.png")), 95) for i in (1, 2)]
BREATH = [ImageOps.mirror(f) for f in load_seq("스킬 이펙트/P2. 파이어브레스", 11)]  # 원본은 오른쪽으로 뿜는다
FIRE = load_seq("피격 이펙트/건물화재", 45)
HIT = load_seq("피격 이펙트/강한 타격", 11)
SMOKE = load_seq("피격 이펙트/잔연기", 45)
GOLD = scale(trim(L("UI&폰트/03_자원/01_gold.png")), 0.06)
LOGO = trim(L("UI&폰트/01_UI에셋/assets/12_타이틀/title_en.png"))
LOGO = LOGO.resize((330, int(LOGO.height * 330 / LOGO.width)), Image.LANCZOS)
DRAGON_H = 290
_ref, _ = dragon(0)
DRAGON_SCALE = DRAGON_H / _ref.height
BREATH_W = 250


def sky():
    c = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(c)
    for y in range(H):  # 보라 → 주황 노을
        t = y / H
        d.line([(0, y), (W, y)], fill=(int(40 + 200 * t), int(20 + 70 * t), int(60 - 20 * t), 255))
    for x in range(0, W, BG_TILE.width):
        c.alpha_composite(BG_TILE, (x, H - 55))
    return c


SKY = sky()


def put_center(c, im, cx, cy, h=None):
    if h:
        im = fit_h(im, h)
    c.alpha_composite(im, (int(cx - im.width / 2), int(cy - im.height / 2)))


def render(s):
    """s: 장면 상태 dict"""
    c = SKY.copy()
    lm_x, lm_cx = 10, 10 + LANDMARK.width / 2
    # 랜드마크: 흔들림, 붕괴(가라앉으며 기울기)
    if s["lm_visible"]:
        lm = LANDMARK
        if s["sink"] > 0:
            lm = lm.rotate(-6 * s["sink"], resample=Image.BICUBIC, expand=True)
        dx = s["shake"] * math.sin(s["t"] * 47)
        top = GROUND - LANDMARK.height + s["sink"] * LANDMARK.height * 0.75
        layer = Image.new("RGBA", (W, H))
        layer.alpha_composite(lm, (int(lm_x + dx), int(top)))
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).rectangle([0, 0, W, GROUND], fill=255)  # 땅 아래로 꺼진 부분은 가린다
        c.paste(layer, (0, 0), Image.composite(layer.getchannel("A"), mask, mask).point(lambda a: a))
    for i, (b, x) in enumerate(zip(BUILDINGS, (215, 300))):
        c.alpha_composite(b, (x, GROUND - b.height))
    # 불: 가운데 건물은 항상, 랜드마크는 불길이 커진다
    put_center(c, FIRE[s["fi"] % 45], 280, GROUND - 95, 110)
    if s["lm_fire"] > 0 and s["lm_visible"] and s["sink"] < 0.5:
        put_center(c, FIRE[(s["fi"] + 15) % 45], 90, GROUND - 150 + s["sink"] * 150, int(60 + 60 * s["lm_fire"]))
        put_center(c, FIRE[(s["fi"] + 30) % 45], 160, GROUND - 90 + s["sink"] * 150, int(50 + 50 * s["lm_fire"]))
    # 드래곤
    dr, mouth = dragon(s["jaw"], s["bob"])
    dr = scale(dr, DRAGON_SCALE)
    dx0 = W - dr.width + 40
    dy0 = GROUND - dr.height + 15
    c.alpha_composite(dr, (dx0, dy0))
    mx, my = dx0 + mouth[0] * DRAGON_SCALE, dy0 + mouth[1] * DRAGON_SCALE
    if s["breath"] is not None:
        br = BREATH[s["breath"]]
        br = br.resize((BREATH_W, int(br.height * BREATH_W / br.width)), Image.LANCZOS)
        # 미러 후 입은 오른쪽 끝(프레임 폭의 0.44 지점이 입 → 미러 후 0.56)
        c.alpha_composite(br, (int(mx - br.width * 0.96), int(my + 6 - br.height / 2)))
    if s["hit"] is not None:
        put_center(c, HIT[s["hit"]], mx - BREATH_W * 0.95, my, 130)
    for (gx, gy) in s["coins"]:
        c.alpha_composite(GOLD, (int(gx), int(gy)))
    if s["smoke"] > 0:
        sm = SMOKE[s["si"] % 45]
        for ox, oy, k in ((lm_cx - 10, GROUND - 110, 1.0), (lm_cx + 60, GROUND - 60, 0.85), (lm_cx - 80, GROUND - 55, 0.8)):
            im = fit_h(sm, int(300 * k * (0.7 + 0.3 * s["smoke"])))
            a = im.getchannel("A").point(lambda v: int(v * min(1, s["smoke"])))
            im.putalpha(a)
            put_center(c, im, ox, oy)
    c.alpha_composite(LOGO, ((W - LOGO.width) // 2, -8))
    return c.convert("RGB")


def base_state(t):
    return dict(t=t, fi=int(t * 15), si=0, jaw=0.0, bob=0.0, breath=None, hit=None, shake=0.0,
                sink=0.0, lm_visible=True, lm_fire=0.0, smoke=0.0, coins=[])


def ease(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


random.seed(7)
COIN_V = [(random.uniform(-90, 160), random.uniform(-300, -160)) for _ in range(10)]


def gif_state(f):
    t = f / FPS
    s = base_state(t)
    # 0~0.3초 입 벌림, 0.25~1.9초 브레스(시작 0~2 → 유지 3~6 반복 → 끝 7~10)
    s["jaw"] = ease(f / 3) if f < 22 else 1 - ease((f - 22) / 3)
    s["bob"] = -6 * s["jaw"]
    if 3 <= f < 6:
        s["breath"] = f - 3
    elif 6 <= f < 19:
        s["breath"] = 3 + (f - 6) % 4
    elif 19 <= f < 23:
        s["breath"] = 7 + (f - 19)
    if 5 <= f < 21:
        s["hit"] = (f - 5) % 11
        s["shake"] = 2 + 5 * (f - 5) / 16
        s["lm_fire"] = (f - 5) / 16
    # 1.75~2.4초 붕괴 → 2.5~3초 땅에서 다시 솟아 첫 프레임으로 이어진다
    if 21 <= f < 30:
        s["sink"] = ease((f - 21) / 7)
        s["lm_fire"] = 1.0
        s["shake"] = 6 * (1 - s["sink"])
    elif f >= 30:
        s["sink"] = 1 - ease((f - 29) / 6)
        s["lm_fire"] = 0.0
    # 연기: 붕괴 때 짙어지고 다시 솟는 동안 옅어진다
    if f >= 22:
        s["smoke"] = ease((f - 22) / 4) * (1 - 0.6 * ease((f - 30) / 6))
        s["si"] = 8 + (f - 22)
    if f < 5:
        s["smoke"] = 0.4 * (1 - ease(f / 5))
        s["si"] = 22 + f
    # 골드: 붕괴 순간 튀어 올라 바닥에 떨어지고, 끝에서 사라진다
    if 23 <= f < 34:
        dt = (f - 23) / FPS
        for vx, vy in COIN_V:
            x = 100 + vx * dt
            y = GROUND - 120 + vy * dt + 0.5 * 900 * dt * dt
            s["coins"].append((x, min(y, GROUND - 14)))
    return s


def static_state():
    s = base_state(0.4)
    s.update(jaw=1.0, bob=-6, breath=5, hit=2, lm_fire=0.7, fi=20)
    return s


if __name__ == "__main__":
    png = render(static_state())
    png.save(f"{OUT}/20261013_itch커버_A_Claude.png")
    png.resize((315, 250), Image.LANCZOS).save(f"{OUT}/20261013_itch커버_A_실제크기_Claude.png")
    frames = [render(gif_state(f)) for f in range(FRAMES)]
    # 전 프레임 공용 팔레트(노을 그라데이션 줄무늬가 프레임마다 바뀌지 않게)
    sheet = Image.new("RGB", (W, H * 4))
    for i, f in enumerate(frames[::9][:4]):
        sheet.paste(f, (0, H * i))
    pal = sheet.quantize(colors=255, method=Image.MEDIANCUT)
    q = [f.quantize(palette=pal, dither=Image.FLOYDSTEINBERG) for f in frames]
    q[0].save(f"{OUT}/20261013_itch커버_A_Claude.gif", save_all=True, append_images=q[1:],
              duration=int(1000 / FPS), loop=0, optimize=True, disposal=1)
    print("ok")
