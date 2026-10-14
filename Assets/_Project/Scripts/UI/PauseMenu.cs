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
        RectTransform confirm;
        Button endButton;
        Func<bool> canEnd;

        /// <param name="onEndSession">세션 끝내기: 이번 런을 끝내고 결과창으로 (확인을 한 번 더 받는다, 10-14)</param>
        /// <param name="canEndSession">지금 끝낼 수 있는지 (전투 중이면 스타터 중에도 된다, 10-14)</param>
        public static PauseMenu Create(Action onResume, Action onSettings, Action onTitle, Action onEndSession = null, Func<bool> canEndSession = null)
        {
            var root = UiFactory.Canvas("PauseMenu", 20);
            var menu = root.gameObject.AddComponent<PauseMenu>();
            menu.canEnd = canEndSession;

            var dim = UiFactory.Stretch(root, "Dim");
            dim.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.82f);
            var card = UiFactory.Panel(root, "Card", UiFactory.Center, Vector2.zero, new Vector2(660, 732), FrameStyle.Cream);
            UiFactory.Text(card, "Title", UiFactory.TopCenter, new Vector2(0, -40), new Vector2(560, 80), 60, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true).text = DWH.Data.UiText.Get("ui_pause");
            UiFactory.Text(card, "Hint", UiFactory.TopCenter, new Vector2(0, -126), new Vector2(600, 40), 24, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = DWH.Data.UiText.Get("ui_pause_hint");

            var size = new Vector2(420, 96);
            UiFactory.Button(card, "Resume", UiFactory.TopCenter, new Vector2(0, -196), size, DWH.Data.UiText.Get("ui_resume"), 38, onResume);
            UiFactory.Button(card, "Settings", UiFactory.TopCenter, new Vector2(0, -308), size, DWH.Data.UiText.Get("ui_settings"), 34, onSettings,
                FrameStyle.ButtonDark);
            // 세션 끝내기: 런을 잠깐 멈추고 업그레이드하러 갈 수 있게 (지금까지 모은 골드로 정산)
            menu.endButton = UiFactory.Button(card, "EndSession", UiFactory.TopCenter, new Vector2(0, -420), size, DWH.Data.UiText.Get("ui_end_session"), 34,
                () => menu.ShowConfirm(true), FrameStyle.ButtonGold);
            UiFactory.Button(card, "ToTitle", UiFactory.TopCenter, new Vector2(0, -532), size, DWH.Data.UiText.Get("ui_to_title"), 34, onTitle,
                FrameStyle.ButtonRed);
            UiFactory.Text(card, "TitleHint", UiFactory.BottomCenter, new Vector2(0, 30), new Vector2(600, 36), 22, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = DWH.Data.UiText.Get("ui_to_title_hint");

            // 확인 상자 (카드 위에 덮는다)
            menu.confirm = UiFactory.Panel(root, "Confirm", UiFactory.Center, Vector2.zero, new Vector2(720, 360), FrameStyle.Cream);
            var question = UiFactory.Text(menu.confirm, "Question", UiFactory.TopCenter, new Vector2(0, -44), new Vector2(640, 150), 32,
                TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            question.text = DWH.Data.UiText.Get("ui_end_session_confirm");
            UiFactory.Fit(question, 22, false);
            UiFactory.Button(menu.confirm, "Yes", UiFactory.BottomCenter, new Vector2(-150, 40), new Vector2(260, 88), DWH.Data.UiText.Get("ui_end_session_yes"), 32,
                () =>
                {
                    menu.ShowConfirm(false);
                    onEndSession?.Invoke();
                }, FrameStyle.ButtonRed);
            UiFactory.Button(menu.confirm, "No", UiFactory.BottomCenter, new Vector2(150, 40), new Vector2(260, 88), DWH.Data.UiText.Get("ui_end_session_no"), 32,
                () => menu.ShowConfirm(false), FrameStyle.ButtonDark);
            menu.confirm.gameObject.SetActive(false);
            if (onEndSession == null) menu.endButton.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
            return menu;
        }

        void ShowConfirm(bool show) => confirm.gameObject.SetActive(show);

        void OnEnable()
        {
            if (confirm != null) confirm.gameObject.SetActive(false);
            if (endButton != null) endButton.interactable = canEnd == null || canEnd();
        }

        public bool IsOpen => gameObject.activeSelf;
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
