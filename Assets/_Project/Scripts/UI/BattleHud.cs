using System;
using System.Collections.Generic;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Skills;
using DWH.InGame.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 전투 HUD (4.12, 웹 프로토타입 v0.3 참고).
    /// 좌상단: 골드 알약(+이번 판 획득)·체력·기력(낮으면 깜빡임)·피로 / 상단 중앙: 스테이지 박스와 파괴 진행 바 /
    /// 우상단: 일시정지 / 좌하단: 이동 모드 / 오른쪽: 콤보 / 가운데: 배너 / 오른쪽 위: 황금 건물·랜드마크 골드 토스트.
    /// 스킬 바와 대미지 팝업은 이후 마일스톤(M6·M9)에서 붙인다.
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        const float LowStamina = 0.25f;
        const float LowHp = 0.3f;
        const float BannerSeconds = 1.8f;
        const float ToastSeconds = 2.2f;
        const float ToastGap = 58f;

        class Toast
        {
            public RectTransform rect;
            public CanvasGroup group;
            public float age;
        }

        BattleController battle;
        Func<double> ownedGold;
        double carriedGold;
        RectTransform root, hpFill, staminaFill, progressFill, toastLayer;
        Image hpChip, staminaChip, hpFillImage, staminaFillImage, progressFillImage, modeChip;
        TMP_Text goldText, sessionText, hpText, staminaText, fatigueText, progressText, modeText;
        RectTransform goldRect;
        double lastSession;
        float goldPunch;

        RectTransform combo;
        CanvasGroup comboGroup;
        TMP_Text comboCount, comboBonus;
        RectTransform comboTimer;
        int lastStacks;
        float comboPunch;

        RectTransform banner;
        CanvasGroup bannerGroup;
        TMP_Text bannerTitle, bannerSub;
        float bannerAge = float.MaxValue;
        BattleState lastState = BattleState.Playing;
        (string title, string sub)? pendingBanner;
        /// <summary>화면 아래쪽 HUD(모드 칩, 스킬 바, 액티브 버튼, 팁). 스타터 줌아웃 중에는 맵의 건물 줄을 가리므로 숨긴다</summary>
        readonly List<GameObject> bottomGroup = new();
        bool bottomShown = true;

        CanvasGroup hurtEdges;
        float hurtFlash;
        CanvasGroup tip;
        float tipAge;
        readonly List<Toast> toasts = new();
        readonly List<(PassiveSkillSystem.SkillState state, Image frame, RectTransform shade, TMP_Text label)> skillSlots = new();
        readonly List<(ActiveSkillId id, Button button, TMP_Text label)> activeButtons = new();
        readonly List<Image> activeIcons = new();
        bool kitSlots;

        /// <param name="ownedGoldProvider">출발 시점의 보유 골드. 없으면 이번 세션 획득량만 표시한다</param>
        /// <param name="onPause">일시정지 버튼. null이면 버튼을 만들지 않는다</param>
        /// <param name="showTip">첫 판 조작 안내를 띄운다</param>
        /// <param name="carriedSessionGold">이번 판 앞 구간(보너스 전 일반 스테이지)에서 번 골드. "+N" 탭에 더한다</param>
        public static BattleHud Create(BattleController battle, Func<double> ownedGoldProvider = null, ArtCatalog art = null,
            Action onPause = null, bool showTip = false, double carriedSessionGold = 0)
        {
            var root = UiFactory.Canvas("HUD", 0);
            var hud = root.gameObject.AddComponent<BattleHud>();
            hud.carriedGold = carriedSessionGold;
            hud.Build(root, battle, ownedGoldProvider, art, onPause, showTip);
            return hud;
        }

        void Build(RectTransform canvas, BattleController target, Func<double> ownedGoldProvider, ArtCatalog art, Action onPause, bool showTip)
        {
            root = canvas;
            battle = target;
            ownedGold = ownedGoldProvider;

            BuildGold(art);
            BuildVitals();
            BuildStageBox();
            BuildCombo();
            BuildBanner();
            toastLayer = UiFactory.Rect(root, "Toasts", UiFactory.TopRight, new Vector2(-40, -150), new Vector2(520, 400));
            BuildSkillBar();
            BuildActiveBar();
            BuildHurtEdges();
            battle.Dragon.Hurt += _ => hurtFlash = 1f;

            var mode = UiFactory.Panel(root, "Mode", UiFactory.BottomLeft, new Vector2(32, 28), new Vector2(220, 56), FrameStyle.ChipCream);
            bottomGroup.Add(mode.gameObject);
            modeChip = mode.GetComponent<Image>();
            modeText = UiFactory.Text(mode, "Text", UiFactory.Center, Vector2.zero, new Vector2(220, 56), 26, TextAlignmentOptions.Center, bold: true);

            if (onPause != null)
            {
                var pause = UiFactory.Button(root, "Pause", UiFactory.TopRight, new Vector2(-28, -24), new Vector2(84, 84), "II", 40, onPause, FrameStyle.ButtonDark);
                // UI 패키지의 일시정지 아이콘이 있으면 글자 대신 쓴다
                if (UiFactory.Icon((RectTransform)pause.transform, "Icon", UiFactory.Center, new Vector2(0, 3), new Vector2(46, 46), "pause") != null)
                    pause.GetComponentInChildren<TMP_Text>().text = "";
            }

            if (showTip)
            {
                var tipPanel = UiFactory.Panel(root, "Tip", UiFactory.BottomCenter, new Vector2(0, 196), new Vector2(980, 80), FrameStyle.Ink);
                bottomGroup.Add(tipPanel.gameObject);
                tip = tipPanel.gameObject.AddComponent<CanvasGroup>();
                var tipText = UiFactory.Text(tipPanel, "Text", UiFactory.Center, Vector2.zero, new Vector2(940, 80), 26, TextAlignmentOptions.Center);
                tipText.text = "커서를 움직이면 용이 그쪽으로 달려가요 · 가만히 두면 알아서 날뛰어요";
            }

            if (battle.Mode == BattleMode.Bonus)
                ShowBanner("보너스 스테이지!", $"기력이 2배로 빨리 줄고 회복도 없어요 · 황금 건물 {battle.Buildings.Count}채를 아껴 부수세요");
            else
                // 스테이지 스타터가 있으면 "Game Start!"와 겹치지 않게 스타터가 끝난 뒤에 띄운다
                pendingBanner = ($"{battle.Stage.stageId} {battle.Stage.themeName}",
                    battle.Stage.stageId.EndsWith("-1") && battle.Stage.era > 1 ? $"새 시대 진입! {battle.Stage.eraName}" : battle.Stage.eraName);

            battle.Drops.Burst += OnBurst;
            battle.Drops.AirBurst += OnAirBurst;
        }

        void BuildGold(ArtCatalog art)
        {
            var pill = UiFactory.Panel(root, "GoldPill", UiFactory.TopLeft, new Vector2(28, -24), new Vector2(360, 72), FrameStyle.Ink);
            var left = new Vector2(0, 0.5f);
            var coin = UiFactory.Rect(pill, "Coin", left, new Vector2(12, 0), new Vector2(52, 52)).gameObject.AddComponent<Image>();
            coin.sprite = UiTheme.KitSprite("coin") ?? (art != null ? art.Drop(DropResource.GoldCoin) : null);
            coin.color = coin.sprite != null ? Color.white : UiTheme.Gold;
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            goldText = UiFactory.Text(pill, "Gold", left, new Vector2(74, 0), new Vector2(276, 72), 36, TextAlignmentOptions.Left, UiTheme.Gold, bold: true);
            goldRect = (RectTransform)goldText.transform;

            var tab = UiFactory.Panel(root, "Session", UiFactory.TopLeft, new Vector2(396, -32), new Vector2(190, 56), FrameStyle.ChipGold);
            sessionText = UiFactory.Text(tab, "Text", UiFactory.Center, Vector2.zero, new Vector2(180, 56), 28, TextAlignmentOptions.Center,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true);
        }

        void BuildVitals()
        {
            hpFill = VitalBar("Hp", "체력", -110, UiTheme.Hp, out hpChip, out hpFillImage, out hpText);
            staminaFill = VitalBar("Stamina", "기력", -170, UiTheme.Energy, out staminaChip, out staminaFillImage, out staminaText);

            var fatigue = UiFactory.Panel(root, "Fatigue", UiFactory.TopLeft, new Vector2(28, -230), new Vector2(300, 46), FrameStyle.ChipCream);
            fatigueText = UiFactory.Text(fatigue, "Text", UiFactory.Center, Vector2.zero, new Vector2(290, 46), 22, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true);
        }

        RectTransform VitalBar(string name, string label, float y, Color color, out Image chip, out Image fillImage, out TMP_Text value)
        {
            var chipRect = UiFactory.Panel(root, $"{name}Chip", UiFactory.TopLeft, new Vector2(28, y), new Vector2(96, 50), FrameStyle.ChipCream, color);
            chip = chipRect.GetComponent<Image>();
            UiFactory.Text(chipRect, "Label", UiFactory.Center, Vector2.zero, new Vector2(96, 50), 24, TextAlignmentOptions.Center, bold: true, outline: true).text = label;

            var fill = UiFactory.Bar(root, $"{name}Bar", UiFactory.TopLeft, new Vector2(132, y - 3), new Vector2(420, 44), color);
            fillImage = fill.GetComponent<Image>();
            value = UiFactory.Text((RectTransform)fill.parent, "Value", new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(390, 44), 22,
                TextAlignmentOptions.Left, bold: true, outline: true);
            return fill;
        }

        void BuildStageBox()
        {
            var box = UiFactory.Panel(root, "StageBox", UiFactory.TopCenter, new Vector2(0, -20), new Vector2(620, 116));
            bool bonus = battle.Mode == BattleMode.Bonus;
            var chip = UiFactory.Panel(box, "Chip", UiFactory.TopLeft, new Vector2(18, -16), new Vector2(110, 48), FrameStyle.ChipGold);
            UiFactory.Text(chip, "Id", UiFactory.Center, Vector2.zero, new Vector2(110, 48), 28, TextAlignmentOptions.Center,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true).text = battle.Stage.stageId;
            var name = UiFactory.Text(box, "Name", UiFactory.TopLeft, new Vector2(142, -14), new Vector2(460, 52), 30, TextAlignmentOptions.Left, bold: true);
            name.text = bonus
                ? $"<color=#{UiTheme.HexString(UiTheme.Gold)}>보너스 스테이지</color>"
                : $"{battle.Stage.themeName} <size=22><color=#{UiTheme.HexString(UiTheme.LineIdle)}>{battle.Stage.eraName}</color></size>";

            progressFill = UiFactory.Bar(box, "Progress", UiFactory.BottomCenter, new Vector2(0, 14), new Vector2(584, 38), bonus ? UiTheme.Gold : UiTheme.Jade);
            progressFillImage = progressFill.GetComponent<Image>();
            progressText = UiFactory.Text((RectTransform)progressFill.parent, "Text", UiFactory.Center, Vector2.zero, new Vector2(560, 38), 22,
                TextAlignmentOptions.Center, bold: true, outline: true);
        }

        void BuildCombo()
        {
            combo = UiFactory.Rect(root, "Combo", new Vector2(1, 0.5f), new Vector2(-60, 120), new Vector2(360, 230));
            comboGroup = combo.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0;
            comboCount = UiFactory.Text(combo, "Count", UiFactory.TopCenter, Vector2.zero, new Vector2(360, 130), 120, TextAlignmentOptions.Center,
                UiTheme.Gold, bold: true, outline: true);
            comboBonus = UiFactory.Text(combo, "Bonus", UiFactory.TopCenter, new Vector2(0, -126), new Vector2(360, 50), 32, TextAlignmentOptions.Center,
                bold: true, outline: true);
            var timer = UiFactory.Bar(combo, "Timer", UiFactory.TopCenter, new Vector2(0, -182), new Vector2(240, 22), UiTheme.Ember);
            comboTimer = timer;
        }

        /// <summary>하단 스킬 바 (4.12): 쿨타임은 아래에서 위로 걷히는 덮개, 발동 중은 금색, 대기 중(대상 없음)은 밝게</summary>
        void BuildSkillBar()
        {
            var skills = battle.Skills != null ? battle.Skills.Skills : null;
            if (skills == null || skills.Count == 0) return;
            kitSlots = UiTheme.KitSprite("slot-normal") != null;
            // UI 패키지: 112×112 슬롯 + 아이콘 + 아래 이름 (02-battle-hud 시안). 없으면 예전 이름 칩
            float width = kitSlots ? 112 : 150, height = kitSlots ? 112 : 84, gap = kitSlots ? 24 : 12;
            float total = skills.Count * width + (skills.Count - 1) * gap;
            var bar = UiFactory.Rect(root, "SkillBar", UiFactory.BottomCenter, new Vector2(0, kitSlots ? 40 : 24), new Vector2(total, height));
            bottomGroup.Add(bar.gameObject);
            for (int i = 0; i < skills.Count; i++)
            {
                var slot = UiFactory.Panel(bar, $"Skill{i}", UiFactory.BottomLeft, new Vector2(i * (width + gap), 0), new Vector2(width, height), FrameStyle.ChipCream);
                if (kitSlots)
                {
                    slot.GetComponent<Image>().sprite = UiTheme.KitSprite("slot-normal");
                    UiFactory.Icon(slot, "Icon", UiFactory.Center, new Vector2(0, 3), new Vector2(72, 72), skills[i].row.icon);
                }
                // 쿨타임 덮개: 슬롯 안에서만 보이게 마스크하고 아래에서 위로 줄어든다
                var clip = UiFactory.Stretch(slot, "CooldownClip", kitSlots ? 8 : 4);
                clip.gameObject.AddComponent<RectMask2D>();
                var shade = UiFactory.Stretch(clip, "Cooldown");
                var shadeImage = shade.gameObject.AddComponent<Image>();
                shadeImage.sprite = UiTheme.KitSprite("cooldown");
                shadeImage.color = shadeImage.sprite != null ? Color.white : new Color(0.15f, 0.1f, 0.08f, 0.7f);
                shadeImage.raycastTarget = false;
                var label = kitSlots
                    ? UiFactory.Text(slot, "Name", UiFactory.BottomCenter, new Vector2(0, -30), new Vector2(width + 30, 28), 20,
                        TextAlignmentOptions.Center, UiTheme.InkText, outline: true)
                    : UiFactory.Text(slot, "Name", UiFactory.Center, Vector2.zero, new Vector2(width - 12, height), 24,
                        TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
                label.text = skills[i].displayName;
                skillSlots.Add((skills[i], slot.GetComponent<Image>(), shade, label));
            }
        }

        static readonly string[] ActiveKeys = { "1", "2", "3" };

        /// <summary>액티브 스킬 버튼 (4.4): 사용 가능(이름 + 남은 횟수), 사용 불가(0회, 회색), 미습득(잠김)</summary>
        void BuildActiveBar()
        {
            if (battle.Actives == null) return;
            const float Size = 112, Gap = 18;
            int i = 0;
            foreach (ActiveSkillId id in Enum.GetValues(typeof(ActiveSkillId)))
            {
                var skill = id;
                var pos = new Vector2(-32 - (2 - i) * (Size + Gap), 40);
                var button = UiFactory.Button(root, $"Active{id}", UiFactory.BottomRight, pos, new Vector2(Size, Size), "", 24,
                    () => battle.Actives.Use(skill), FrameStyle.ButtonGold);
                bottomGroup.Add(button.gameObject);
                var rect = (RectTransform)button.transform;
                var icon = UiFactory.Icon(rect, "Icon", UiFactory.Center, new Vector2(0, 10), new Vector2(64, 64), battle.Stage != null ? ActiveIcon(id) : null);
                activeIcons.Add(icon);
                activeButtons.Add((id, button, button.GetComponentInChildren<TMP_Text>()));
                i++;
            }
        }

        static string ActiveIcon(ActiveSkillId id) => id switch
        {
            ActiveSkillId.Meteor => "meteor",
            ActiveSkillId.Hurricane => "upgrade-tailspinSize",
            _ => "upgrade-apocalypse",
        };

        /// <summary>피격 연출: 화면 네 가장자리에 붉은 띠</summary>
        void BuildHurtEdges()
        {
            var group = UiFactory.Stretch(root, "HurtEdges");
            hurtEdges = group.gameObject.AddComponent<CanvasGroup>();
            hurtEdges.blocksRaycasts = false;
            hurtEdges.alpha = 0;
            var color = new Color(0.9f, 0.1f, 0.1f, 0.55f);
            void Edge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
            {
                var r = UiFactory.Rect(group, name, UiFactory.Center, Vector2.zero, size);
                r.anchorMin = anchorMin;
                r.anchorMax = anchorMax;
                r.pivot = (anchorMin + anchorMax) / 2f;
                r.anchoredPosition = Vector2.zero;
                var image = r.gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
            const float T = 46;
            Edge("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, T));
            Edge("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, T));
            Edge("Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(T, 0));
            Edge("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(T, 0));
        }

        void UpdateActiveBar()
        {
            var actives = battle.Actives;
            if (actives == null) return;
            for (int i = 0; i < activeButtons.Count; i++)
            {
                var (id, button, label) = activeButtons[i];
                int level = actives.Level(id), left = actives.UsesLeft(id);
                string name = id switch { ActiveSkillId.Meteor => "메테오", ActiveSkillId.Hurricane => "허리케인", _ => "지진" };
                bool usable = actives.CanUse(id);
                button.interactable = usable;
                var icon = activeIcons[i];
                if (icon != null)
                {
                    // UI 패키지 슬롯: 잠김 / 사용 가능(준비) / 0회(비활성). 아이콘 아래에 남은 횟수와 키
                    var slot = UiTheme.KitSprite(level == 0 ? "slot-locked" : usable ? "slot-ready" : "slot-disabled");
                    if (button.targetGraphic is Image image && image.sprite != slot)
                    {
                        button.transition = Selectable.Transition.ColorTint;
                        image.sprite = slot;
                    }
                    icon.color = usable ? Color.white : new Color(0.5f, 0.45f, 0.47f, 1f);
                    label.color = UiTheme.InkText;
                    label.alignment = TextAlignmentOptions.Bottom;
                    label.text = level == 0 ? "<size=18>잠김</size>" : $"<size=22>×{left}</size> <size=14>[{ActiveKeys[i]}]</size>";
                    continue;
                }
                var style = level == 0 ? FrameStyle.ButtonDark : FrameStyle.ButtonGold;
                if (button.targetGraphic is Image fallback && fallback.sprite != UiFactory.SpriteFor(style))
                {
                    UiFactory.Frame(button.gameObject, style);
                    label.color = UiFactory.LabelColorFor(style);
                }
                label.text = level == 0 ? $"{name}\n<size=18>잠김</size>"
                    : $"{name}\n<size=30>×{left}</size>\n<size=16>[{ActiveKeys[i]}]</size>";
            }

            // 키보드 1·2·3. 일시정지 중(timeScale 0)에는 받지 않는다
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || Time.timeScale <= 0) return;
            if (keyboard.digit1Key.wasPressedThisFrame) actives.Use(ActiveSkillId.Meteor);
            if (keyboard.digit2Key.wasPressedThisFrame) actives.Use(ActiveSkillId.Hurricane);
            if (keyboard.digit3Key.wasPressedThisFrame) actives.Use(ActiveSkillId.Earthquake);
        }

        void UpdateSkillBar()
        {
            foreach (var (state, frame, shade, label) in skillSlots)
            {
                if (kitSlots)
                {
                    // 발동 중 > 쿨다운 > 준비 완료 (UI 패키지 상태 우선순위)
                    frame.sprite = UiTheme.KitSprite(state.active ? "slot-active" : state.CooldownRatio > 0.001f ? "slot-normal" : "slot-ready");
                }
                else
                {
                    var style = state.active ? FrameStyle.ChipGold : FrameStyle.ChipCream;
                    UiFactory.Frame(frame.gameObject, style);
                    label.color = UiFactory.LabelColorFor(style);
                }
                float ratio = state.active ? 0 : state.CooldownRatio;
                shade.anchorMax = new Vector2(1, ratio);
                shade.gameObject.SetActive(ratio > 0.001f);
            }
        }

        void BuildBanner()
        {
            banner = UiFactory.Panel(root, "Banner", UiFactory.Center, new Vector2(0, 170), new Vector2(900, 170), FrameStyle.Ink);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerGroup.blocksRaycasts = false;
            bannerTitle = UiFactory.Text(banner, "Title", UiFactory.TopCenter, new Vector2(0, -14), new Vector2(860, 90), 70, TextAlignmentOptions.Center,
                UiTheme.Gold, bold: true, outline: true);
            bannerSub = UiFactory.Text(banner, "Sub", UiFactory.BottomCenter, new Vector2(0, 16), new Vector2(860, 50), 30, TextAlignmentOptions.Center);
        }

        public void ShowBanner(string title, string sub)
        {
            bannerTitle.text = title;
            bannerSub.text = sub;
            bannerAge = 0;
        }

        void OnBurst(Building building, double gold)
        {
            bool golden = building.IsGilded || building.Type == BuildingType.Golden;
            if (!golden && building.Type != BuildingType.Landmark) return;
            string what = building.Type == BuildingType.Landmark ? "랜드마크 파괴!" : "황금 건물!";
            ShowToast($"{what} <color=#{UiTheme.HexString(UiTheme.Gold)}>+{NumberFormat.ToKorean(gold)}</color>");
        }

        void OnAirBurst(AirUnit unit, double gold) =>
            ShowToast($"{unit.UnitName} 격추! <color=#{UiTheme.HexString(UiTheme.Gold)}>+{NumberFormat.ToKorean(gold)}</color>");

        public void ShowToast(string text)
        {
            var rect = UiFactory.Panel(toastLayer, "Toast", UiFactory.TopRight, Vector2.zero, new Vector2(460, 52), FrameStyle.Ink);
            UiFactory.Text(rect, "Text", UiFactory.Center, Vector2.zero, new Vector2(440, 52), 26, TextAlignmentOptions.Right, bold: true).text = text;
            toasts.Add(new Toast { rect = rect, group = rect.gameObject.AddComponent<CanvasGroup>() });
        }

        void OnDestroy()
        {
            if (battle != null && battle.Drops != null)
            {
                battle.Drops.Burst -= OnBurst;
                battle.Drops.AirBurst -= OnAirBurst;
            }
        }

        void Update()
        {
            if (battle == null) return;
            float dt = Time.unscaledDeltaTime;
            bool showBottom = !battle.InStarter;
            if (showBottom != bottomShown)
            {
                bottomShown = showBottom;
                foreach (var go in bottomGroup) if (go != null) go.SetActive(showBottom);
            }
            if (pendingBanner is { } pending && !battle.InStarter)
            {
                ShowBanner(pending.title, pending.sub);
                pendingBanner = null;
            }
            float blink = Mathf.PingPong(Time.unscaledTime * 3f, 1f);

            double session = battle.Drops.CollectedGold;
            if (session > lastSession) goldPunch = 1f;
            lastSession = session;
            goldText.text = NumberFormat.ToKorean((ownedGold?.Invoke() ?? 0) + session);
            sessionText.text = $"+{NumberFormat.ToKorean(carriedGold + session)}";
            goldPunch = Mathf.Max(0, goldPunch - dt * 5f);
            goldRect.localScale = Vector3.one * (1f + 0.12f * goldPunch);

            var v = battle.Dragon.Vitals;
            double hpRatio = v.Hp / v.MaxHp, staminaRatio = v.Stamina / v.MaxStamina;
            UiFactory.SetFill(hpFill, hpRatio);
            UiFactory.SetFill(staminaFill, staminaRatio);
            hpText.text = $"{NumberFormat.ToKorean(v.Hp)} / {NumberFormat.ToKorean(v.MaxHp)}";
            staminaText.text = $"{NumberFormat.ToKorean(v.Stamina)} / {NumberFormat.ToKorean(v.MaxStamina)}";
            Blink(hpChip, hpFillImage, UiTheme.Hp, hpRatio < LowHp && battle.State == BattleState.Playing, blink);
            Blink(staminaChip, staminaFillImage, UiTheme.Energy, staminaRatio < LowStamina && battle.State == BattleState.Playing, blink);
            fatigueText.text = v.DrainMultiplier > 1
                ? $"피로 ×{v.Fatigue:0} · 기력 <color=#{UiTheme.HexString(UiTheme.Hp)}>-{v.StaminaDrainPerSecond:0}/초</color>"
                : $"피로 ×{v.Fatigue:0} · 기력 -{v.Fatigue:0}/초";

            int destroyed = battle.DestroyedCount, total = battle.Buildings.Count;
            UiFactory.SetFill(progressFill, total > 0 ? (double)destroyed / total : 0);
            progressText.text = battle.Mode == BattleMode.Bonus
                ? $"황금 건물 {destroyed}/{total}채 · 기력 소모 ×2 · 회복 없음"
                : $"파괴 {destroyed} / {total}";
            if (battle.Mode == BattleMode.Bonus)
                progressFillImage.color = Color.Lerp(UiTheme.Gold, UiTheme.Cream, Mathf.PingPong(Time.unscaledTime * 1.5f, 1f) * 0.6f);

            bool guided = battle.Dragon.IsGuided;
            UiFactory.Frame(modeChip.gameObject, guided ? FrameStyle.ChipGold : FrameStyle.ChipCream);
            modeText.color = UiFactory.LabelColorFor(guided ? FrameStyle.ChipGold : FrameStyle.ChipCream);
            modeText.text = guided ? "유도 중" : "자동 이동";

            UpdateSkillBar();
            UpdateActiveBar();
            hurtFlash = Mathf.Max(0, hurtFlash - dt * 3f);
            hurtEdges.alpha = hurtFlash;
            UpdateCombo(dt);
            UpdateEnd();
            UpdateBanner(dt);
            UpdateToasts(dt);

            if (tip != null)
            {
                tipAge += dt;
                tip.alpha = Mathf.Clamp01(Mathf.Min(tipAge * 2f, (9f - tipAge) / 1.5f));
                if (tipAge > 9f) { Destroy(tip.gameObject); tip = null; }
            }
        }

        static void Blink(Image chip, Image fill, Color color, bool low, float blink)
        {
            var c = low ? Color.Lerp(color, Color.white, blink * 0.7f) : color;
            chip.color = c;
            fill.color = c;
        }

        void UpdateCombo(float dt)
        {
            var drops = battle.Drops;
            bool active = drops.ChainActive;
            if (active && drops.ChainStacks > lastStacks) comboPunch = 1f;
            lastStacks = active ? drops.ChainStacks : 0;

            comboGroup.alpha = Mathf.MoveTowards(comboGroup.alpha, active ? 1f : 0f, dt * (active ? 8f : 2.5f));
            if (!active) return;
            comboCount.text = $"{drops.ChainStacks + 1}<size=48> 콤보</size>";
            comboBonus.text = drops.ChainGoldBonus > 0 ? $"골드 +{drops.ChainGoldBonus * 100:0.#}%" : "연쇄 파괴!";
            UiFactory.SetFill(comboTimer, drops.ChainTimeLeft);
            comboPunch = Mathf.Max(0, comboPunch - dt * 5f);
            combo.localScale = Vector3.one * (1f + 0.25f * comboPunch);
        }

        void UpdateEnd()
        {
            if (battle.State == lastState) return;
            lastState = battle.State;
            var stats = battle.Stats;
            if (battle.State == BattleState.Cleared)
            {
                string sub = battle.Mode == BattleMode.Bonus ? "황금 마을을 다 털었어요"
                    : stats.StaminaOnClear > 0 ? $"승전보! 기력 +{stats.StaminaOnClear:0}" : "마을을 다녀갔어요";
                ShowBanner("다녀가용!", sub);
            }
            else if (battle.State == BattleState.Failed)
            {
                bool dead = battle.Dragon.Vitals.IsDead;
                ShowBanner(dead ? "쓰러졌다!" : "기력 고갈!", dead ? "체력이 바닥났어요" : "용이 지쳐 버렸어요");
            }
        }

        void UpdateBanner(float dt)
        {
            if (bannerAge > BannerSeconds) { bannerGroup.alpha = 0; return; }
            bannerAge += dt;
            float t = bannerAge / BannerSeconds;
            bannerGroup.alpha = Mathf.Clamp01(Mathf.Min(t * 8f, (1f - t) * 5f));
            banner.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t * 6f));
        }

        void UpdateToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var toast = toasts[i];
                toast.age += dt;
                if (toast.age > ToastSeconds)
                {
                    Destroy(toast.rect.gameObject);
                    toasts.RemoveAt(i);
                }
            }
            for (int i = 0; i < toasts.Count; i++)
            {
                var toast = toasts[i];
                int slot = toasts.Count - 1 - i;
                toast.rect.anchoredPosition = Vector2.Lerp(toast.rect.anchoredPosition, new Vector2(0, -slot * ToastGap), dt * 12f);
                toast.group.alpha = Mathf.Clamp01(Mathf.Min(toast.age * 6f, (ToastSeconds - toast.age) * 3f));
            }
        }
    }
}
