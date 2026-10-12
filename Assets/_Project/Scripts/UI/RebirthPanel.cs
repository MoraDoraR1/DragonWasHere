using System;
using System.Collections.Generic;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.OutGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 허브의 환생 탭 (가이드 6.10, 임시unity제작.md 4.9).
    /// 왼쪽: 보유 포인트, "지금 환생하면 +N P", 2단계 확인 환생 버튼 / 오른쪽 위: 환생 특성 6장 / 오른쪽 아래: 액티브 스킬 3장.
    /// </summary>
    public class RebirthPanel : MonoBehaviour
    {
        const float ConfirmSeconds = 3f;

        GameDatabase db;
        SaveData save;
        OutGameActions actions;
        TMP_Text title, desc, summary, rebirthLabel, traitsTitle, activesTitle;
        Button rebirthButton;
        float confirmUntil = -1;
        readonly List<(RebirthTraitRow row, TMP_Text name, TMP_Text level, TMP_Text effect, Button buy, TMP_Text buyLabel)> traits = new();
        readonly List<(ActiveSkillRow row, TMP_Text name, TMP_Text level, TMP_Text effect, Button buy, TMP_Text buyLabel)> actives = new();

        public static RebirthPanel Create(RectTransform parent, GameDatabase database, ArtCatalog art, OutGameActions outGameActions)
        {
            var root = UiFactory.Rect(parent, "RebirthPanel", UiFactory.TopCenter, new Vector2(0, -184), new Vector2(1896, 884));
            var panel = root.gameObject.AddComponent<RebirthPanel>();
            panel.Build(root, database, art, outGameActions);
            return panel;
        }

        void Build(RectTransform root, GameDatabase database, ArtCatalog art, OutGameActions outGameActions)
        {
            db = database;
            actions = outGameActions;
            var tl = UiFactory.TopLeft;
            Sprite Icon(string resId) => art != null ? art.SpriteOrNull(resId) : null;

            // 왼쪽 설명 카드
            var card = UiFactory.Panel(root, "Card", tl, Vector2.zero, new Vector2(560, 884), FrameStyle.Cream);
            UiFactory.Icon(card, "Icon", tl, new Vector2(24, -24), new Vector2(64, 64), "icon-rebirth");
            title = UiFactory.Text(card, "Title", tl, new Vector2(100, -26), new Vector2(430, 64), 48, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
            desc = UiFactory.Text(card, "Desc", tl, new Vector2(28, -104), new Vector2(504, 200), 23, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            summary = UiFactory.Text(card, "Summary", tl, new Vector2(28, -330), new Vector2(504, 300), 26, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            rebirthButton = UiFactory.Button(card, "Rebirth", UiFactory.BottomCenter, new Vector2(0, 36), new Vector2(504, 110), "", 30, OnRebirthClicked,
                FrameStyle.ButtonSoul);
            rebirthLabel = rebirthButton.GetComponentInChildren<TMP_Text>();

            // 환생 특성 6종 (3 × 2)
            const float x0 = 584, cardW = 428, gap = 14;
            traitsTitle = UiFactory.Text(root, "TraitsTitle", tl, new Vector2(x0, -4), new Vector2(800, 44), 30, TextAlignmentOptions.Left, UiTheme.Gold, bold: true, outline: true);
            for (int i = 0; i < db.rebirthTraits.Count; i++)
            {
                var row = db.rebirthTraits[i];
                var pos = new Vector2(x0 + (i % 3) * (cardW + gap), -52 - (i / 3) * 236);
                var c = UiFactory.Panel(root, row.traitId, tl, pos, new Vector2(cardW, 222), FrameStyle.Card);
                bool hasIcon = UiFactory.Icon(c, "Icon", tl, new Vector2(14, -10), new Vector2(48, 48), Icon(row.resId)) != null;
                var name = UiFactory.Text(c, "Name", tl, new Vector2(hasIcon ? 68 : 18, -14), new Vector2(230, 40), 26, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
                var level = UiFactory.Text(c, "Level", UiFactory.TopRight, new Vector2(-18, -16), new Vector2(130, 36), 22, TextAlignmentOptions.Right, UiTheme.InkDim, bold: true);
                var effect = UiFactory.Text(c, "Effect", tl, new Vector2(18, -64), new Vector2(cardW - 36, 76), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
                string id = row.traitId;
                var buy = UiFactory.Button(c, "Buy", UiFactory.BottomCenter, new Vector2(0, 14), new Vector2(cardW - 36, 60), "", 24, () =>
                {
                    if (actions.BuyTrait(id)) Refresh();
                }, FrameStyle.ButtonSoul);
                traits.Add((row, name, level, effect, buy, buy.GetComponentInChildren<TMP_Text>()));
            }

            // 액티브 스킬 3종
            activesTitle = UiFactory.Text(root, "ActivesTitle", tl, new Vector2(x0, -532), new Vector2(800, 44), 30, TextAlignmentOptions.Left, UiTheme.Gold, bold: true, outline: true);
            int k = 0;
            foreach (var row in db.activeSkills)
            {
                var pos = new Vector2(x0 + k++ * (cardW + gap), -580);
                var c = UiFactory.Panel(root, row.skillId, tl, pos, new Vector2(cardW, 300), FrameStyle.Card);
                bool hasIcon = UiFactory.Icon(c, "Icon", tl, new Vector2(14, -10), new Vector2(48, 48), Icon(row.resId)) != null;
                var name = UiFactory.Text(c, "Name", tl, new Vector2(hasIcon ? 68 : 18, -14), new Vector2(230, 40), 26, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
                var level = UiFactory.Text(c, "Level", UiFactory.TopRight, new Vector2(-18, -16), new Vector2(130, 36), 22, TextAlignmentOptions.Right, UiTheme.InkDim, bold: true);
                var effect = UiFactory.Text(c, "Effect", tl, new Vector2(18, -64), new Vector2(cardW - 36, 150), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
                var skill = row.Id;
                var buy = UiFactory.Button(c, "Buy", UiFactory.BottomCenter, new Vector2(0, 14), new Vector2(cardW - 36, 60), "", 24, () =>
                {
                    if (actions.BuyActive(skill)) Refresh();
                }, FrameStyle.ButtonSoul);
                actives.Add((row, name, level, effect, buy, buy.GetComponentInChildren<TMP_Text>()));
            }
        }

        public void Show(SaveData saveData)
        {
            save = saveData;
            gameObject.SetActive(true);
            confirmUntil = -1;
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        void OnRebirthClicked()
        {
            // 파괴적 동작은 2단계 확인: 첫 클릭은 확인 요청, 3초 안에 다시 누르면 환생
            if (Time.unscaledTime > confirmUntil)
            {
                confirmUntil = Time.unscaledTime + ConfirmSeconds;
                Refresh();
                return;
            }
            confirmUntil = -1;
            actions.Rebirth();
            Refresh();
        }

        void Update()
        {
            if (confirmUntil > 0 && Time.unscaledTime > confirmUntil)
            {
                confirmUntil = -1;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (save == null) return;
            bool unlocked = actions.RebirthUnlocked();
            double points = Rebirth.PointsIfRebirthNow(db, save);
            string soul = UiTheme.HexString(UiTheme.Chocolate);
            title.text = UiText.Get("ui_rebirth_title");
            desc.text = UiText.Get("ui_rebirth_desc");
            traitsTitle.text = UiText.Get("ui_rebirth_traits");
            activesTitle.text = UiText.Get("ui_rebirth_actives");

            string wall = Rebirth.NextWall(save);
            summary.text =
                $"{UiText.Get("ui_rebirth_point")}  <b><color=#{soul}>{save.rebirthPoint:0} P</color></b>\n" +
                $"{UiText.Format("ui_rebirth_count", save.rebirthCount, Rebirth.MaxRebirths)}\n" +
                $"<b>{UiText.Format("ui_rebirth_gain", points.ToString("0"))}</b>\n" +
                (unlocked ? "" : wall == null
                    ? $"\n{UiText.Get("ui_rebirth_done")}"
                    : $"\n<color=#{UiTheme.HexString(UiTheme.Hp)}>{UiText.Format("ui_rebirth_locked", db.StageLabel(wall))}</color>");

            bool confirming = confirmUntil > 0;
            rebirthButton.interactable = unlocked;
            rebirthLabel.text = !unlocked ? UiText.Get("ui_rebirth_button")
                : confirming ? UiText.Get("ui_rebirth_confirm_button") : $"{UiText.Get("ui_rebirth_button")} (+{points:0} P)";
            if (confirming) summary.text += $"\n<color=#{UiTheme.HexString(UiTheme.Hp)}>{UiText.Get("ui_rebirth_confirm")}</color>";

            foreach (var (row, name, level, effect, buy, label) in traits)
            {
                int lv = Rebirth.TraitLevel(save, row);
                bool maxed = Rebirth.TraitMaxed(row, lv);
                double cost = Rebirth.TraitCost(row, lv);
                name.text = db.Text(row.nameStr);
                level.text = $"Lv {lv} / {row.maxLevel}";
                string now = UpgradeText.Total(db, row, lv), next = UpgradeText.Total(db, row, lv + 1);
                effect.text = $"{db.Text(row.descStr)}\n<b>{now ?? "-"}</b>" + (maxed ? "" : $"  →  <b>{next}</b>");
                buy.interactable = !maxed && save.rebirthPoint >= cost;
                label.text = maxed ? UiText.Get("ui_max") : $"{UiText.Get("ui_buy")} · {cost:0} P";
            }

            foreach (var (row, name, level, effect, buy, label) in actives)
            {
                int lv = Rebirth.ActiveLevel(save, row);
                bool maxed = lv >= row.maxLevel;
                bool prereq = Rebirth.ActivePrereqMet(save, row);
                double cost = Rebirth.ActiveCost(row, lv);
                name.text = db.Text(row.nameStr);
                level.text = lv == 0 ? "-" : $"Lv {lv} / {row.maxLevel}";
                string uses = UiText.Format("ui_active_uses", ActiveSpecs.Uses(Math.Max(1, maxed ? lv : lv + 1)));
                effect.text = $"{db.Text(row.descStr)}\n<b>{UpgradeText.Total(db, row, Math.Max(1, lv))}</b> · {uses}" +
                    (prereq || maxed ? "" : $"\n<color=#{UiTheme.HexString(UiTheme.Hp)}>{UiText.Format("ui_active_need", db.Text(db.Upgrade(row.prereqUpgradeId).nameStr))}</color>");
                buy.interactable = !maxed && Rebirth.CanBuyActive(save, row);
                label.text = maxed ? UiText.Get("ui_max") : $"{UiText.Get(lv == 0 ? "ui_active_learn" : "ui_buy")} · {cost:0} P";
            }
        }
    }

    /// <summary>아웃게임 화면이 GameManager에 요청하는 동작들</summary>
    public class OutGameActions
    {
        public Action Depart;
        public Func<UpgradeRow, int, int> BuyNode;
        public Func<double> Rebirth;
        public Func<string, bool> BuyTrait;
        public Func<ActiveSkillId, bool> BuyActive;
        public Func<bool> RebirthUnlocked;
        public Action ToggleLanguage;
    }
}
