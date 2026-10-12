"""UI 프레임 스프라이트(9-slice)를 만든다. 웹 프로토타입 v0.3의 디자인 언어를 따른다.

굵은 먹선 + 크림색 안쪽 테두리 + 모서리를 깎은 평평한 면 (기준 문서 9.6: "두꺼운 검은 테두리, 플랫 채색").
사용: python Tools/make_ui_frames.py   → Assets/_Project/Art/UI/*.png
9-slice 경계(px)는 ArtScaleSettings의 UI 규칙(slice_border)과 맞춘다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "Assets" / "_Project" / "Art" / "UI"
SIZE = 48          # 원본 크기(px). 9-slice로 늘려 쓴다
CUT = 8            # 모서리 깎기
INK = 3            # 바깥 먹선 두께
RIM = 2            # 안쪽 테두리 두께
SCALE = 4          # 계단 현상을 줄이려고 크게 그린 뒤 줄인다

INK_COLOR = "#271b1b"
RIM_CREAM = "#ecd0ab"


def chamfer(inset, size=SIZE, cut=CUT):
    a, b = inset, size - inset
    c = max(0, cut - inset * 0.6)
    return [(a + c, a), (b - c, a), (b, a + c), (b, b - c), (b - c, b), (a + c, b), (a, b - c), (a, a + c)]


def draw(layers, path, size=SIZE, cut=CUT):
    """layers: (inset, color) 목록. 바깥부터 차례로 덮어 그린다. color가 None이면 투명하게 뚫는다."""
    big = Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    for inset, color in layers:
        poly = [(x * SCALE, y * SCALE) for x, y in chamfer(inset, size, cut)]
        d.polygon(poly, fill=color if color else (0, 0, 0, 0))
    big.resize((size, size), Image.LANCZOS).save(path)


def frame(fill, rim=RIM_CREAM):
    return [(0, INK_COLOR), (INK, rim), (INK + RIM, fill)]


STYLES = {
    # 패널
    "panel_dark": frame("#49312c"),
    "panel_cream": frame("#f7f0e6"),
    "panel_ink": [(0, INK_COLOR), (INK, "#3a2a26"), (INK + 1, INK_COLOR)],
    # 버튼
    "button_gold": frame("#e9bd64"),
    "button_dark": frame("#49312c"),
    "button_soul": frame("#b9a3c1"),
    # 스테이지 번호 칩 (금색 + 밝은 안쪽 테두리)
    "chip_gold": frame("#efc15a", rim="#f7dfa0"),
    "chip_cream": frame("#f2dfc5", rim="#9b775a"),
    # 상태 테두리(가운데가 뚫린 고리). 색은 Image.color로 입힌다
    "ring_white": [(0, "#ffffff"), (INK + 1, None)],
}

# 바(게이지)는 둥근 사각형이 자연스러워 따로 그린다
BARS = {
    "bar_back": ("#2c211f", "#84664e"),
    "bar_fill": ("#ffffff", None),
}


def draw_bar(fill, edge, path, size=24, radius=7):
    big = Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    if edge:
        d.rounded_rectangle([0, 0, size * SCALE - 1, size * SCALE - 1], radius=radius * SCALE, fill=edge)
        d.rounded_rectangle([2 * SCALE, 2 * SCALE, size * SCALE - 1 - 2 * SCALE, size * SCALE - 1 - 2 * SCALE],
                            radius=(radius - 2) * SCALE, fill=fill)
    else:
        d.rounded_rectangle([0, 0, size * SCALE - 1, size * SCALE - 1], radius=radius * SCALE, fill=fill)
    big.resize((size, size), Image.LANCZOS).save(path)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, layers in STYLES.items():
        draw(layers, OUT / f"{name}.png")
    for name, (fill, edge) in BARS.items():
        draw_bar(fill, edge, OUT / f"{name}.png")
    print(f"{len(STYLES) + len(BARS)}개 저장 -> {OUT}")


if __name__ == "__main__":
    main()
