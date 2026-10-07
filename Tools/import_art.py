"""리소스 폴더(한글 경로)를 Unity 영문 경로로 복사하고 필요한 이미지를 트리밍한다.

기준: Docs/임시unity제작.md 9.1(경로), 9.3(트리밍)
사용: python Tools/import_art.py <리소스 폴더> [--out Assets/_Project/Art]
리소스가 갱신되면 다시 실행한다. 출력 폴더의 png는 덮어쓴다(.meta는 유지).
"""
import argparse
import re
import shutil
import sys
from pathlib import Path

from PIL import Image

# 레벨디자인 순서(3.1)의 테마 번호. 파일명 접두어 번호(05 독일, 06 프랑스)는 순서가 반대라 테마키로 연결한다.
THEME_KEYS = {
    "dinosaur": 1, "primitive": 2, "egypt": 3, "greco-roman": 4,
    "medieval-france": 5, "medieval-germany": 6,
    "industrial-britain": 7, "industrial-america": 8,
    "modern-america": 9, "modern-korea": 10,
    "future-japan": 11, "future-china": 12,
    "space-moon": 13, "space-mars": 14,
}
BUILDING_NAME = re.compile(r"^\d{2}-(?P<key>" + "|".join(map(re.escape, THEME_KEYS)) + r")-(?P<name>.+?)(?:-gold)?\.png$")
NUMBERED = re.compile(r"^(\d+)\.")
AIR_UNIT = re.compile(r"^building_st(\d)_(\d)_(\d)\.png$")
DROPS = {
    "01-gold-coin": "gold_coin", "02-gold-bars": "gold_bars", "03-sapphire": "sapphire",
    "04-ruby": "ruby", "05-diamond": "diamond", "06-crown": "crown",
}
BACKGROUND_LAYERS = {"01_far": "far", "02_mid": "mid", "03_ground": "ground"}

ALPHA_NOISE = 8  # 이 값 이하의 알파는 눈에 보이지 않는 잔여 픽셀로 보고 지운다
MARGIN = 12      # 트리밍 후 위·좌·우 여백(px). 바닥 여백은 0


def clean_alpha(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    alpha = im.getchannel("A").point(lambda v: 0 if v <= ALPHA_NOISE else v)
    im.putalpha(alpha)
    return im


def trim_bottom_anchored(im: Image.Image) -> Image.Image:
    im = clean_alpha(im)
    bbox = im.getbbox()
    if bbox is None:
        raise ValueError("이미지가 완전히 투명합니다")
    cropped = im.crop(bbox)
    out = Image.new("RGBA", (cropped.width + MARGIN * 2, cropped.height + MARGIN), (0, 0, 0, 0))
    out.paste(cropped, (MARGIN, MARGIN))
    return out


class Importer:
    def __init__(self, src: Path, out: Path):
        self.src, self.out = src, out
        self.count = 0
        self.errors: list[str] = []

    def save(self, src_file: Path, rel: str, mode: str):
        dst = self.out / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        if mode == "copy":
            shutil.copyfile(src_file, dst)
        else:
            im = Image.open(src_file)
            im = trim_bottom_anchored(im) if mode == "trim" else clean_alpha(im)
            im.save(dst, optimize=True)
        self.count += 1

    def folder(self, number: int) -> Path:
        matches = [p for p in self.src.iterdir() if p.is_dir() and (m := NUMBERED.match(p.name)) and int(m.group(1)) == number]
        if len(matches) != 1:
            raise FileNotFoundError(f"리소스 폴더 {number}번을 찾을 수 없거나 여러 개입니다: {matches}")
        return matches[0]

    def buildings(self, number: int, category: str, mode: str):
        for f in sorted(self.folder(number).rglob("*.png")):
            m = BUILDING_NAME.match(f.name)
            if not m:
                self.errors.append(f"건물 파일명 규칙 불일치: {f.relative_to(self.src)}")
                continue
            theme = THEME_KEYS[m.group("key")]
            self.save(f, f"Buildings/{category}/theme{theme:02d}/{m.group('name')}.png", mode)

    def run(self):
        for era_dir in sorted(self.folder(1).iterdir()):
            era = int(NUMBERED.match(era_dir.name).group(1))
            for src_name, dst_name in BACKGROUND_LAYERS.items():
                self.save(era_dir / f"{src_name}.png", f"Backgrounds/era{era}/{dst_name}.png", "copy")

        self.buildings(2, "Normal", "trim")
        self.buildings(7, "Attack", "copy")
        self.buildings(10, "Golden", "trim")
        self.buildings(11, "GoldenAttack", "trim")

        for f in sorted(self.folder(6).glob("*.png")):
            theme = int(NUMBERED.match(f.name).group(1))
            self.save(f, f"Buildings/Landmark/theme{theme:02d}.png", "copy")

        # 드래곤 파츠는 회전점 좌표(4.1.5)가 원본 캔버스 기준이라 자르지 않고 잔여 픽셀만 지운다
        for f in sorted(self.folder(8).glob("*.png")):
            if f.name.startswith("2026"):
                continue  # 조립 예시 그림은 Docs/에 둔다
            self.save(f, f"Dragon/Parts/{f.name}", "clean")

        # 공중 유닛은 캔버스(200px = 1 unit) 기준 크기를 쓰므로 자르지 않고 잔여 픽셀만 지운다
        for f in sorted(self.folder(9).glob("*.png")):
            m = AIR_UNIT.match(f.name)
            if not m:
                self.errors.append(f"공중 유닛 파일명 규칙 불일치: {f.name}")
                continue
            era, sub, unit = map(int, m.groups())
            self.save(f, f"AirUnits/theme{(era - 1) * 2 + sub:02d}_unit{unit}.png", "clean")

        for f in sorted(self.folder(12).glob("*.png")):
            name = DROPS.get(f.stem)
            if name is None:
                self.errors.append(f"드랍 자원 파일명 규칙 불일치: {f.name}")
                continue
            self.save(f, f"Drops/{name}.png", "copy")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("src", type=Path)
    parser.add_argument("--out", type=Path, default=Path("Assets/_Project/Art"))
    args = parser.parse_args()

    importer = Importer(args.src, args.out)
    importer.run()
    print(f"{importer.count}개 저장 -> {args.out}")
    for e in importer.errors:
        print("오류:", e)
    sys.exit(1 if importer.errors else 0)


if __name__ == "__main__":
    main()
