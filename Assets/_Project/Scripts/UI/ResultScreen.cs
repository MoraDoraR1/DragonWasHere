using System;
using DWH.Core;
using DWH.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 결과 정산 화면 (4.6, 웹 프로토타입 v0.3 참고).
    /// 종료 사유 소제목 + 문장 제목, 2×2 수치 칸(획득 골드·파괴·보너스·최고 기록), 다음 목표, 살 수 있는 업그레이드 수.
    /// 버튼: "업그레이드 하러 가기"(아웃게임) / "바로 다시 출격"(도전 스테이지로 곧장 출발).
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        static readonly Vector2 CardSize = new(1000, 800);
        static readonly Vector2 CellSize = new(452, 128);

        GameDatabase db;
        Image eyebrowChip;
        TMP_Text eyebrow, title, subtitle, goal;
        readonly TMP_Text[] cellLabels = new TMP_Text[4];
        readonly TMP_Text[] cellValues = new TMP_Text[4];

        public static ResultScreen Create(GameDatabase database, Action onUpgrade, Action onRetry)
        {
            var root = UiFactory.Canvas("ResultScreen", 10);
            var screen = root.gameObject.AddComponent<ResultScreen>();
            screen.Build(root, database, onUpgrade, onRetry);
            root.gameObject.SetActive(false);
            return screen;
        }

        void Build(RectTransform root, GameDatabase database, Action onUpgrade, Action onRetry)
        {
            db = database;
            var dim = UiFactory.Stretch(root, "Dim");
            dim.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.86f);

            var card = UiFactory.Panel(root, "Card", UiFactory.Center, Vector2.zero, CardSize, FrameStyle.Cream);
            var top = UiFactory.TopCenter;

            var chip = UiFactory.Panel(card, "Eyebrow", top, new Vector2(0, -36), new Vector2(260, 54), FrameStyle.ChipGold);
            eyebrowChip = chip.GetComponent<Image>();
            eyebrow = UiFactory.Text(chip, "Text", UiFactory.Center, Vector2.zero, new Vector2(260, 54), 28, TextAlignmentOptions.Center, bold: true);
            title = UiFactory.Text(card, "Title", top, new Vector2(0, -104), new Vector2(920, 72), 54, TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            subtitle = UiFactory.Text(card, "Subtitle", top, new Vector2(0, -178), new Vector2(920, 40), 26, TextAlignmentOptions.Center, UiTheme.InkDim);

            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector2((i % 2 == 0 ? -1 : 1) * (CellSize.x / 2 + 10), -240 - (i / 2) * (CellSize.y + 18));
                var cell = UiFactory.Panel(card, $"Cell{i}", top, pos, CellSize, FrameStyle.ChipCream);
                cellLabels[i] = UiFactory.Text(cell, "Label", UiFactory.TopLeft, new Vector2(24, -14), new Vector2(CellSize.x - 48, 32), 22,
                    TextAlignmentOptions.Left, UiTheme.InkDim, bold: true);
                cellValues[i] = UiFactory.Text(cell, "Value", UiFactory.BottomLeft, new Vector2(24, 12), new Vector2(CellSize.x - 48, 74), 38,
                    TextAlignmentOptions.BottomLeft, UiTheme.InkText, bold: true);
            }

            goal = UiFactory.Text(card, "Goal", top, new Vector2(0, -540), new Vector2(920, 80), 26, TextAlignmentOptions.Center, UiTheme.InkText);

            var bottom = UiFactory.BottomCenter;
            UiFactory.Button(card, "Upgrade", bottom, new Vector2(-232, 40), new Vector2(440, 100), "업그레이드 하러 가기", 34, onUpgrade, FrameStyle.ButtonDark);
            UiFactory.Button(card, "Retry", bottom, new Vector2(232, 40), new Vector2(440, 100), "바로 다시 출격", 36, onRetry);
        }

        /// <param name="affordableUpgrades">지금 보유 골드로 살 수 있는 업그레이드 노드 수</param>
        public void Show(SessionResult result, SaveData save, int affordableUpgrades)
        {
            var stage = db.Stage(result.stageId);
            var chipStyle = result.cleared ? FrameStyle.ChipGold : FrameStyle.ChipCream;
            UiFactory.Frame(eyebrowChip.gameObject, chipStyle, result.cleared ? null : UiTheme.Hp);
            eyebrow.color = result.cleared ? UiFactory.LabelColorFor(chipStyle) : UiTheme.TextLight;
            (eyebrow.text, title.text) = result.end switch
            {
                SessionEnd.Defeated => ("쓰러졌다!", "용이 상처를 입고 물러났어요"),
                SessionEnd.Exhausted => ("기력 고갈!", "용이 지쳐 잠들었어요"),
                _ when result.bonusEntered => ("다녀가용!", "황금 마을까지 털고 왔어요!"),
                _ => ("다녀가용!", "마을을 깨끗이 다녀갔어요!"),
            };
            subtitle.text = $"{stage.stageId} · {stage.eraName} {stage.themeName}";

            string gold = UiTheme.HexString(UiTheme.GoldDeep);
            SetCell(0, "획득 골드", $"<color=#{gold}>+{NumberFormat.ToKorean(result.earnedGold)}</color>" +
                (result.clearBonusGold > 0 ? $" <size=22>(보물 더미 +{NumberFormat.ToKorean(result.clearBonusGold)})</size>" : ""));
            SetCell(1, "부순 건물", $"{result.destroyedBuildings} <size=26>/ {result.totalBuildings}</size>");
            SetCell(2, "보너스 스테이지", result.bonusEntered
                ? $"황금 {result.bonusDestroyed}/{result.bonusTotal} <size=26><color=#{gold}>+{NumberFormat.ToKorean(result.bonusGold)}</color></size>"
                : result.cleared ? "<size=30>기력이 모자랐어요</size>" : "<size=30>클리어하면 열려요</size>");
            SetCell(3, "최고 기록", string.IsNullOrEmpty(save.highestStage) ? "-" : save.highestStage);

            string next = result.cleared && save.currentStage != result.stageId
                ? $"다음 목표: 스테이지 <b>{save.currentStage}</b>"
                : $"다음 목표: 스테이지 <b>{save.currentStage}</b> 다시 도전";
            string shop = affordableUpgrades > 0
                ? $"지금 살 수 있는 업그레이드 <color=#{gold}><b>{affordableUpgrades}개</b></color>"
                : "골드를 더 모아 업그레이드하세요";
            goal.text = $"{next}\n<size=24>{shop}</size>";
            gameObject.SetActive(true);
        }

        void SetCell(int index, string label, string value)
        {
            cellLabels[index].text = label;
            cellValues[index].text = value;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
