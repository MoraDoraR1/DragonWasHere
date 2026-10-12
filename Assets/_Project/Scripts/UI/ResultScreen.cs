using System;
using DWH.Core;
using DWH.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 결과 화면 (가이드 6.4, 웹 프로토타입 v0.3 결과 카드 기준).
    /// 종료 사유 + 스테이지, 수치 칸(획득 골드·파괴 수·보너스 진입·최고 스테이지), 살 수 있는 업그레이드 수.
    /// 버튼: "둥지로"(아웃게임) / "바로 다시 출격"(첫 판에서는 비활성).
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        static readonly Vector2 CardSize = new(1000, 800);
        static readonly Vector2 CellSize = new(452, 128);

        GameDatabase db;
        Image eyebrowChip;
        TMP_Text eyebrow, title, subtitle, goal;
        Button retry;
        readonly TMP_Text[] cellLabels = new TMP_Text[4];
        readonly TMP_Text[] cellValues = new TMP_Text[4];
        TMP_Text upgradeLabel, retryLabel;

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

            var chip = UiFactory.Panel(card, "Eyebrow", top, new Vector2(0, -36), new Vector2(300, 54), FrameStyle.ChipGold);
            eyebrowChip = chip.GetComponent<Image>();
            eyebrow = UiFactory.Text(chip, "Text", UiFactory.Center, Vector2.zero, new Vector2(300, 54), 28, TextAlignmentOptions.Center, bold: true);
            title = UiFactory.Text(card, "Title", top, new Vector2(0, -104), new Vector2(920, 72), 54, TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            subtitle = UiFactory.Text(card, "Subtitle", top, new Vector2(0, -178), new Vector2(920, 40), 26, TextAlignmentOptions.Center, UiTheme.InkDim);

            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector2((i % 2 == 0 ? -1 : 1) * (CellSize.x / 2 + 10), -240 - (i / 2) * (CellSize.y + 18));
                var cell = UiFactory.Panel(card, $"Cell{i}", top, pos, CellSize, FrameStyle.Card);
                cellLabels[i] = UiFactory.Text(cell, "Label", UiFactory.TopLeft, new Vector2(24, -14), new Vector2(CellSize.x - 48, 32), 22,
                    TextAlignmentOptions.Left, UiTheme.InkDim, bold: true);
                cellValues[i] = UiFactory.Text(cell, "Value", UiFactory.BottomLeft, new Vector2(24, 12), new Vector2(CellSize.x - 48, 74), 38,
                    TextAlignmentOptions.BottomLeft, UiTheme.InkText, bold: true);
            }

            goal = UiFactory.Text(card, "Goal", top, new Vector2(0, -540), new Vector2(920, 80), 26, TextAlignmentOptions.Center, UiTheme.InkText);

            var bottom = UiFactory.BottomCenter;
            var upgrade = UiFactory.Button(card, "Upgrade", bottom, new Vector2(-232, 40), new Vector2(440, 100), "", 34, onUpgrade, FrameStyle.ButtonDark);
            upgradeLabel = upgrade.GetComponentInChildren<TMP_Text>();
            retry = UiFactory.Button(card, "Retry", bottom, new Vector2(232, 40), new Vector2(440, 100), "", 36, onRetry);
            retryLabel = retry.GetComponentInChildren<TMP_Text>();
        }

        /// <param name="affordableUpgrades">지금 보유 골드로 살 수 있는 업그레이드 노드 수</param>
        /// <param name="canRetry">"바로 다시 출격" 가능 여부 (첫 판 1-1에서는 비활성)</param>
        public void Show(SessionResult result, SaveData save, int affordableUpgrades, bool canRetry)
        {
            var stage = db.Stage(result.stageId);
            var chipStyle = result.cleared ? FrameStyle.ChipGold : FrameStyle.ButtonRed;
            UiFactory.Frame(eyebrowChip.gameObject, chipStyle);
            eyebrow.color = UiFactory.LabelColorFor(chipStyle);
            eyebrow.text = UiText.Get(result.end switch
            {
                SessionEnd.Defeated => "ui_result_defeated",
                SessionEnd.Exhausted => "ui_result_exhausted",
                _ => "ui_result_clear",
            });
            title.text = result.gameCompleted ? UiText.Get("ui_result_ending") : UiText.Get(result.cleared ? "ui_hud_clear" : result.end == SessionEnd.Defeated ? "ui_hud_dead_sub" : "ui_hud_exhausted_sub");
            subtitle.text = $"{db.StageLabel(stage)} {db.Text(stage.nameStr)} · {db.Text(db.Era(stage).nameStr)} | {db.Text(db.Theme(stage).nameStr)}";

            string gold = UiTheme.HexString(UiTheme.GoldDeep);
            SetCell(0, UiText.Get("ui_result_gold"), $"<color=#{gold}>+{NumberFormat.ToKorean(result.earnedGold)}</color>" +
                (result.clearBonusGold > 0 ? $" <size=22>({UiText.Get("ui_result_clear_bonus")} +{NumberFormat.ToKorean(result.clearBonusGold)})</size>" : ""));
            SetCell(1, UiText.Get("ui_result_destroyed"), $"{result.destroyedBuildings} <size=26>/ {result.totalBuildings}</size>" +
                (result.maxCombo >= 3 ? $"  <size=22>{UiText.Get("ui_result_max_combo")} {result.maxCombo}</size>" : ""));
            SetCell(2, UiText.Get("ui_result_bonus"), result.bonusEntered
                ? $"{result.bonusDestroyed}/{result.bonusTotal} <size=26><color=#{gold}>+{NumberFormat.ToKorean(result.bonusGold)}</color></size>"
                : $"<size=30>{UiText.Get("ui_result_bonus_none")}</size>");
            SetCell(3, UiText.Get("ui_result_best"), string.IsNullOrEmpty(save.highestStage) ? "-" : db.StageLabel(save.highestStage));

            string next = $"{UiText.Get("ui_next_stage")}: <b>{db.StageLabel(save.currentStage)} {db.Text(db.Stage(save.currentStage).nameStr)}</b>";
            string shop = affordableUpgrades > 0
                ? $"<color=#{gold}><b>{UiText.Format("ui_result_upgrades", affordableUpgrades)}</b></color>"
                : "";
            goal.text = $"{next}\n<size=24>{shop}</size>";
            upgradeLabel.text = UiText.Get("ui_result_to_base");
            retryLabel.text = UiText.Get("ui_result_retry");
            retry.interactable = canRetry;
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
