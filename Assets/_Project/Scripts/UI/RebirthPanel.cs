using System;
using System.Collections.Generic;
using DWH.Core;
using DWH.Data;
using DWH.OutGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 허브의 환생 탭 (4.9, 아웃게임 업그레이드 기획서 Ver01 p19~21).
    /// 왼쪽: 환생 설명 카드와 2단계 확인 환생 버튼 / 오른쪽 위: 환생 특성 6종 / 오른쪽 아래: 액티브 스킬 3종 습득·강화.
    /// </summary>
    public class RebirthPanel : MonoBehaviour
    {
        const float ConfirmSeconds = 3f;

        GameDatabase db;
        SaveData save;
        OutGameActions actions;
        TMP_Text summary, rebirthLabel;
        Button rebirthButton;
        float confirmUntil = -1;
        readonly List<(RebirthTraitRow row, TMP_Text level, TMP_Text effect, Button buy, TMP_Text buyLabel)> traits = new();
        readonly List<(ActiveSkillId id, TMP_Text level, TMP_Text effect, Button buy, TMP_Text buyLabel)> actives = new();

        public static RebirthPanel Create(RectTransform parent, GameDatabase database, OutGameActions outGameActions)
        {
            // 넓은 화면에서는 가운데에 둔다
            var root = UiFactory.Rect(parent, "RebirthPanel", UiFactory.TopCenter, new Vector2(0, -184), new Vector2(1896, 884));
            var panel = root.gameObject.AddComponent<RebirthPanel>();
            panel.Build(root, database, outGameActions);
            return panel;
        }

        void Build(RectTransform root, GameDatabase database, OutGameActions outGameActions)
        {
            db = database;
            actions = outGameActions;
            var tl = UiFactory.TopLeft;

            // 왼쪽 설명 카드
            var card = UiFactory.Panel(root, "Card", tl, Vector2.zero, new Vector2(560, 884), FrameStyle.Cream);
            UiFactory.Text(card, "Title", tl, new Vector2(28, -26), new Vector2(504, 64), 48, TextAlignmentOptions.Left, UiTheme.InkText, bold: true).text = "환생";
            UiFactory.Text(card, "Desc", tl, new Vector2(28, -96), new Vector2(504, 200), 23, TextAlignmentOptions.TopLeft, UiTheme.InkText).text =
                "진행을 처음(1-1)부터 다시 시작하고 환생 포인트를 받아요.\n" +
                "<b>초기화</b>: 업그레이드 트리, 보유 골드, 스테이지 진행\n" +
                "<b>유지</b>: 환생 포인트, 환생 특성, 액티브 스킬, 최고 기록\n" +
                "포인트로 영구 특성과 액티브 스킬을 강화해 막힌 시대를 돌파하세요.";
            summary = UiFactory.Text(card, "Summary", tl, new Vector2(28, -320), new Vector2(504, 300), 26, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            rebirthButton = UiFactory.Button(card, "Rebirth", UiFactory.BottomCenter, new Vector2(0, 36), new Vector2(504, 110), "", 34, OnRebirthClicked,
                FrameStyle.ButtonSoul);
            rebirthLabel = rebirthButton.GetComponentInChildren<TMP_Text>();

            // 환생 특성 6종 (3 × 2)
            const float x0 = 584, cardW = 428, gap = 14;
            UiFactory.Text(root, "TraitsTitle", tl, new Vector2(x0, -4), new Vector2(800, 44), 30, TextAlignmentOptions.Left, UiTheme.Gold, bold: true, outline: true)
                .text = "환생 특성 <size=20><color=#c7b8a4>영구 강화 · 환생해도 유지</color></size>";
            for (int i = 0; i < db.rebirthTraits.rows.Count; i++)
            {
                var row = db.rebirthTraits.rows[i];
                var pos = new Vector2(x0 + (i % 3) * (cardW + gap), -52 - (i / 3) * 236);
                var c = UiFactory.Panel(root, row.trait.ToString(), tl, pos, new Vector2(cardW, 222), FrameStyle.ChipCream);
                bool traitIcon = UiFactory.Icon(c, "Icon", tl, new Vector2(14, -10), new Vector2(44, 44), row.icon) != null;
                UiFactory.Text(c, "Name", tl, new Vector2(traitIcon ? 64 : 18, -12), new Vector2(220, 40), 28, TextAlignmentOptions.Left, UiTheme.InkText, bold: true).text = row.nameKo;
                var level = UiFactory.Text(c, "Level", UiFactory.TopRight, new Vector2(-18, -14), new Vector2(150, 36), 22, TextAlignmentOptions.Right, UiTheme.InkDim, bold: true);
                var effect = UiFactory.Text(c, "Effect", tl, new Vector2(18, -56), new Vector2(cardW - 36, 82), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
                var id = row.trait;
                var buy = UiFactory.Button(c, "Buy", UiFactory.BottomCenter, new Vector2(0, 14), new Vector2(cardW - 36, 64), "", 26, () =>
                {
                    if (actions.BuyTrait(id)) Refresh();
                }, FrameStyle.ButtonSoul);
                traits.Add((row, level, effect, buy, buy.GetComponentInChildren<TMP_Text>()));
            }

            // 액티브 스킬 3종
            UiFactory.Text(root, "ActivesTitle", tl, new Vector2(x0, -532), new Vector2(800, 44), 30, TextAlignmentOptions.Left, UiTheme.Gold, bold: true, outline: true)
                .text = "액티브 스킬 <size=20><color=#c7b8a4>전투 중 버튼(1·2·3 키)으로 사용 · 최대 5단계</color></size>";
            int k = 0;
            foreach (ActiveSkillId id in Enum.GetValues(typeof(ActiveSkillId)))
            {
                var pos = new Vector2(x0 + k++ * (cardW + gap), -580);
                var c = UiFactory.Panel(root, id.ToString(), tl, pos, new Vector2(cardW, 300), FrameStyle.ChipCream);
                bool activeIcon = UiFactory.Icon(c, "Icon", tl, new Vector2(14, -10), new Vector2(44, 44), db.Active(id, 1).icon) != null;
                UiFactory.Text(c, "Name", tl, new Vector2(activeIcon ? 64 : 18, -12), new Vector2(220, 40), 28, TextAlignmentOptions.Left, UiTheme.InkText, bold: true)
                    .text = db.Active(id, 1).nameKo;
                var level = UiFactory.Text(c, "Level", UiFactory.TopRight, new Vector2(-18, -14), new Vector2(150, 36), 22, TextAlignmentOptions.Right, UiTheme.InkDim, bold: true);
                var effect = UiFactory.Text(c, "Effect", tl, new Vector2(18, -56), new Vector2(cardW - 36, 150), 21, TextAlignmentOptions.TopLeft, UiTheme.InkText);
                var skill = id;
                var buy = UiFactory.Button(c, "Buy", UiFactory.BottomCenter, new Vector2(0, 14), new Vector2(cardW - 36, 64), "", 26, () =>
                {
                    if (actions.BuyActive(skill)) Refresh();
                }, FrameStyle.ButtonSoul);
                actives.Add((id, level, effect, buy, buy.GetComponentInChildren<TMP_Text>()));
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
            string soul = UiTheme.HexString(UiTheme.Soul);
            summary.text =
                $"보유 환생 포인트  <b><color=#{soul}>{save.rebirthPoint:0} P</color></b>\n" +
                $"환생 횟수  <b>{save.rebirthCount}회</b>\n" +
                $"이번 회차 획득 골드  <b>{NumberFormat.ToKorean(save.runEarnedGold)}</b>\n" +
                $"지금 환생하면  <b><color=#{soul}>+{points:0} P</color></b>\n" +
                (Rebirth.StartGold(db, save) > 0 ? $"환생 직후 골드  <b>{NumberFormat.ToKorean(Rebirth.StartGold(db, save))}</b>\n" : "") +
                (unlocked ? "" : $"\n<color=#{UiTheme.HexString(UiTheme.Hp)}>{db.balance.rebirthUnlockStage}을 클리어하면 환생할 수 있어요</color>");

            bool confirming = confirmUntil > 0;
            rebirthButton.interactable = unlocked;
            rebirthLabel.text = !unlocked ? "아직 환생할 수 없어요"
                : confirming ? "정말 환생할까요? 한 번 더 누르세요" : $"환생하기 (+{points:0} P)";
            rebirthLabel.fontSize = confirming ? 28 : 34;

            foreach (var (row, level, effect, buy, label) in traits)
            {
                int lv = Rebirth.TraitLevel(save, row.trait);
                bool maxed = Rebirth.TraitMaxed(row, lv);
                double cost = Rebirth.TraitCost(row, lv);
                level.text = $"Lv {lv} / {row.maxLevel}";
                effect.text = $"{row.description}\n<b>{TraitText(row, lv)}</b>" + (maxed ? "" : $"  →  <b>{TraitText(row, lv + 1)}</b>");
                buy.interactable = !maxed && save.rebirthPoint >= cost;
                label.text = maxed ? "최대 레벨" : $"강화 · {cost:0} P";
            }

            foreach (var (id, level, effect, buy, label) in actives)
            {
                int lv = Rebirth.ActiveLevel(save, id);
                bool maxed = lv >= Rebirth.MaxActiveLevel;
                double cost = Rebirth.ActiveCost(db, save, id);
                level.text = lv == 0 ? "미습득" : $"{lv}단계 / {Rebirth.MaxActiveLevel}";
                effect.text = lv == 0 ? $"<color=#7c644e>습득하면</color>\n{ActiveText(id, 1)}"
                    : maxed ? ActiveText(id, lv)
                    : $"{ActiveText(id, lv)}\n<color=#7c644e>다음 단계</color>\n{ActiveText(id, lv + 1)}";
                buy.interactable = !maxed && save.rebirthPoint >= cost;
                label.text = maxed ? "최대 단계" : lv == 0 ? $"습득 · {cost:0} P" : $"강화 · {cost:0} P";
            }
        }

        static string TraitText(RebirthTraitRow row, int level)
        {
            double v = Rebirth.TraitTotal(row, level);
            return row.effect switch
            {
                RebirthTraitEffect.AttackPercent => $"공격력 +{v * 100:0}%",
                RebirthTraitEffect.GoldPercent => $"골드 +{v * 100:0}%",
                RebirthTraitEffect.MaxStamina => $"최대 기력 +{v:0}",
                RebirthTraitEffect.StartGold => $"시작 골드 {NumberFormat.ToKorean(Formulas.Round(v))}",
                RebirthTraitEffect.UpgradeDiscount => $"비용 -{v * 100:0}%",
                RebirthTraitEffect.AutoUnlockSkills => level switch { <= 0 => "없음", 1 => "파이어 브레스", _ => "브레스 + 회오리" },
                _ => v.ToString("0.##"),
            };
        }

        string ActiveText(ActiveSkillId id, int level)
        {
            var r = db.Active(id, level);
            return id == ActiveSkillId.Meteor
                ? $"메테오 {r.hitCount}개 × {r.damageMultiplier * 100:0}% · 세션당 {r.usesPerSession}회"
                : $"{r.duration:0.#}초 · 틱당 {r.damageMultiplier * 100:0}% · 세션당 {r.usesPerSession}회";
        }
    }

    /// <summary>아웃게임 화면이 GameManager에 요청하는 동작들</summary>
    public class OutGameActions
    {
        public Action Depart;
        public Func<UpgradeNodeRow, int, int> BuyNode;
        public Func<double> Rebirth;
        public Func<RebirthTraitId, bool> BuyTrait;
        public Func<ActiveSkillId, bool> BuyActive;
        public Func<bool> RebirthUnlocked;
    }
}
