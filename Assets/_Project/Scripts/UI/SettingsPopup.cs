using System;
using DWH.Data;
using DWH.InGame.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    public class SettingsActions
    {
        public Action ToggleLanguage;
        /// <summary>언어는 화면을 다시 만들어야 해서 타이틀·아웃게임에서만 바꾼다 (전투 중 일시정지에서는 비활성)</summary>
        public Func<bool> CanChangeLanguage;
        /// <summary>화면 흔들림 설정이 바뀌면 저장한다</summary>
        public Action<bool> SetScreenShake;
    }

    /// <summary>
    /// 환경설정 팝업 (UI 기획서 2장). 타이틀의 톱니바퀴와 일시정지의 설정 버튼이 연다.
    /// 언어(한국어/English) · 화면 모드(전체 화면/창) · 화면 흔들림(켜기/끄기) · 닫기.
    /// 환경음·효과음 슬라이더는 MVP에 사운드가 없어 넣지 않았다 (가이드 1장 제외 목록).
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        static readonly Vector2 RowSize = new(720, 96);

        SettingsActions actions;
        Button languageButton;
        TMP_Text languageValue, screenValue, shakeValue;

        public bool IsOpen => gameObject.activeSelf;

        public static SettingsPopup Create(SettingsActions settingsActions)
        {
            var root = UiFactory.Canvas("SettingsPopup", 30);
            var popup = root.gameObject.AddComponent<SettingsPopup>();
            popup.Build(root, settingsActions);
            root.gameObject.SetActive(false);
            return popup;
        }

        void Build(RectTransform root, SettingsActions settingsActions)
        {
            actions = settingsActions;
            UiFactory.Stretch(root, "Dim").gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.75f);
            var card = UiFactory.Panel(root, "Card", UiFactory.Center, Vector2.zero, new Vector2(820, 640), FrameStyle.Cream);
            UiFactory.Text(card, "Title", UiFactory.TopCenter, new Vector2(0, -34), new Vector2(760, 70), 52, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true).text = UiText.Get("ui_settings");

            languageButton = Row(card, "Language", -130, "ui_settings_language", () => actions.ToggleLanguage?.Invoke(), out languageValue);
            Row(card, "Screen", -240, "ui_settings_screen", ToggleFullScreen, out screenValue);
            Row(card, "Shake", -350, "ui_settings_shake", ToggleShake, out shakeValue);

            UiFactory.Button(card, "Close", UiFactory.BottomCenter, new Vector2(0, 40), new Vector2(360, 96), UiText.Get("ui_close"), 38, Hide);
        }

        /// <summary>한 줄: 왼쪽 항목 이름, 오른쪽 현재 값 버튼(누르면 바뀐다)</summary>
        static Button Row(RectTransform card, string name, float y, string labelKey, Action onClick, out TMP_Text value)
        {
            var row = UiFactory.Panel(card, name, UiFactory.TopCenter, new Vector2(0, y), RowSize, FrameStyle.Card);
            UiFactory.Text(row, "Label", new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(330, 80), 32, TextAlignmentOptions.Left,
                UiTheme.InkText, bold: true).text = UiText.Get(labelKey);
            var button = UiFactory.Button(row, "Value", new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(330, 74), "", 30, onClick, FrameStyle.ButtonDark);
            value = button.GetComponentInChildren<TMP_Text>();
            return button;
        }

        void ToggleFullScreen()
        {
            // 웹에서는 브라우저 전체 화면, PC에서는 전체 화면 창 ↔ 창 모드
            Screen.fullScreen = !Screen.fullScreen;
            screenValue.text = UiText.Get(!Screen.fullScreen ? "ui_settings_fullscreen" : "ui_settings_windowed");
        }

        void ToggleShake()
        {
            BattleCamera.ShakeEnabled = !BattleCamera.ShakeEnabled;
            actions.SetScreenShake?.Invoke(BattleCamera.ShakeEnabled);
            Refresh();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        void Refresh()
        {
            languageValue.text = Localization.Current == Language.En ? "English" : "한국어";
            languageButton.interactable = actions.CanChangeLanguage?.Invoke() ?? true;
            screenValue.text = UiText.Get(Screen.fullScreen ? "ui_settings_fullscreen" : "ui_settings_windowed");
            shakeValue.text = UiText.Get(BattleCamera.ShakeEnabled ? "ui_settings_on" : "ui_settings_off");
        }
    }
}
