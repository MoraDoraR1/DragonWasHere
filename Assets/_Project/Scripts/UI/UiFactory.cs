using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DWH.UI
{
    public enum FrameStyle { Dark, Cream, Ink, ButtonGold, ButtonDark, ButtonSoul, ChipGold, ChipCream, Ring, BarBack, BarFill }

    /// <summary>
    /// 코드로 uGUI를 만드는 도우미. UiTheme(프레임 스프라이트·폰트)이 있으면 그것으로, 없으면 단색 그레이박스로 그린다.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new(1920, 1080);
        public static readonly Vector2 TopLeft = new(0, 1);
        public static readonly Vector2 TopCenter = new(0.5f, 1);
        public static readonly Vector2 TopRight = new(1, 1);
        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Vector2 BottomLeft = new(0, 0);
        public static readonly Vector2 BottomCenter = new(0.5f, 0);
        public static readonly Vector2 BottomRight = new(1, 0);

        public static RectTransform Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Expand: 화면 비율과 관계없이 캔버스가 기준 해상도(1920×1080)보다 작아지지 않는다.
            // 16:9보다 넓으면 가로, 좁으면 세로로 공간이 늘어난다. 고정 높이 배치가 아래로 잘리지 않게 한다
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return (RectTransform)go.transform;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>부모를 꽉 채우는 RectTransform</summary>
        public static RectTransform Stretch(RectTransform parent, string name, float inset = 0)
        {
            var rect = Rect(parent, name, Center, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>
        /// 프레임 스타일 → UI 패키지 v1 스프라이트 이름 (기본 상태). 패키지가 없으면 예전 프레임(UI/*.png)을 쓴다.
        /// 패널은 모두 어두운 panel/well이고, 금색 = primary, 어두운 버튼 = secondary, 영혼색 = soul.
        /// </summary>
        static string KitName(FrameStyle style) => style switch
        {
            FrameStyle.Dark or FrameStyle.Cream => "panel-normal",
            FrameStyle.Ink or FrameStyle.ChipCream => "well-normal",
            FrameStyle.ButtonGold or FrameStyle.ChipGold => "primary-normal",
            FrameStyle.ButtonDark => "secondary-normal",
            FrameStyle.ButtonSoul => "soul-normal",
            FrameStyle.BarBack => "fill-empty",
            FrameStyle.BarFill => "fill-progress",
            _ => null,
        };

        /// <summary>버튼 상태 스프라이트 접두어 (Sprite Swap). 버튼이 아닌 스타일은 null</summary>
        static string ButtonPrefix(FrameStyle style) => style switch
        {
            FrameStyle.ButtonGold => "primary",
            FrameStyle.ButtonDark => "secondary",
            FrameStyle.ButtonSoul => "soul",
            _ => null,
        };

        public static Sprite SpriteFor(FrameStyle style)
        {
            var t = UiTheme.Active;
            if (t == null) return null;
            var kit = t.Kit(KitName(style));
            if (kit != null) return kit;
            return style switch
            {
                FrameStyle.Dark => t.panelDark,
                FrameStyle.Cream => t.panelCream,
                FrameStyle.Ink => t.panelInk,
                FrameStyle.ButtonGold => t.buttonGold,
                FrameStyle.ButtonDark => t.buttonDark,
                FrameStyle.ButtonSoul => t.buttonSoul,
                FrameStyle.ChipGold => t.chipGold,
                FrameStyle.ChipCream => t.chipCream,
                FrameStyle.Ring => t.ring,
                FrameStyle.BarBack => t.barBack,
                FrameStyle.BarFill => t.barFill,
                _ => null,
            };
        }

        /// <summary>테마가 없을 때 쓰는 단색 (패키지 색과 같은 계열)</summary>
        static Color FallbackColor(FrameStyle style) => style switch
        {
            FrameStyle.ButtonGold or FrameStyle.ChipGold => UiTheme.Gold,
            FrameStyle.ButtonSoul => UiTheme.Soul,
            FrameStyle.ButtonDark => UiTheme.Hex(0x6b4e46),
            FrameStyle.ChipCream or FrameStyle.Ink or FrameStyle.BarBack => UiTheme.Well,
            FrameStyle.Ring or FrameStyle.BarFill => Color.white,
            _ => UiTheme.Panel,
        };

        /// <summary>
        /// 프레임 이미지를 붙인다. 스프라이트가 있으면 9-slice, 없으면 단색.
        /// 버튼이면 상태별 스프라이트(보통·호버·누름·비활성)로 바꾸는 Sprite Swap을 건다.
        /// 크기가 9-slice 경계 합보다 작으면 경계를 얇게(pixelsPerUnitMultiplier) 해서 모서리가 겹치지 않게 한다.
        /// </summary>
        public static Image Frame(GameObject target, FrameStyle style, Color? tint = null)
        {
            if (!target.TryGetComponent(out Image image)) image = target.AddComponent<Image>();
            var sprite = SpriteFor(style);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = BorderFit(target.transform as RectTransform, sprite);
                image.color = tint ?? Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = tint ?? FallbackColor(style);
            }

            if (target.TryGetComponent(out Button button))
            {
                string prefix = ButtonPrefix(style);
                var t = UiTheme.Active;
                if (prefix != null && t != null && t.Kit(prefix + "-normal") != null)
                {
                    button.transition = Selectable.Transition.SpriteSwap;
                    button.spriteState = new SpriteState
                    {
                        highlightedSprite = t.Kit(prefix + "-hover"),
                        selectedSprite = t.Kit(prefix + "-normal"),
                        pressedSprite = t.Kit(prefix + "-pressed"),
                        disabledSprite = t.Kit(prefix + "-disabled"),
                    };
                }
                else
                    button.transition = Selectable.Transition.ColorTint;
            }
            return image;
        }

        /// <summary>9-slice 경계가 영역보다 크면 그만큼 얇게 그린다 (1 이상)</summary>
        static float BorderFit(RectTransform rect, Sprite sprite)
        {
            if (rect == null) return 1f;
            var size = rect.rect.size;
            if (size.x <= 1 || size.y <= 1) size = rect.sizeDelta;
            var b = sprite.border;
            float k = 1f;
            if (size.x > 1 && b.x + b.z > 0) k = Mathf.Max(k, (b.x + b.z) / (size.x * 0.9f));
            if (size.y > 1 && b.y + b.w > 0) k = Mathf.Max(k, (b.y + b.w) / (size.y * 0.9f));
            return k;
        }

        /// <summary>패키지 아이콘 이미지 (Simple + 비율 유지). 아이콘이 없으면 만들지 않는다</summary>
        public static Image Icon(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string sprite)
        {
            var kit = UiTheme.KitSprite(sprite);
            if (kit == null) return null;
            var rect = Rect(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = kit;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            FrameStyle style = FrameStyle.Dark, Color? tint = null)
        {
            var rect = Rect(parent, name, anchor, position, size);
            Frame(rect.gameObject, style, tint);
            return rect;
        }

        public static TMP_Text Text(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color? color = null, bool bold = false, bool outline = false)
        {
            var rect = Rect(parent, name, anchor, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = KoreanFont.Get();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? UiTheme.TextLight;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (bold) text.fontStyle = FontStyles.Bold;
            if (outline)
            {
                text.outlineWidth = 0.25f;
                text.outlineColor = UiTheme.Ink;
            }
            return text;
        }

        /// <summary>프레임 위 글자색: 금색 버튼·칩은 먹색, 나머지(어두운 패널·영혼색 버튼)는 밝은 색</summary>
        public static Color LabelColorFor(FrameStyle style) => style switch
        {
            FrameStyle.ButtonGold or FrameStyle.ChipGold => UiTheme.Ink,
            _ => UiTheme.TextLight,
        };

        public static Button Button(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            string label, float fontSize, Action onClick, FrameStyle style = FrameStyle.ButtonGold)
        {
            var rect = Rect(parent, name, anchor, position, size);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
            button.colors = colors;
            button.targetGraphic = Frame(rect.gameObject, style);
            button.onClick.AddListener(() => onClick());
            var text = Text(rect, "Label", Center, Vector2.zero, size, fontSize, TextAlignmentOptions.Center, LabelColorFor(style), bold: true);
            text.text = label;
            // 비활성 버튼은 회색 프레임 위에 밝은 글자 (패키지 권장)
            rect.gameObject.AddComponent<ButtonLabelTint>().Init(button, text);
            return button;
        }

        /// <summary>바 채움 색 → 패키지 채움 스프라이트 (체력·기력·진행·보너스·영혼)</summary>
        static string FillSpriteFor(Color c)
        {
            static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.08f;
            if (Near(c, UiTheme.Hp)) return "fill-hp";
            if (Near(c, UiTheme.Energy)) return "fill-energy";
            if (Near(c, UiTheme.Soul)) return "fill-soul";
            if (Near(c, UiTheme.Jade)) return "fill-progress";
            return null;
        }

        /// <summary>게이지. 반환값은 채움 RectTransform (anchorMax.x로 비율 조절). 패키지 채움 스프라이트가 있으면 그 색 그대로 쓴다</summary>
        public static RectTransform Bar(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color fill)
        {
            var back = Panel(parent, name, anchor, position, size, FrameStyle.BarBack);
            var fillRect = Stretch(back, "Fill", 3);
            var kitFill = UiTheme.KitSprite(FillSpriteFor(fill));
            if (kitFill != null)
            {
                var image = fillRect.gameObject.AddComponent<Image>();
                image.sprite = kitFill;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = BorderFit(fillRect, kitFill);
                image.raycastTarget = false;
            }
            else
                Frame(fillRect.gameObject, FrameStyle.BarFill, fill);
            fillRect.anchorMax = new Vector2(1, 1);
            return fillRect;
        }

        public static void SetFill(RectTransform fill, double ratio)
        {
            float r = Mathf.Clamp01((float)ratio);
            fill.anchorMax = new Vector2(r, 1);
            fill.offsetMax = new Vector2(r <= 0 ? 0 : -3, -3);
            // 0%에서는 둥근 끝 조각이 남지 않게 숨긴다 (패키지 권장)
            if (fill.TryGetComponent(out Image image)) image.enabled = r > 0.005f;
        }
    }

    /// <summary>버튼이 비활성이면 글자를 밝은 색으로, 다시 활성이면 원래 색으로 되돌린다</summary>
    public class ButtonLabelTint : MonoBehaviour
    {
        Button button;
        TMP_Text label;
        Color active;
        bool? lastInteractable;

        public void Init(Button target, TMP_Text text)
        {
            button = target;
            label = text;
            active = text.color;
        }

        /// <summary>프레임 스타일이 바뀌어 활성 글자색이 달라졌을 때 알려 준다</summary>
        public void SetActiveColor(Color color)
        {
            active = color;
            lastInteractable = null;
        }

        void LateUpdate()
        {
            if (button == null || label == null) return;
            bool on = button.interactable;
            if (lastInteractable == on) return;
            lastInteractable = on;
            label.color = on ? active : UiTheme.InkText;
        }
    }

    /// <summary>마우스가 올라오고 나갈 때 알린다</summary>
    public class UiHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Entered;
        public Action Exited;
        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
    }
}
