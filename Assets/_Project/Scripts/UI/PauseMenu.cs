using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>일시정지 모달 (Esc / P, 창 포커스를 잃으면 자동). 시간 정지는 GameManager가 맡는다.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Create(Action onResume)
        {
            var root = UiFactory.Canvas("PauseMenu", 20);
            var menu = root.gameObject.AddComponent<PauseMenu>();

            var dim = UiFactory.Stretch(root, "Dim");
            dim.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.06f, 0.05f, 0.82f);
            var card = UiFactory.Panel(root, "Card", UiFactory.Center, Vector2.zero, new Vector2(620, 380), FrameStyle.Cream);
            UiFactory.Text(card, "Title", UiFactory.TopCenter, new Vector2(0, -40), new Vector2(560, 80), 60, TextAlignmentOptions.Center,
                UiTheme.InkText, bold: true).text = "일시정지";
            UiFactory.Text(card, "Hint", UiFactory.TopCenter, new Vector2(0, -126), new Vector2(560, 40), 24, TextAlignmentOptions.Center,
                UiTheme.InkDim).text = "용이 잠깐 숨을 고르는 중이에요 · Esc / P로 계속";
            UiFactory.Button(card, "Resume", UiFactory.BottomCenter, new Vector2(0, 44), new Vector2(380, 100), "계속하기", 38, onResume);

            root.gameObject.SetActive(false);
            return menu;
        }

        public bool IsOpen => gameObject.activeSelf;
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
