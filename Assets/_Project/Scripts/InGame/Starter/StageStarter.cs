using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using DWH.InGame.Fx;
using DWH.InGame.Skills;
using DWH.InGame.Stage;
using DWH.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DWH.InGame.Starter
{
    /// <summary>스타터 클릭 입력. 테스트는 게이지 상태를 보고 누르는 입력을 넣는다</summary>
    public interface IStarterInput
    {
        bool Pressed(StageStarter starter);
    }

    /// <summary>
    /// 마우스 왼쪽 버튼 또는 스페이스. 버튼 같은 조작 UI(일시정지 버튼 등) 위 클릭과 일시정지 중 입력만 무시한다.
    /// 게이지나 HUD 패널 위를 눌러도 판정은 된다.
    /// </summary>
    public class MouseStarterInput : IStarterInput
    {
        static readonly List<RaycastResult> Hits = new();

        public bool Pressed(StageStarter starter)
        {
            if (Time.timeScale <= 0) return false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;
            return !OverControl(mouse.position.ReadValue());
        }

        static bool OverControl(Vector2 screen)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            Hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screen }, Hits);
            return Hits.Any(h => h.gameObject.GetComponentInParent<Selectable>() != null);
        }
    }

    public enum StarterPhase { Intro, Aiming, Gauge, Result, Drop, Done }

    /// <summary>
    /// 스테이지 스타터 (가이드 6.9, 임시unity제작.md 4.7). 일반 스테이지를 시작할 때마다 전투 전에 한 번 진행한다.
    /// 시간은 const.starter_*, 게이지는 starter_stage, 판정 배율·범위·표시 이미지는 starter_grade.
    /// 1) "Game Start!"와 함께 맵 전체가 보이게 줌아웃 2) 드래곤이 상공에서 맵 양 끝을 왕복하고 아래로 빨간 표적이 따라온다 → 클릭
    /// 3) 게이지(판정 영역과 판정 바가 다른 속도로 왕복) → 클릭해 판정 4) 결과 표시 0.5초 → 화면 위로 이탈 0.5초 → 같은 x로 낙하 타격.
    /// 연출 중에는 전투가 멈춰 있다.
    /// </summary>
    public class StageStarter : MonoBehaviour
    {
        float IntroSeconds, ZoomSeconds, AlertSeconds, ResultSeconds, LeaveSeconds, DropSeconds, FlyLapSeconds;

        GameDatabase db;
        ArtCatalog art;
        Image judgement;
        BattleController battle;
        Camera view;
        BattleCamera follow;
        IStarterInput input;
        float normalSize, groundY;
        RectTransform canvas, gaugeRoot, zoneRect, greatRect, barRect;
        Image barImage;
        RectTransform hintBack;

        void Update()
        {
            if (hintBack != null) hintBack.gameObject.SetActive(hint != null && !string.IsNullOrEmpty(hint.text));
        }
        TMP_Text title, hint;
        GameObject marker, line;

        public StarterPhase Phase { get; private set; } = StarterPhase.Intro;
        public StarterGauge Gauge { get; private set; }
        public StarterGrade? Grade { get; private set; }
        public Building Target { get; private set; }
        public float TargetX { get; private set; }
        public int HitCount { get; private set; }
        /// <summary>판정한 순간의 바 위치 (테스트용)</summary>
        public float JudgedBar { get; private set; }
        public event Action<StageStarter> Finished;

        public void Init(GameDatabase database, ArtCatalog catalog, BattleController owner, Camera camera, BattleCamera followCamera, IStarterInput starterInput)
        {
            db = database;
            art = catalog;
            IntroSeconds = (float)db.Const("starter_intro_sec");
            ZoomSeconds = (float)db.Const("starter_zoom_sec");
            FlyLapSeconds = (float)db.Const("starter_fly_lap_sec");
            AlertSeconds = (float)db.Const("starter_alert_sec");
            ResultSeconds = (float)db.Const("starter_result_sec");
            LeaveSeconds = (float)db.Const("starter_leave_sec");
            DropSeconds = (float)db.Const("starter_drop_sec");
            battle = owner;
            view = camera;
            follow = followCamera;
            input = starterInput;
            normalSize = camera.orthographicSize;
            groundY = Tuning.GroundHeight;
            Gauge = StarterGauge.ForStage(db.StarterStage(battle.Stage.stageId));
            BuildUi();
            StartCoroutine(Run());
        }

        void BuildUi()
        {
            canvas = UiFactory.Canvas("Starter", 3);
            // 안내 문구와 게이지는 클릭을 가로채지 않는다
            Destroy(canvas.GetComponent<GraphicRaycaster>());
            // 드래곤은 화면 가운데보다 조금 위를 난다. 글자·안내·게이지는 그 아래 빈 하늘에 둬서 드래곤과 지면의 건물을 가리지 않는다 (DWH-56)
            title = UiFactory.Text(canvas, "Title", UiFactory.Center, new Vector2(0, -150), new Vector2(1400, 180), 120, TextAlignmentOptions.Center,
                UiTheme.Gold, bold: true, outline: true);
            // 밝은 하늘 위에서도 읽히게 안내 문구 뒤에 어두운 띠를 깐다. 문구가 없으면 띠도 숨긴다 (Update)
            hintBack = UiFactory.Panel(canvas, "HintBack", UiFactory.BottomCenter, new Vector2(0, 412), new Vector2(1240, 76), FrameStyle.Ink);
            hint = UiFactory.Text(canvas, "Hint", UiFactory.BottomCenter, new Vector2(0, 420), new Vector2(1200, 60), 34, TextAlignmentOptions.Center,
                bold: true, outline: true);

            gaugeRoot = UiFactory.Panel(canvas, "Gauge", UiFactory.BottomCenter, new Vector2(0, 310), new Vector2(900, 80), FrameStyle.Ink);
            var track = UiFactory.Stretch(gaugeRoot, "Track", 12);
            zoneRect = UiFactory.Rect(track, "Good", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 56));
            zoneRect.gameObject.AddComponent<Image>().color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.8f);
            greatRect = UiFactory.Rect(zoneRect, "Great", UiFactory.Center, Vector2.zero, new Vector2(0, 56));
            greatRect.gameObject.AddComponent<Image>().color = UiTheme.Jade;
            barRect = UiFactory.Rect(track, "Bar", new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 76));
            barImage = barRect.gameObject.AddComponent<Image>();
            barImage.color = Color.white;
            gaugeRoot.gameObject.SetActive(false);

            // 판정 표시 이미지 (starter_grade.res_id)
            judgement = UiFactory.Rect(canvas, "Judgement", UiFactory.Center, new Vector2(0, -60), new Vector2(576, 216)).gameObject.AddComponent<Image>();
            judgement.preserveAspect = true;
            judgement.raycastTarget = false;
            judgement.gameObject.SetActive(false);
        }

        IEnumerator Run()
        {
            var dragon = battle.Dragon;
            float left = battle.StageLeft, right = battle.StageRight;
            float centerX = (left + right) / 2f;
            float startX = view.transform.position.x;
            var normalScale = dragon.transform.localScale;
            // 줌아웃: 건물 배치 전체 + 양옆 여백(상공을 나는 드래곤 몸 절반)이 화면 폭에 들어오게 한다.
            // 여백이 없으면 맵 끝을 날 때 드래곤이 화면 밖으로 잘린다 (DWH-56). 드래곤 크기가 줌에 따라 바뀌어 두 번 맞춘다
            float zoomSize = Mathf.Max(normalSize, (right - left) / (2f * view.aspect));
            float sidePad = 0;
            for (int i = 0; i < 2; i++)
            {
                sidePad = SkyHalfWidth(dragon, normalScale, zoomSize) + 0.4f;
                zoomSize = Mathf.Max(normalSize, (right - left + 2f * sidePad) / (2f * view.aspect));
            }

            // 1) Game Start! + 줌아웃. 드래곤은 처음부터 땅이 아닌 화면 상공 정중앙에 있다
            Phase = StarterPhase.Intro;
            title.text = UiText.Get("ui_starter_start");
            float scale = zoomSize / normalSize;
            dragon.Rig.SetFacing(1);
            for (float t = 0; t < IntroSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / ZoomSeconds));
                float size = Mathf.Lerp(normalSize, zoomSize, k);
                SetCamera(size, Mathf.Lerp(startX, centerX, k));
                PlaceInSky(dragon, normalScale, size);
                title.alpha = Mathf.Clamp01((IntroSeconds - t) / 0.4f);
                yield return null;
            }
            SetCamera(zoomSize, centerX);
            PlaceInSky(dragon, normalScale, zoomSize);
            title.text = "";
            title.alpha = 1;

            // 2) 상공 왕복 + 표적 → 클릭. 정중앙에서 출발한다
            Phase = StarterPhase.Aiming;
            hint.text = UiText.Get("ui_starter_aim");
            float flyY = SkyY(zoomSize);
            // 표적은 건물을 가리지 않게 지면 아래 흙 띠에 둔다
            marker = Quad("StarterTarget", new Vector2(0.9f * scale, Mathf.Min(0.35f * scale, groundY * 0.8f)), new Color(0.95f, 0.15f, 0.15f, 0.9f));
            line = Quad("StarterLine", new Vector2(0.06f * scale, 1), new Color(0.95f, 0.15f, 0.15f, 0.5f));
            // 상공 왕복 범위: 화면 양 끝에서 드래곤 몸 절반 안쪽 (맵 끝 건물 위까지는 간다)
            float viewHalf = zoomSize * view.aspect;
            float minX = centerX - viewHalf + sidePad, maxX = centerX + viewHalf - sidePad, travel = 0;
            float speed = (maxX - minX) * 2f / FlyLapSeconds;
            float x = centerX;
            travel = Mathf.Clamp(centerX - minX, 0, maxX - minX);
            PlaceMarker(x, flyY);
            yield return null;
            while (true)
            {
                if (input.Pressed(this)) break;
                travel += speed * Time.deltaTime;
                float span = maxX - minX;
                float m = travel % (span * 2f);
                x = minX + (m <= span ? m : span * 2f - m);
                dragon.Rig.SetFacing(m <= span ? 1 : -1);
                dragon.transform.position = new Vector3(x, flyY, 0);
                PlaceMarker(x, flyY);
                yield return null;
            }
            TargetX = x;

            // 3) 게이지 → 클릭해 판정
            Phase = StarterPhase.Gauge;
            hint.text = UiText.Get("ui_starter_gauge");
            gaugeRoot.gameObject.SetActive(true);
            DrawGauge();
            yield return null;
            // 클릭한 순간 화면에 보이는 바 위치로 판정한다: 먼저 입력을 보고, 누르지 않았을 때만 움직인다
            while (!input.Pressed(this))
            {
                Gauge.Advance(Time.deltaTime);
                DrawGauge();
                yield return null;
            }
            Grade = Gauge.Judge();
            JudgedBar = Gauge.Bar;

            // 4) "!!" → 결과: 바를 멈춘 채 판정 색과 판정 이미지를 보여 준다 → 이탈 → 낙하 타격
            Phase = StarterPhase.Result;
            barImage.color = Grade switch { StarterGrade.Great => UiTheme.Jade, StarterGrade.Good => UiTheme.Gold, _ => UiTheme.Hp };
            barRect.sizeDelta = new Vector2(18, barRect.sizeDelta.y);
            DrawGauge();
            hint.text = "";
            title.text = "!!";
            title.color = UiTheme.Gold;
            yield return new WaitForSeconds(AlertSeconds);
            var gradeRow = db.StarterGrade(Grade.Value);
            var gradeSprite = art != null ? art.SpriteOrNull(gradeRow.resId) : null;
            if (gradeSprite != null)
            {
                title.text = "";
                judgement.sprite = gradeSprite;
                judgement.gameObject.SetActive(true);
            }
            else
            {
                title.text = db.Text(gradeRow.nameStr) + (Grade == StarterGrade.Fail ? "..." : "!!");
                title.color = Grade == StarterGrade.Fail ? UiTheme.LineIdle : UiTheme.Gold;
            }
            yield return new WaitForSeconds(ResultSeconds);
            judgement.gameObject.SetActive(false);

            Phase = StarterPhase.Drop;
            gaugeRoot.gameObject.SetActive(false);
            float top = zoomSize * 2f + dragon.Hitbox.height * 2f;
            for (float t = 0; t < LeaveSeconds; t += Time.deltaTime)
            {
                dragon.transform.position = new Vector3(TargetX, Mathf.Lerp(flyY, top, t / LeaveSeconds), 0);
                PlaceMarker(TargetX, dragon.transform.position.y);
                yield return null;
            }
            title.text = "";

            // 카메라를 원래 크기로 되돌리고 낙하 지점을 따라가게 한다
            dragon.transform.localScale = normalScale;
            view.orthographicSize = normalSize;
            follow.Suspended = false;
            float dropFrom = normalSize * 2f + dragon.Hitbox.height;
            for (float t = 0; t < DropSeconds; t += Time.deltaTime)
            {
                dragon.transform.position = new Vector3(TargetX, Mathf.Lerp(dropFrom, groundY, t / DropSeconds), 0);
                yield return null;
            }
            dragon.transform.position = new Vector3(TargetX, groundY, 0);
            Impact();
            Finish();
        }

        /// <summary>상공 높이: 화면 높이의 약 57% (카메라 아래 끝이 지면 바닥 0). 왼쪽 위 HUD에 가리지 않게 2/3에서 내렸다 (DWH-56)</summary>
        static float SkyY(float cameraSize) => cameraSize * 1.15f;

        /// <summary>상공을 나는 드래곤 그림의 가로 절반 (날개·꼬리 포함 근사: 몸 판정 폭의 0.8배)</summary>
        float SkyHalfWidth(DragonController dragon, Vector3 normalScale, float cameraSize)
        {
            float grow = Mathf.Max(1f, cameraSize / normalSize * 0.7f);
            float baseWidth = dragon.Hitbox.width / Mathf.Max(0.01f, dragon.transform.localScale.x / Mathf.Max(0.01f, normalScale.x));
            return baseWidth * grow * 0.8f;
        }

        /// <summary>현재 카메라 화면의 가로 정중앙 상공에 드래곤을 둔다. 줌아웃할수록 작아 보이므로 함께 키운다</summary>
        void PlaceInSky(DragonController dragon, Vector3 normalScale, float cameraSize)
        {
            dragon.transform.localScale = normalScale * Mathf.Max(1f, cameraSize / normalSize * 0.7f);
            dragon.transform.position = new Vector3(view.transform.position.x, SkyY(cameraSize), 0);
        }

        void SetCamera(float size, float x)
        {
            follow.Suspended = true;
            view.orthographicSize = size;
            view.transform.position = new Vector3(x, size, view.transform.position.z);
        }

        GameObject Quad(string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GrayboxSprites.Square;
            sr.color = color;
            sr.sortingOrder = SortingOrders.Projectile;
            return go;
        }

        void PlaceMarker(float x, float dragonY)
        {
            if (marker != null) marker.transform.position = new Vector3(x, groundY - marker.transform.localScale.y / 2f, 0);
            if (line != null)
            {
                float h = Mathf.Max(0.1f, dragonY - groundY);
                line.transform.position = new Vector3(x, groundY + h / 2f, 0);
                line.transform.localScale = new Vector3(line.transform.localScale.x, h, 1);
            }
        }

        void DrawGauge()
        {
            var track = (RectTransform)zoneRect.parent;
            float w = track.rect.width;
            zoneRect.anchoredPosition = new Vector2(Gauge.ZoneStart * w, 0);
            zoneRect.sizeDelta = new Vector2(Gauge.GoodWidth * w, zoneRect.sizeDelta.y);
            greatRect.sizeDelta = new Vector2(Gauge.GreatWidth * w, greatRect.sizeDelta.y);
            barRect.anchoredPosition = new Vector2(Gauge.Bar * w - barRect.sizeDelta.x / 2f, 0);
        }

        /// <summary>표적 = 낙하 x 아래(또는 가장 가까운) 건물. 그 건물과 양옆 N개씩을 공격력 × 배율로 친다. 맵 끝이면 있는 건물만 (T-12)</summary>
        void Impact()
        {
            var row = db.StarterGrade(Grade.Value);
            double multiplier = row.dmgPct / 100.0;
            var alive = battle.Buildings.Where(b => b.IsAlive).OrderBy(b => b.CenterX).ToList();
            if (alive.Count > 0)
            {
                Target = alive.OrderBy(b => b.Left <= TargetX && TargetX <= b.Right ? 0 : Mathf.Abs(b.CenterX - TargetX)).First();
                if (multiplier > 0)
                {
                    int i = alive.IndexOf(Target);
                    var hits = alive.Skip(Math.Max(0, i - row.sideCount)).Take(Math.Min(alive.Count, i + row.sideCount + 1) - Math.Max(0, i - row.sideCount)).ToList();
                    double amount = Damage.Compute(battle.Stats.AttackPower, multiplier, false, 1);
                    foreach (var b in hits) b.TakeDamage(amount, false);
                    HitCount = hits.Count;
                }
            }
            SkillFx.Burst(transform, db, art, FxIds.Landing, new Vector2(TargetX, groundY + 0.6f), multiplier > 0 ? 3f : 1.5f, new Color(1f, 0.8f, 0.4f, 0.8f));
            follow.Shake(0.3f, multiplier > 0 ? 0.25f : 0.08f);
        }

        void Finish()
        {
            Phase = StarterPhase.Done;
            if (marker != null) Destroy(marker);
            if (line != null) Destroy(line);
            if (canvas != null) Destroy(canvas.gameObject);
            Finished?.Invoke(this);
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
