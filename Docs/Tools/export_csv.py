"""데이터 테이블 엑셀 → CSV (시트 1개 = 파일 1개).
규칙: 2행 = 컬럼명, 3행 = 자료형, 4행부터 데이터. Define·ERD 시트는 건너뜀.
사용: python export_csv.py <엑셀 경로> <출력 폴더>"""
import csv, os, sys, openpyxl
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
wb = openpyxl.load_workbook(src, data_only=True, read_only=True)
for ws in wb.worksheets:
    if ws.title in ("Define", "ERD"): continue
    rows = list(ws.iter_rows(values_only=True))
    cols = [i for i, h in enumerate(rows[1]) if h]
    names = [rows[1][i] for i in cols]; types = [rows[2][i] for i in cols]
    def cell(v, t):
        if v is None: return ""
        if t in ("int", "bool") and isinstance(v, float) and v.is_integer(): return str(int(v))
        return str(v)
    with open(os.path.join(out, f"{ws.title}.csv"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f); w.writerow(names)
        n = 0
        for r in rows[3:]:
            if r[cols[0]] is None: continue
            w.writerow([cell(r[i], t) for i, t in zip(cols, types)]); n += 1
    print(f"{ws.title}.csv  {len(names)}열 {n}행")
