using System;
using DWH.Data;
using DWH.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DWH.UI
{
    public class TitleActions
    {
        public Action NewGame;
        public Action Continue;
        public Action OpenSettings;
    }

    /// <summary>
    /// 타이틀 화면 (UI 기획서 2장 "타이틀 UI 구성").
    /// 배경 동영상(용이 건물을 부수는 장면, 입력 없음) · 로고(한/영) · 설정 · 새 게임 · 이어하기 · 버전.
    /// 새 게임: 저장 기록이 있으면 경고 팝업("네!" → 초기화 후 1-1). 이어하기: 저장이 없으면 비활성.
    /// 도감·미니게임 버튼은 MVP 범위 밖이라 넣지 않았다 (가이드 1장 제외 목록).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        static readonly Vector2 ButtonSize = new(440, 100);

        TitleActions actions;
        Button continueButton;
        RectTransform logo, warning;
        VideoPlayer video;
        RawImage videoImage;
        RenderTexture videoTexture;
        bool hasProgress;
        float shownAt;

        public bool IsOpen => gameObject.activeSelf;
        public bool WarningOpen => warning != null && warning.gameObject.activeSelf;
        public bool VideoPlaying => video != null && video.isPlaying && videoImage.enabled;

        public static TitleScreen Create(GameDatabase database, ArtCatalog art, TitleActions titleActions)
        {
            var root = UiFactory.Canvas("TitleScreen", 15);
            var screen = root.gameObject.AddComponent<TitleScreen>();
            screen.Build(root, database, art, titleActions);
            root.gameObject.SetActive(false);
            return screen;
        }

        void Build(RectTransform root, GameDatabase database, ArtCatalog art, TitleActions titleActions)
        {
            actions = titleActions;

            // 1. 배경: 동영상이 준비되기 전과 재생할 수 없을 때는 단색으로 둔다
            UiFactory.Stretch(root, "Backdrop").gameObject.AddComponent<Image>().color = UiTheme.Background;
            BuildVideo(root, database);
            var shade = UiFactory.Stretch(root, "Shade").gameObject.AddComponent<Image>();
            shade.color = new Color(0.08f, 0.05f, 0.05f, 0.25f);
            shade.raycastTarget = false;

            // 2. 로고 (언어에 따라 한글·영문)
            string logoRes = Localization.Current == Language.En ? Tuning.TitleLogoEnRes : Tuning.TitleLogoKoRes;
            var logoSprite = art != null ? art.SpriteOrNull(logoRes) : null;
            var logoImage = UiFactory.Icon(root, "Logo", UiFactory.Center, new Vector2(0, 190), new Vector2(1100, 560), logoSprite);
            if (logoImage != null) logo = logoImage.rectTransform;
            else
            {
                var text = UiFactory.Text(root, "Logo", UiFactory.Center, new Vector2(0, 190), new Vector2(1400, 260), 180, TextAlignmentOptions.Center,
                    UiTheme.Gold, bold: true, outline: true);
                text.text = UiText.Get("ui_title_name");
                logo = text.rectTransform;
            }

            // 3. 설정 (톱니바퀴)
            var settings = UiFactory.Button(root, "Settings", UiFactory.TopRight, new Vector2(-40, -40), new Vector2(104, 104), "", 1,
                () => actions.OpenSettings?.Invoke(), FrameStyle.ButtonDark);
            if (UiFactory.Icon((RectTransform)settings.transform, "Icon", UiFactory.Center, Vector2.zero, new Vector2(64, 64), "icon-settings") == null)
                settings.GetComponentInChildren<TMP_Text>().text = UiText.Get("ui_settings");

            // 4·5. 새 게임 / 이어하기
            UiFactory.Button(root, "NewGame", UiFactory.Center, new Vector2(0, -190), ButtonSize, UiText.Get("ui_title_new"), 42, OnNewGame);
            continueButton = UiFactory.Button(root, "Continue", UiFactory.Center, new Vector2(0, -310), ButtonSize, UiText.Get("ui_title_continue"), 42,
                () => actions.Continue?.Invoke(), FrameStyle.ButtonGreen);

            // 도감 · 미니게임: 아직 없는 기능이라 "(예정)"으로 보여 주기만 하고 누를 수 없다 (10-14 사용자 요청)
            // 글자가 버튼에 꽉 차 보여(영어) 버튼을 키우고, "(예정)"은 아랫줄에 작게, 안쪽 여백을 둔다 (10-14)
            var soonSize = new Vector2(250, 92);
            foreach (var (name, key, x) in new[] { ("Collection", "ui_title_collection", -soonSize.x / 2f - 10f), ("MiniGame", "ui_title_minigame", soonSize.x / 2f + 10f) })
            {
                var soon = UiFactory.Button(root, name, UiFactory.Center, new Vector2(x, -420), soonSize, UiText.Get(key), 26, () => { }, FrameStyle.ButtonDark);
                soon.interactable = false;
                var label = soon.GetComponentInChildren<TMP_Text>();
                label.margin = new Vector4(18, 8, 18, 8);
                label.lineSpacing = -12;
                UiFactory.Fit(label, 16, false);
            }

            // 8. 버전
            UiFactory.Text(root, "Version", UiFactory.BottomLeft, new Vector2(40, 32), new Vector2(500, 44), 26, TextAlignmentOptions.BottomLeft,
                outline: true).text = UiText.Format("ui_title_version", Application.version);

            BuildWarning(root);
        }

        void BuildVideo(RectTransform root, GameDatabase database)
        {
            var row = database != null && database.HasResource(Tuning.TitleVideoRes) ? database.Resource(Tuning.TitleVideoRes) : null;
            if (row == null) return;

            var frame = UiFactory.Stretch(root, "Video");
            videoImage = frame.gameObject.AddComponent<RawImage>();
            videoImage.raycastTarget = false;
            videoImage.enabled = false;
            // 화면 비율이 16:9가 아니어도 빈틈 없이 덮는다
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            videoTexture = new RenderTexture(1920, 1080, 0) { name = "TitleVideo" };
            videoImage.texture = videoTexture;
            video = frame.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = row.loop;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = videoTexture;
            video.source = VideoSource.Url;
            // WebGL은 VideoClip을 쓸 수 없어 StreamingAssets의 파일을 URL로 연다 (SceneBuilder가 복사)
            video.url = Application.streamingAssetsPath + "/" + row.resName;
            video.prepareCompleted += p =>
            {
                fitter.aspectRatio = p.height > 0 ? (float)p.width / p.height : 16f / 9f;
                if (IsOpen) p.Play();
            };
            video.started += _ => videoImage.enabled = true;
            video.errorReceived += (_, message) =>
            {
                Debug.LogWarning($"[DWH] 타이틀 영상을 재생하지 못해 단색 배경을 씁니다: {message}");
                videoImage.enabled = false;
            };
        }

        void BuildWarning(RectTransform root)
        {
            warning = UiFactory.Stretch(root, "Warning");
            warning.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.75f);
            var card = UiFactory.Panel(warning, "Card", UiFactory.Center, Vector2.zero, new Vector2(900, 440), FrameStyle.Cream);
            UiFactory.Text(card, "Title", UiFactory.TopCenter, new Vector2(0, -36), new Vector2(820, 70), 54, TextAlignmentOptions.Center,
                UiTheme.Hp, bold: true).text = UiText.Get("ui_title_warning");
            UiFactory.Text(card, "Body", UiFactory.TopCenter, new Vector2(0, -124), new Vector2(820, 100), 32, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true).text = UiText.Get("ui_title_reset_body");
            UiFactory.Text(card, "Sub", UiFactory.TopCenter, new Vector2(0, -226), new Vector2(820, 40), 26, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = UiText.Get("ui_title_reset_sub");
            UiFactory.Button(card, "Yes", UiFactory.BottomCenter, new Vector2(-200, 44), new Vector2(340, 96), UiText.Get("ui_title_yes"), 38,
                () => { HideWarning(); actions.NewGame?.Invoke(); }, FrameStyle.ButtonRed);
            UiFactory.Button(card, "No", UiFactory.BottomCenter, new Vector2(200, 44), new Vector2(340, 96), UiText.Get("ui_title_no"), 38,
                HideWarning, FrameStyle.ButtonDark);
            warning.gameObject.SetActive(false);
        }

        /// <summary>"새 게임" 버튼과 같다</summary>
        public void OnNewGame()
        {
            // 저장된 기록이 있으면 경고부터 띄운다 (기획서 규칙)
            if (hasProgress) warning.gameObject.SetActive(true);
            else actions.NewGame?.Invoke();
        }

        public void HideWarning() => warning.gameObject.SetActive(false);

        /// <summary>Enter·Space: 기록이 있으면 이어하기, 없으면 새 게임</summary>
        public void SubmitDefault()
        {
            if (WarningOpen) return;
            if (hasProgress) actions.Continue?.Invoke();
            else OnNewGame();
        }

        /// <param name="progress">이어할 저장 기록이 있는지</param>
        public void Show(bool progress)
        {
            hasProgress = progress;
            continueButton.interactable = progress;
            HideWarning();
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
            if (video != null)
            {
                if (video.isPrepared) video.Play();
                else video.Prepare();
            }
        }

        public void Hide()
        {
            if (video != null && video.isPlaying) video.Pause();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (logo == null) return;
            // 로고: 처음 0.5초 동안 커지며 나타나고, 그 뒤 천천히 위아래로 떠 있다
            float t = Time.unscaledTime - shownAt;
            float pop = Mathf.Clamp01(t / 0.5f);
            float scale = Mathf.LerpUnclamped(0.8f, 1f, 1f - Mathf.Pow(1f - pop, 3f) + Mathf.Sin(pop * Mathf.PI) * 0.08f);
            logo.localScale = Vector3.one * scale;
            logo.anchoredPosition = new Vector2(0, 190 + Mathf.Sin(t * 1.6f) * 10f);
        }

        void OnDestroy()
        {
            if (videoTexture != null) videoTexture.Release();
        }
    }
}
