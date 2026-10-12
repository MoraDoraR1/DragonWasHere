# Docs/Data — 게임 데이터 CSV (엑셀 Ver02에서 생성)

원본은 `Docs/20261012_데이터테이블_Ver02.xlsx`다. **CSV를 직접 고치지 말고 엑셀을 고친 뒤 다시 만든다.**

```bash
python Docs/Tools/export_csv.py Docs/20261012_데이터테이블_Ver02.xlsx Docs/Data
```

그다음 Unity 메뉴 **DWH > Import Data**(또는 **DWH > Build All**)를 실행하면 `Assets/_Project/Data/GameDatabase.asset`이 갱신된다.
배치모드: `-executeMethod DWH.Editor.Data.CsvImporter.ImportAll`

## 규칙 (MVP 제작 가이드 3장)
- 엑셀 시트 1개 = CSV 1개(Define·ERD 제외, 20개). 2행 컬럼명, 3행 자료형, 4행부터 데이터.
- 머리글(snake_case)은 `Assets/_Project/Scripts/Data/Rows.cs` 필드(camelCase)와 이름으로 연결한다. 맞지 않으면 임포트가 실패한다.
- **빈칸 = 해당 없음.** `[Optional]` 컬럼만 빈칸을 허용한다(문자열 "", 정수·enum 0, 실수 NaN).
- ID는 문자열(`stg101`, `upg505`), enum 컬럼은 정수(`enum.csv`), 문구는 `string.csv`(ko/en), 리소스는 `resource.csv`(`Assets/_Project/Art/` + res_path).
- 고친 뒤 EditMode `DataIntegrityTests`를 돌려 참조 무결성·리소스 파일·MVP 경로를 확인한다.

## 파일
const, enum, dragon, dragon_growth, dragon_part, era, theme, stage, building, unit, drop_item, drop_group, starter_grade, starter_stage, combo, upgrade, rebirth_trait, active_skill, string, resource
