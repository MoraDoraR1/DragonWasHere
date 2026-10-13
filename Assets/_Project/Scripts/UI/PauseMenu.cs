using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 일시정지 모달 (Esc / P, 창 포커스를 잃으면 자동). 시간 정지는 GameManager가 맡는다.
    /// 버튼: 계속하기 · 설정(환경설정 팝업) · 타이틀로 이동 (UI 기획서 2장 "일시정지").
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Create(Action onResume, Action onSettings, Action onTitle)
        {
            var root = UiFactory.Canvas("PauseMenu", 20);
            var menu = root.gameObject.AddComponent<PauseMenu>();

            var dim = UiFactory.Stretch(root, "Dim");
            dim.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.82f);
            var card = UiFactory.Panel(root, "Card", UiFactory.Center, Vector2.zero, new Vector2(660, 620), FrameStyle.Cream);
            UiFactory.Text(card, "Title", UiFactory.TopCenter, new Vector2(0, -40), new Vector2(560, 80), 60, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true).text = DWH.Data.UiText.Get("ui_pause");
            UiFactory.Text(card, "Hint", UiFactory.TopCenter, new Vector2(0, -126), new Vector2(600, 40), 24, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = DWH.Data.UiText.Get("ui_pause_hint");

            var size = new Vector2(420, 96);
            UiFactory.Button(card, "Resume", UiFactory.TopCenter, new Vector2(0, -196), size, DWH.Data.UiText.Get("ui_resume"), 38, onResume);
            UiFactory.Button(card, "Settings", UiFactory.TopCenter, new Vector2(0, -308), size, DWH.Data.UiText.Get("ui_settings"), 34, onSettings,
                FrameStyle.ButtonDark);
            UiFactory.Button(card, "ToTitle", UiFactory.TopCenter, new Vector2(0, -420), size, DWH.Data.UiText.Get("ui_to_title"), 34, onTitle,
                FrameStyle.ButtonRed);
            UiFactory.Text(card, "TitleHint", UiFactory.BottomCenter, new Vector2(0, 30), new Vector2(600, 36), 22, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = DWH.Data.UiText.Get("ui_to_title_hint");

            root.gameObject.SetActive(false);
            return menu;
        }

        public bool IsOpen => gameObject.activeSelf;
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
