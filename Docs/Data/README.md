# Docs/Data — 게임 데이터 원본 CSV

Unity 메뉴 **DWH > Import Data**를 실행하면 이 CSV들이 `Assets/_Project/Data/*.asset`으로 변환된다.
배치모드에서는 `-executeMethod DWH.Editor.Data.CsvImporter.ImportAll`로 실행한다.

## 규칙
- 머리글은 Unity 데이터 클래스(`Assets/_Project/Scripts/Data/Rows.cs` 등)의 필드 이름을 snake_case로 쓴 것이다. 열 이름과 필드가 하나라도 맞지 않으면 임포트가 실패한다.
- 목록 값은 `;`로 구분한다. 예: `3;10;25`
- 배율은 퍼센트가 아니라 소수로 쓴다. 예: 150% → `1.5`
- `key,value,description` 형식 파일(`dragon_base`, `balance`)은 key가 필드 이름이다.
- 값을 바꾼 뒤에는 EditMode 테스트(`DataConsistencyTests`)를 돌려 공식과 표가 여전히 맞는지 확인한다.

## 파일과 출처

| 파일 | 내용 | 출처 | 관리 방법 |
|---|---|---|---|
| `stages.csv` | 28스테이지 건물 수·HP·공격력·허들·골드 가이드 | 임시unity제작.md 3.2 | **생성** (`python Tools/export_data.py`) |
| `air_units.csv` | 스테이지별 공중 유닛 이름·수·스탯·골드 | 임시unity제작.md 4.5 | **생성** |
| `drop_composition.csv` | 건물 1개당 자원별 드랍 개수 | 엑셀 Ver06 「드랍 구성 (개편)」 | **생성** |
| `air_drop.csv` | 공중 유닛 1마리당 자원별 드랍 개수 | 엑셀 Ver06 「공중 유닛 드랍」 | **생성** |
| `hurdles.csv` | 허들 등급별 HP 배율·목표 반복 | 3.3 | 수기 |
| `building_types.csv` | 건물 유형별 골드 배율 | 3.4 | 수기 |
| `drop_curves.csv` | 드랍 개수 곡선(기준·증가율) | 4.6 | 수기 |
| `drop_resources.csv` | 드랍 자원 6종 환산 골드·데뷔 스테이지 | 4.6, 엑셀 「설정」 | 수기 |
| `passive_skills.csv` | 패시브 6종 수치·하위 업그레이드 | 4.3 | 수기 |
| `active_skills.csv` | 액티브 3종 × 5단계 수치·RP 비용 | 4.4 | 수기 |
| `rebirth_traits.csv` | 환생 특성 6종(비용·최대 레벨은 아웃게임 기획서 Ver01 p21, 효과는 임시) | 4.9 | 수기 |
| `upgrade_nodes.csv` | 업그레이드 트리 노드 14개 + 스킬 노드 6개 + 하위 업그레이드 6개 | 4.8 | 수기 |
| `dragon_base.csv` | 드래곤 기본 능력치 | 4.1.1, 8.6 | 수기 |
| `growth.csv` | 성장 4단계 | 4.1.4 | 수기 |
| `stage_starter.csv` | 스타터 판정별 배율·범위 | 4.7 | 수기 |
| `dragon_parts.csv` | 드래곤 파츠 조립표(회전점·부착점·레이어) | 4.1.5 | 수기 |
| `balance.csv` | 그 밖의 단일 수치(골드 계수, 공중 유닛 배율, 환생, 스타터 난이도, 카메라·배치·탄 등) | 1.1, 3.4, 4.5, 4.7, 4.9, 8.5.2, 9.3 | 수기 |

**생성** 파일은 손으로 고치지 않는다. 기준 문서나 엑셀을 고친 뒤 스크립트를 다시 실행한다.

## 임시로 정한 값 (기준 문서에 없는 구현 해석)
- 스킬 노드 비용은 일반 노드와 같은 공식 `ceil(초기 비용 × 배수^현재레벨)`을 쓴다.
- 랜드마크 3발은 부채꼴 각도가 아니라 아래·중간·위 세 높이의 수평 탄으로 구현했다(`landmark_shot_heights`). 붙어 있을 때 각도와 상관없이 3발이 모두 맞는 문제를 피하고, "위쪽 탄은 점프해도 맞을 수 있음"을 그대로 표현한다. (T-05)
- 몸통 박치기는 기본 보유라 `initial_level = 1`에서 시작한다. 그래서 첫 강화 비용은 50 × 1.2 = 60이다. (T-14)

- `icon` 열(upgrade_nodes, rebirth_traits, passive_skills, active_skills): UI 디자인 패키지 v1의 아이콘 파일 이름(`Assets/_Project/Art/UIKit/icons`). 기준 문서 9.5.1.
