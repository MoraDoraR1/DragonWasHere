using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Stage;
using DWH.InGame.Starter;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 튜토리얼 (기획서 20261012_튜토리얼기획서_Ver03). 게임 시작 후 인게임, 인게임 종료 후 아웃게임 조작을 알려 준다. 종류마다 한 번 (새 게임을 누르면 처음부터 다시).
    /// 진행 순서: 스타터 → 인게임 → 정산 → 강화 (환생 탭을 처음 누르면 환생).
    /// - 스타터: 표적 고르기 시작(1쪽, X로 닫으면 진행) → 위치 확정 후 게이지 시작(2·3쪽)
    /// - 인게임: 스타터 착지 2초 뒤 4쪽 (마우스 방향 이동 · 마우스 유도 + 근경은 유도 안 됨 · 모드 버튼/Space + 보조 스킬/숫자키 · 게임 오버 조건).
    ///   2·3쪽은 한 쪽에 안내 카드가 두 개다(두 번째 카드는 버튼 없는 보조 카드)
    /// - 정산: 첫 결과 화면 1쪽 (정산 창 전체)
    /// - 강화: 아웃게임에 처음 들어오면 2쪽 (노드 선택 · 오른쪽 상세 창)
    /// - 환생: 환생 탭을 처음 열면 3쪽 (환생 강화 · 환생 포인트 · 액티브 해금 조건)
    /// 떠 있는 동안 조작·진행을 멈춘다(Time.timeScale 0, 화면 전체가 클릭을 막음). "다음"(또는 Space)으로 넘기고 마지막 쪽의 X로 닫는다. 오른쪽 위 SKIP은 지금 튜토리얼 전체를 건너뛴다.
    /// 끝낸 튜토리얼은 SaveData.tutorialsDone에 남는다. 테스트·봇(타이틀 없이 시작)은 GameManager.TutorialsEnabled가 꺼져 있다.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        public const string Starter = "starter", InGame = "ingame", Settle = "settle", Upgrade = "upgrade", RebirthTab = "rebirth";
        public static readonly string[] All = { Starter, InGame, Settle, Upgrade, RebirthTab };

        /// <summary>착지 뒤 인게임 튜토리얼까지 기다리는 시간 (기획서: 착지 판정 후 2초)</summary>
        const float InGameDelay = 2f;
        const float FrameThickness = 6f, FramePad = 10f, LineThickness = 5f;
        static readonly Color Red = new(0.92f, 0.12f, 0.12f, 1f);
        static readonly Vector2 CardSize = new(980, 280);
        /// <summary>대상이 커서(정산 창 등) 넓은 카드 자리가 없을 때 옆에 두는 좁은 카드</summary>
        static readonly Vector2 NarrowCardSize = new(600, 360);
        /// <summary>같은 쪽의 두 번째 안내 카드 (버튼 없음)</summary>
        static readonly Vector2 NoteSize = new(760, 170);

        class Page
        {
            public string textKey;
            /// <summary>강조할 곳 (틀 좌표). 없으면 null</summary>
            public Func<Rect?> target;
            /// <summary>이 쪽을 띄우기 직전에 할 일 (예: 노드 선택)</summary>
            public Action onShow;
            /// <summary>같은 쪽에 함께 띄우는 두 번째 안내 (없으면 null)</summary>
            public string noteKey;
            public Func<Rect?> noteTarget;
        }

        GameManager game;
        GameDatabase db;
        RectTransform frame, card, highlight, line, lineHead;
        RectTransform note, noteHighlight, noteLine, noteLineHead;
        TMP_Text body, counter, nextLabel, skipLabel, noteText;
        Button next;
        readonly List<Page> pages = new();
        int index;
        Action onDone;
        /// <summary>SKIP하면 끝낸 것으로 기록할 튜토리얼</summary>
        string skipId;
        float savedTimeScale = 1f;

        // 스타터·인게임 튜토리얼은 전투마다 상태를 다시 본다
        UnityEngine.Object watchedBattle;
        bool starterFirstShown;
        float sinceLanding;

        public bool IsOpen => card != null && frame.gameObject.activeSelf;
        /// <summary>
        /// 이번 프레임의 Space를 튜토리얼이 "다음"으로 썼는지. 마지막 쪽을 Space로 닫은 그 프레임에
        /// 같은 Space가 모드 바꾸기·출격·결과 확인·스타터 입력으로 한 번 더 쓰이지 않게 한다
        /// </summary>
        public static bool UsedKeyThisFrame => usedKeyFrame == Time.frameCount;
        static int usedKeyFrame = -1;
        /// <summary>지금 보이는 쪽의 문구 키 (테스트용)</summary>
        public string CurrentKey => IsOpen ? pages[index].textKey : null;

        public static TutorialOverlay Create(GameManager manager, GameDatabase database)
        {
            var root = UiFactory.Canvas("Tutorial", 40);
            var overlay = root.parent.gameObject.AddComponent<TutorialOverlay>();
            overlay.game = manager;
            overlay.db = database;
            overlay.frame = root;
            overlay.Build();
            root.gameObject.SetActive(false);
            return overlay;
        }

        void Build()
        {
            // 화면 전체를 덮어 아래 UI·게임 클릭을 막는다. 강조한 곳이 보이게 옅게만 어둡게
            var block = frame.parent.gameObject.AddComponent<Image>();
            block.color = new Color(0, 0, 0, 0);
            block.raycastTarget = false;   // 열려 있을 때만 막는다
            var dim = UiFactory.Stretch(frame, "Dim");
            dim.anchorMin = dim.anchorMax = new Vector2(0.5f, 0.5f);
            dim.sizeDelta = new Vector2(4000, 4000);
            dim.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.03f, 0.02f, 0.35f);

            highlight = MakeHighlight("Highlight");
            (line, lineHead) = MakeLine("Line");
            noteHighlight = MakeHighlight("NoteHighlight");
            (noteLine, noteLineHead) = MakeLine("NoteLine");

            card = UiFactory.Panel(frame, "Card", UiFactory.Center, Vector2.zero, CardSize, FrameStyle.Cream);
            // 문구 칸은 아래 버튼 줄(높이 86)과 20px 넘게 띄운다 (영어 문구가 닫기 버튼에 붙어 보였다, 10-14)
            body = UiFactory.Text(card, "Body", UiFactory.TopLeft, new Vector2(44, -32), new Vector2(CardSize.x - 88, 140), 30,
                TextAlignmentOptions.TopLeft, UiTheme.InkText);
            UiFactory.Fit(body, 20, false);
            counter = UiFactory.Text(card, "Counter", UiFactory.BottomLeft, new Vector2(44, 26), new Vector2(200, 44), 24,
                TextAlignmentOptions.Left, UiTheme.InkDim, bold: true);
            next = UiFactory.Button(card, "Next", UiFactory.BottomRight, new Vector2(-28, 22), new Vector2(220, 64), "", 28, OnNext);
            nextLabel = next.GetComponentInChildren<TMP_Text>();

            // 같은 쪽의 두 번째 안내 카드 (버튼 없음, 튜토리얼 Ver03 인게임 2·3쪽)
            note = UiFactory.Panel(frame, "Note", UiFactory.Center, Vector2.zero, NoteSize, FrameStyle.Cream);
            noteText = UiFactory.Text(note, "Body", UiFactory.Center, Vector2.zero, NoteSize - new Vector2(72, 40), 28,
                TextAlignmentOptions.Left, UiTheme.InkText);
            UiFactory.Fit(noteText, 18, false);

            // 모든 튜토리얼 화면 오른쪽 위: 지금 튜토리얼 전체 건너뛰기 (10-14 사용자 요청)
            var skip = UiFactory.Button(frame, "Skip", UiFactory.TopRight, new Vector2(-24, -24), new Vector2(180, 64), "", 30, OnSkip, FrameStyle.ButtonDark);
            skipLabel = skip.GetComponentInChildren<TMP_Text>();
        }

        RectTransform MakeHighlight(string name)
        {
            var box = UiFactory.Rect(frame, name, UiFactory.Center, Vector2.zero, Vector2.one);
            foreach (var (edgeName, min, max) in new[]
            {
                ("Top", new Vector2(0, 1), new Vector2(1, 1)), ("Bottom", new Vector2(0, 0), new Vector2(1, 0)),
                ("Left", new Vector2(0, 0), new Vector2(0, 1)), ("Right", new Vector2(1, 0), new Vector2(1, 1)),
            })
            {
                var edge = UiFactory.Rect(box, edgeName, UiFactory.Center, Vector2.zero, Vector2.zero);
                edge.anchorMin = min;
                edge.anchorMax = max;
                // 변 하나 = 앵커 선을 따라 두께만큼 (네 변이 모서리에서 겹쳐 닫힌다)
                edge.offsetMin = new Vector2(-FrameThickness / 2, -FrameThickness / 2);
                edge.offsetMax = new Vector2(FrameThickness / 2, FrameThickness / 2);
                var image = edge.gameObject.AddComponent<Image>();
                image.color = Red;
                image.raycastTarget = false;
            }
            return box;
        }

        (RectTransform line, RectTransform head) MakeLine(string name)
        {
            var bar = UiFactory.Rect(frame, name, UiFactory.Center, Vector2.zero, new Vector2(10, LineThickness));
            bar.pivot = new Vector2(0, 0.5f);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = Red;
            barImage.raycastTarget = false;
            var head = UiFactory.Rect(frame, name + "Head", UiFactory.Center, Vector2.zero, new Vector2(18, 18));
            var headImage = head.gameObject.AddComponent<Image>();
            headImage.color = Red;
            headImage.raycastTarget = false;
            return (bar, head);
        }

        // ───── 열고 닫기 ─────

        void Open(string id, IEnumerable<Page> sequence, Action done)
        {
            skipId = id;
            pages.Clear();
            pages.AddRange(sequence);
            if (pages.Count == 0) return;
            onDone = done;
            index = 0;
            savedTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            frame.gameObject.SetActive(true);
            frame.parent.GetComponent<Image>().raycastTarget = true;
            ShowPage();
        }

        void ShowPage()
        {
            var page = pages[index];
            // 마우스로 누른 "다음" 버튼이 선택된 채 남으면 키보드 제출과 Space가 겹쳐 두 쪽이 넘어갈 수 있다
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            page.onShow?.Invoke();
            Core.Analytics.TutorialPageShown();
            body.text = UiText.Get(page.textKey);
            note.gameObject.SetActive(page.noteKey != null);
            if (page.noteKey != null) noteText.text = UiText.Get(page.noteKey);
            skipLabel.text = UiText.Get("ui_tutorial_skip");
            counter.text = pages.Count > 1 ? $"{index + 1} / {pages.Count}" : "";
            bool last = index == pages.Count - 1;
            nextLabel.text = UiText.Get(last ? "ui_tutorial_close" : "ui_tutorial_next");
            UiFactory.Frame(next.gameObject, last ? FrameStyle.ButtonDark : FrameStyle.ButtonGold);
            nextLabel.color = UiFactory.LabelColorFor(last ? FrameStyle.ButtonDark : FrameStyle.ButtonGold);
            Layout();
        }

        /// <summary>"다음" 또는 마지막 쪽의 X(닫기)</summary>
        public void OnNext()
        {
            if (!IsOpen) return;
            Core.Analytics.TutorialStep(db, game.Save, StepId(pages[index]), false);
            if (index < pages.Count - 1)
            {
                index++;
                ShowPage();
                return;
            }
            Close();
        }

        /// <summary>SKIP: 지금 튜토리얼의 남은 쪽을 모두 건너뛰고 끝낸 것으로 기록한다 (스타터는 뒤의 게이지 쪽까지)</summary>
        public void OnSkip()
        {
            if (!IsOpen) return;
            Core.Analytics.TutorialStep(db, game.Save, StepId(pages[index]), true);
            string id = skipId;
            onDone = null;
            Close();
            if (id != null) Complete(id);
        }

        void Close()
        {
            frame.gameObject.SetActive(false);
            frame.parent.GetComponent<Image>().raycastTarget = false;
            Time.timeScale = game != null && game.IsPaused ? 0f : savedTimeScale;
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }

        void Complete(string id)
        {
            game.Save.MarkTutorial(id);
            game.SaveNow();
        }

        // ───── 언제 띄우나 ─────

        void Update()
        {
            if (game == null || !game.TutorialsEnabled || !game.IsInitialized) return;
            if (IsOpen)
            {
                Layout();   // 강조 대상이 움직이거나(드래곤) 화면이 늦게 만들어져도 따라간다
                // Space = "다음" (마지막 쪽이면 닫기), 모든 튜토리얼 공통 (10-14 사용자 요청)
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                {
                    usedKeyFrame = Time.frameCount;
                    OnNext();
                }
                return;
            }
            if (game.IsPaused || (game.Settings != null && game.Settings.IsOpen)) return;
            var save = game.Save;

            switch (game.State)
            {
                case GameState.Stage when game.Battle != null && game.Battle.Starter != null:
                    var battle = game.Battle;
                    if (watchedBattle != battle) { watchedBattle = battle; starterFirstShown = false; sinceLanding = 0; }
                    var phase = battle.Starter.Phase;
                    if (!save.TutorialDone(Starter))
                    {
                        if (phase == StarterPhase.Aiming && !starterFirstShown)
                            Open(Starter, new[] { P("ui_tutorial_starter_1") }, () => starterFirstShown = true);
                        else if (phase == StarterPhase.Gauge)
                            Open(Starter, new[] { P("ui_tutorial_starter_2", () => Ui("Starter", "Gauge")), P("ui_tutorial_starter_3") }, () => Complete(Starter));
                    }
                    else if (!save.TutorialDone(InGame) && phase == StarterPhase.Done && battle.State == BattleState.Playing)
                    {
                        sinceLanding += Time.deltaTime;
                        if (sinceLanding >= InGameDelay)
                            Open(InGame, new[]
                            {
                                P("ui_tutorial_ingame_1", DragonBody),
                                P("ui_tutorial_ingame_2", null, "ui_tutorial_ingame_2b", GroundZone),
                                P("ui_tutorial_ingame_3", () => Ui("HUD", "Mode"),
                                    "ui_tutorial_ingame_3b", () => Ui("HUD", "ActiveMeteor", "ActiveHurricane", "ActiveEarthquake")),
                                P("ui_tutorial_ingame_4", () => Ui("HUD", "HpChip", "HpBar", "StaminaChip", "StaminaBar")),
                            }, () => Complete(InGame));
                    }
                    break;

                // 정산: 첫 결과 화면 (정산 창 전체)
                case GameState.Result when !save.TutorialDone(Settle):
                    Open(Settle, new[] { P("ui_tutorial_settle_1", () => Ui("ResultScreen", "Card")) }, () => Complete(Settle));
                    break;

                // 강화: 정산 튜토리얼을 본 뒤 아웃게임(둥지)에 처음 들어오면 (순서: 정산 → 강화). 시작 노드를 골라 두고 노드 → 상세 창
                case GameState.OutGame when save.TutorialDone(Settle) && !save.TutorialDone(Upgrade) && game.OutGame != null && game.OutGame.Tab == 0:
                    var root = db.upgrades.FirstOrDefault(u => string.IsNullOrEmpty(u.prereqId1));
                    Open(Upgrade, new[]
                    {
                        new Page
                        {
                            textKey = "ui_tutorial_upgrade_1", target = () => root != null ? Ui("OutGameScreen", root.upgradeId) : null,
                            onShow = () => { if (root != null) game.OutGame.Select(root); },
                        },
                        P("ui_tutorial_upgrade_2", () => Ui("OutGameScreen", "Detail")),
                    }, () => Complete(Upgrade));
                    break;

                case GameState.OutGame when !save.TutorialDone(RebirthTab) && game.OutGame != null && game.OutGame.Tab == 1:
                    Open(RebirthTab, new[]
                    {
                        P("ui_tutorial_rebirth_1", () => Ui("OutGameScreen", "Tab1")),
                        P("ui_tutorial_rebirth_2", () => Ui("RebirthPanel", "Rebirth")),
                        P("ui_tutorial_rebirth_3", () => Ui("RebirthPanel", db.activeSkills.Select(a => a.skillId).ToArray())),
                    }, () => Complete(RebirthTab));
                    break;
            }
        }

        /// <summary>지표 tutorial_step의 step_id: 문구 키에서 앞부분을 뺀 것 (예: ingame_2)</summary>
        static string StepId(Page page) => page.textKey.Replace("ui_tutorial_", "");

        static Page P(string key, Func<Rect?> target = null, string noteKey = null, Func<Rect?> noteTarget = null) =>
            new() { textKey = key, target = target, noteKey = noteKey, noteTarget = noteTarget };

        // ───── 강조 대상 (틀 좌표) ─────

        /// <summary>이름으로 찾은 UI들을 감싸는 사각형. root는 활성 오브젝트 이름(캔버스·패널)</summary>
        Rect? Ui(string root, params string[] names)
        {
            var parent = GameObject.Find(root);
            if (parent == null) return null;
            Rect? box = null;
            foreach (var name in names)
            {
                var found = Deep(parent.transform, name) as RectTransform;
                if (found == null || !found.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4];
                found.GetWorldCorners(corners);
                var cam = CanvasCamera(found);
                box = Union(box, ScreenRect(RectTransformUtility.WorldToScreenPoint(cam, corners[0]), RectTransformUtility.WorldToScreenPoint(cam, corners[2])));
            }
            return box;
        }

        static Transform Deep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = Deep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>드래곤 몸</summary>
        Rect? DragonBody()
        {
            var dragon = game.Battle != null ? game.Battle.Dragon : null;
            if (dragon == null) return null;
            var box = dragon.Hitbox;
            return World(box.min, box.max);
        }

        /// <summary>근경: 마우스 유도에서 커서를 무시하는 아래 영역 (건물 HP바 아래 ~ 화면 아래, 화면 가로 전체)</summary>
        Rect? GroundZone()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, -cam.transform.position.z));
            var right = cam.ViewportToWorldPoint(new Vector3(1, 0, -cam.transform.position.z));
            var zone = World(bottomLeft, new Vector2(right.x, DWH.InGame.Dragon.MouseGuideInput.FloorY));
            // 화면 가장자리에 붙은 테두리가 잘리지 않게 안쪽으로 조금 줄인다
            return zone is Rect z ? Rect.MinMaxRect(z.xMin + 16, z.yMin + 16, z.xMax - 16, z.yMax) : null;
        }

        Rect? World(Vector2 min, Vector2 max)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            return ScreenRect(cam.WorldToScreenPoint(min), cam.WorldToScreenPoint(max));
        }

        /// <summary>캔버스가 그리는 카메라. 오버레이면 null (화면 캡처 테스트는 잠시 카메라 모드로 바꾼다)</summary>
        static Camera CanvasCamera(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            canvas = canvas != null ? canvas.rootCanvas : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        Rect ScreenRect(Vector2 screenMin, Vector2 screenMax)
        {
            var cam = CanvasCamera(frame);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screenMin, cam, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screenMax, cam, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static Rect Union(Rect? a, Rect b) => a is Rect r ? Rect.MinMaxRect(Mathf.Min(r.xMin, b.xMin), Mathf.Min(r.yMin, b.yMin), Mathf.Max(r.xMax, b.xMax), Mathf.Max(r.yMax, b.yMax)) : b;

        // ───── 배치 ─────

        /// <summary>
        /// 강조 테두리를 대상에 맞추고, 안내 카드는 대상·다른 카드와 겹치지 않는 자리(대상에 가까운 순)에 둔 뒤 카드 → 대상으로 선을 긋는다.
        /// 한 쪽에 카드가 두 개면 본 카드(버튼 있음)부터 자리를 잡는다
        /// </summary>
        void Layout()
        {
            var page = index < pages.Count ? pages[index] : null;
            Rect? mainTarget = page?.target?.Invoke();
            Rect? noteTarget = page?.noteKey != null ? page.noteTarget?.Invoke() : null;
            var blocked = new List<Rect>();
            if (mainTarget is Rect m) blocked.Add(Pad(m));
            if (noteTarget is Rect n) blocked.Add(Pad(n));

            var mainSpots = new[] { new Vector2(0, 220), new Vector2(0, -40), new Vector2(0, -300), new Vector2(0, 380), new Vector2(0, -400),
                new Vector2(-460, 220), new Vector2(460, 220), new Vector2(-460, -40), new Vector2(460, -40) };
            // 넓은 카드 자리가 없으면(대상이 화면 가운데를 크게 차지) 좁은 카드로 대상 옆에 둔다
            var cardSize = CardSize;
            if (!HasFreeSpot(CardSize, mainSpots, blocked))
            {
                var sideSpots = new[] { new Vector2(650, 120), new Vector2(-650, 120), new Vector2(650, -160), new Vector2(-650, -160) };
                if (HasFreeSpot(NarrowCardSize, sideSpots, blocked))
                {
                    cardSize = NarrowCardSize;
                    mainSpots = sideSpots;
                }
            }
            SizeCard(cardSize);
            var cardRect = Place(card, cardSize, mainSpots, mainTarget, blocked);
            Point(highlight, line, lineHead, cardRect, mainTarget);

            if (page?.noteKey != null)
            {
                blocked.Add(cardRect);
                var noteSpots = new[] { new Vector2(-560, 260), new Vector2(560, 260), new Vector2(-560, 20), new Vector2(560, 20),
                    new Vector2(-560, -220), new Vector2(560, -220), new Vector2(0, -220), new Vector2(0, 380) };
                var noteRect = Place(note, NoteSize, noteSpots, noteTarget, blocked);
                Point(noteHighlight, noteLine, noteLineHead, noteRect, noteTarget);
            }
            else Point(noteHighlight, noteLine, noteLineHead, default, null);
        }

        static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0 && h > 0 ? w * h : 0;
        }

        static bool HasFreeSpot(Vector2 size, Vector2[] spots, List<Rect> blocked) =>
            spots.Any(p => blocked.All(r => !new Rect(p - size / 2, size).Overlaps(r)));

        /// <summary>카드 크기와 문구 칸을 맞춘다 (문구 칸은 아래 버튼 줄과 20px 넘게 띄운다)</summary>
        void SizeCard(Vector2 size)
        {
            card.sizeDelta = size;
            body.rectTransform.sizeDelta = new Vector2(size.x - 88, size.y - 140);
        }

        static Rect Pad(Rect t) => new(t.x - FramePad, t.y - FramePad, t.width + FramePad * 2, t.height + FramePad * 2);

        /// <summary>겹치지 않는 자리 중 대상에 가장 가까운 곳 (모두 겹치면 첫 후보)</summary>
        static Rect Place(RectTransform box, Vector2 size, Vector2[] spots, Rect? target, List<Rect> blocked)
        {
            var order = target is Rect t ? spots.OrderBy(p => Vector2.Distance(p, t.center)).ToArray() : spots;
            // 모두 겹치면 겹치는 넓이가 가장 작은 곳 (정산 창처럼 큰 대상은 아래 버튼 줄 쪽을 덮고 제목·골드는 보이게)
            var pos = order.Cast<Vector2?>().FirstOrDefault(p => blocked.All(r => !new Rect(p.Value - size / 2, size).Overlaps(r)))
                ?? spots.OrderBy(p => blocked.Sum(r => OverlapArea(new Rect(p - size / 2, size), r))).First();
            box.anchoredPosition = pos;
            return new Rect(pos - size / 2, size);
        }

        /// <summary>대상 테두리와 카드 → 대상 선</summary>
        static void Point(RectTransform frameBox, RectTransform bar, RectTransform head, Rect from, Rect? target)
        {
            frameBox.gameObject.SetActive(target != null);
            bar.gameObject.SetActive(false);
            head.gameObject.SetActive(false);
            if (target is not Rect t) return;
            var padded = Pad(t);
            frameBox.anchoredPosition = padded.center;
            frameBox.sizeDelta = padded.size;
            var start = Closest(from, padded.center);
            var end = Closest(padded, start);
            var delta = end - start;
            if (delta.magnitude < 4f) return;
            bar.anchoredPosition = start;
            bar.sizeDelta = new Vector2(delta.magnitude, LineThickness);
            bar.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            head.anchoredPosition = end;
            head.localRotation = Quaternion.Euler(0, 0, 45);
            bar.gameObject.SetActive(true);
            head.gameObject.SetActive(true);
        }

        static Vector2 Closest(Rect rect, Vector2 point) =>
            new(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
    }
}
