using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.OutGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DWH.UI
{
    /// <summary>
    /// 아웃게임 허브 (4.8, 웹 프로토타입 v0.3 참고).
    /// 상단 바: 보유 골드·도전 스테이지·최고 기록·출격 / 탭: 업그레이드 트리·환생·용 도감(준비 중), 환생 포인트
    /// 트리 탭: 왼쪽 업그레이드 트리(드래그·확대), 오른쪽 선택한 노드 상세와 구매. 환생 탭: RebirthPanel.
    /// 노드 배치는 upgrade_nodes.csv의 tree_x·tree_y를 쓴다. 노드를 누르면 선택, 선택된 노드를 다시 누르면 구매한다.
    /// 도감은 이후 이 화면에 붙는다.
    /// </summary>
    public class OutGameScreen : MonoBehaviour
    {
        const float Unit = 175f;
        const float TileSize = 140f;
        /// <summary>처음 보는 배율과 중심(격자 단위). 위쪽 생존 갈래부터 첫 스킬 줄까지 보인다. 전체는 "전체" 버튼</summary>
        const float StartZoom = 0.8f;
        static readonly Vector2 StartCenter = new(0, -1.0f);
        const float LineWidth = 10f;
        const float PunchScale = 1.18f;
        static readonly int[] Quantities = { 1, 10, UpgradeTree.MaxQuantity };
        static readonly string[] QuantityLabels = { "×1", "×10", "최대" };

        class Tile
        {
            public UpgradeNodeRow node;
            public RectTransform rect;
            public Image body, ring, glow, icon, lockIcon;
            public TMP_Text name, level;
            public float punch;
        }

        GameDatabase db;
        UpgradeTree tree;
        SaveData save;
        Func<UpgradeNodeRow, int, int> onBuy;
        OutGameActions actions;
        RectTransform treeView, detailPanel, rpPill;
        RebirthPanel rebirth;
        TMP_Text rpText;
        readonly List<(Button button, TMP_Text label)> tabs = new();
        int tab;
        readonly List<Tile> tiles = new();
        readonly List<(UpgradeNodeRow from, UpgradeNodeRow to, Image image)> lines = new();
        TreeViewport viewport;
        RectTransform selection;
        UpgradeNodeRow selected;
        int quantityIndex;

        TMP_Text gold, stageChip, stageInfo;
        TMP_Text eyebrow, title, description, now, next, perLevel, statsLeft, statsRight;
        Image detailIcon;
        Image nextBox;
        Button buyButton;
        TMP_Text buyLabel;
        readonly List<(Button button, TMP_Text label)> quantityButtons = new();

        public static OutGameScreen Create(GameDatabase database, ArtCatalog art, UpgradeTree upgradeTree, OutGameActions outGameActions)
        {
            var root = UiFactory.Canvas("OutGameScreen", 5);
            var screen = root.gameObject.AddComponent<OutGameScreen>();
            screen.Build(root, database, art, upgradeTree, outGameActions);
            root.gameObject.SetActive(false);
            return screen;
        }

        public UpgradeNodeRow Selected => selected;
        public int Quantity => Quantities[quantityIndex];

        void Build(RectTransform root, GameDatabase database, ArtCatalog art, UpgradeTree upgradeTree, OutGameActions outGameActions)
        {
            db = database;
            tree = upgradeTree;
            actions = outGameActions;
            onBuy = actions.BuyNode;
            Action onDepart = actions.Depart;

            var backdrop = UiFactory.Stretch(root, "Backdrop");
            backdrop.gameObject.AddComponent<Image>().color = UiTheme.Hex(0x2f211e);

            BuildTopBar(root, art, onDepart);
            BuildTabs(root);
            BuildTree(root);
            BuildDetail(root);
            rebirth = RebirthPanel.Create(root, db, actions);
            rebirth.Hide();
            Select(tree.Node("DragonAwakening"));
            SetTab(0);
        }

        void BuildTopBar(RectTransform root, ArtCatalog art, Action onDepart)
        {
            var bar = UiFactory.Panel(root, "TopBar", UiFactory.TopCenter, new Vector2(0, -12), new Vector2(1896, 104));
            var left = new Vector2(0, 0.5f);

            if (UiFactory.Icon(bar, "Wordmark", left, new Vector2(20, 0), new Vector2(250, 62), "title-wordmark") == null)
            {
                var logo = UiFactory.Text(bar, "Title", left, new Vector2(28, 0), new Vector2(250, 80), 44, TextAlignmentOptions.Left,
                    UiTheme.Gold, bold: true, outline: true);
                logo.text = "다녀가 용!";
            }

            var pill = UiFactory.Panel(bar, "GoldPill", left, new Vector2(290, 0), new Vector2(380, 72), FrameStyle.Ink);
            var coin = UiFactory.Rect(pill, "Coin", left, new Vector2(12, 0), new Vector2(52, 52));
            var coinImage = coin.gameObject.AddComponent<Image>();
            coinImage.sprite = UiTheme.KitSprite("coin") ?? (art != null ? art.Drop(DropResource.GoldCoin) : null);
            coinImage.color = coinImage.sprite != null ? Color.white : UiTheme.Gold;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;
            gold = UiFactory.Text(pill, "Gold", left, new Vector2(74, 0), new Vector2(296, 72), 38, TextAlignmentOptions.Left, UiTheme.Gold, bold: true);

            var chip = UiFactory.Panel(bar, "StageChip", left, new Vector2(700, 0), new Vector2(120, 64), FrameStyle.ChipGold);
            stageChip = UiFactory.Text(chip, "Id", UiFactory.Center, Vector2.zero, new Vector2(120, 64), 34, TextAlignmentOptions.Center,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true);
            stageInfo = UiFactory.Text(bar, "StageInfo", left, new Vector2(836, 0), new Vector2(620, 84), 28, TextAlignmentOptions.Left);

            UiFactory.Button(bar, "Depart", new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(260, 80), "출격!", 44, onDepart);
        }

        static readonly string[] TabLabels = { "업그레이드 트리", "환생", "용 도감" };

        void BuildTabs(RectTransform root)
        {
            for (int i = 0; i < TabLabels.Length; i++)
            {
                int index = i;
                var button = UiFactory.Button(root, $"Tab{i}", UiFactory.TopLeft, new Vector2(12 + i * 232, -124), new Vector2(220, 52), TabLabels[i], 24,
                    () => SetTab(index), FrameStyle.ButtonDark);
                tabs.Add((button, button.GetComponentInChildren<TMP_Text>()));
            }
            // 도감은 기획 미정 (4.10)
            tabs[2].button.interactable = false;
            tabs[2].label.text = "용 도감 (준비 중)";
            tabs[2].label.fontSize = 20;

            rpPill = UiFactory.Panel(root, "RebirthPoints", UiFactory.TopRight, new Vector2(-12, -124), new Vector2(300, 52), FrameStyle.Ink);
            UiFactory.Icon(rpPill, "Soul", new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(44, 44), "soul");
            rpText = UiFactory.Text(rpPill, "Text", UiFactory.Center, new Vector2(16, 0), new Vector2(250, 52), 26, TextAlignmentOptions.Center, UiTheme.Soul, bold: true);
        }

        public int Tab => tab;

        public void SetTab(int index)
        {
            tab = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                var style = i == index ? FrameStyle.ButtonGold : FrameStyle.ButtonDark;
                UiFactory.Frame(tabs[i].button.gameObject, style);
                tabs[i].label.color = UiFactory.LabelColorFor(style);
            }
            treeView.gameObject.SetActive(index == 0);
            detailPanel.gameObject.SetActive(index == 0);
            if (index == 1 && save != null) rebirth.Show(save);
            else rebirth.Hide();
        }

        void BuildTree(RectTransform root)
        {
            var view = UiFactory.Panel(root, "TreeView", UiFactory.TopLeft, new Vector2(12, -184), new Vector2(1380, 884));
            treeView = view;
            // 화면이 기준보다 넓거나 높으면 트리 창이 남는 공간을 채운다 (오른쪽 상세 패널 500 + 여백 제외)
            view.anchorMin = new Vector2(0, 0);
            view.anchorMax = new Vector2(1, 1);
            view.pivot = new Vector2(0.5f, 0.5f);
            view.offsetMin = new Vector2(12, 12);
            view.offsetMax = new Vector2(-(12 + 500 + 16), -184);
            view.gameObject.AddComponent<RectMask2D>().padding = new Vector4(8, 8, 8, 8);
            var content = UiFactory.Rect(view, "Content", UiFactory.Center, Vector2.zero, Vector2.zero);
            var lineLayer = UiFactory.Rect(content, "Lines", UiFactory.Center, Vector2.zero, Vector2.zero);
            var nodeLayer = UiFactory.Rect(content, "Nodes", UiFactory.Center, Vector2.zero, Vector2.zero);

            foreach (var node in db.upgradeNodes.rows)
                foreach (var p in node.prerequisites)
                    lines.Add((tree.Node(p), node, CreateLine(lineLayer, tree.Node(p), node)));
            foreach (var node in db.upgradeNodes.rows)
                tiles.Add(CreateTile(nodeLayer, node));

            var selectedSprite = UiTheme.KitSprite("node-selected");
            selection = UiFactory.Rect(nodeLayer, "Selection", UiFactory.Center, Vector2.zero, Vector2.one * (selectedSprite != null ? TileSize + 14 : TileSize + 30));
            if (selectedSprite != null)
            {
                var sel = selection.gameObject.AddComponent<Image>();
                sel.sprite = selectedSprite;
                sel.type = Image.Type.Sliced;
                sel.fillCenter = false;
                sel.raycastTarget = false;
            }
            else
                UiFactory.Frame(selection.gameObject, FrameStyle.Ring, UiTheme.Ember).raycastTarget = false;

            var positions = db.upgradeNodes.rows.Select(Position).ToArray();
            float half = TileSize / 2 + 20;
            var min = new Vector2(positions.Min(p => p.x) - half, positions.Min(p => p.y) - half);
            var max = new Vector2(positions.Max(p => p.x) + half, positions.Max(p => p.y) + half);
            viewport = view.gameObject.AddComponent<TreeViewport>();
            viewport.Init(view, content, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            // 창 크기는 캔버스 배치가 끝난 뒤에 정해지므로 처음 보일 때 맞춘다 (Update)

            UiFactory.Button(view, "ZoomIn", UiFactory.BottomLeft, new Vector2(18, 18), new Vector2(64, 64), "+", 40,
                () => viewport.ZoomBy(1.25f), FrameStyle.ButtonDark);
            UiFactory.Button(view, "ZoomOut", UiFactory.BottomLeft, new Vector2(90, 18), new Vector2(64, 64), "-", 44,
                () => viewport.ZoomBy(0.8f), FrameStyle.ButtonDark);
            UiFactory.Button(view, "Fit", UiFactory.BottomLeft, new Vector2(162, 18), new Vector2(120, 64), "전체", 28,
                viewport.Fit, FrameStyle.ButtonDark);

            var hint = UiFactory.Text(view, "Hint", UiFactory.BottomRight, new Vector2(-20, 18), new Vector2(900, 64), 20,
                TextAlignmentOptions.BottomRight, UiTheme.LineIdle);
            hint.text = "드래그로 이동 · 휠로 확대 · 선택한 노드를 한 번 더 누르면 구매";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            hint.text += "\n<color=#8b7a66>개발용: PageUp/PageDown 도전 스테이지 · G 골드 +1,000 · Shift+G +10만</color>";
#endif
        }

        static Vector2 Position(UpgradeNodeRow node) => new((float)node.treeX * Unit, -(float)node.treeY * Unit);

        static Image CreateLine(RectTransform layer, UpgradeNodeRow from, UpgradeNodeRow to)
        {
            Vector2 a = Position(from), b = Position(to);
            var rect = UiFactory.Rect(layer, $"{from.node}>{to.node}", UiFactory.Center, (a + b) / 2, new Vector2((b - a).magnitude, LineWidth));
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        Tile CreateTile(RectTransform layer, UpgradeNodeRow node)
        {
            var tile = new Tile { node = node };
            tile.rect = UiFactory.Rect(layer, node.node, UiFactory.Center, Position(node), Vector2.one * TileSize);

            var glow = UiFactory.Stretch(tile.rect, "Glow", -16);
            tile.glow = UiFactory.Frame(glow.gameObject, FrameStyle.Ring, UiTheme.Gold);
            tile.glow.raycastTarget = false;

            var body = UiFactory.Stretch(tile.rect, "Body");
            tile.body = UiFactory.Frame(body.gameObject, FrameStyle.Cream);
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = tile.body;
            button.onClick.AddListener(() => OnTileClicked(node));

            var ring = UiFactory.Stretch(tile.rect, "Ring", -5);
            tile.ring = UiFactory.Frame(ring.gameObject, FrameStyle.Ring);
            tile.ring.raycastTarget = false;

            // 아이콘이 있으면 타일 가운데에 아이콘, 이름은 타일 아래에 둔다 (UI 패키지 v1). 없으면 이름을 타일 안에 쓴다
            tile.icon = UiFactory.Icon(tile.rect, "Icon", UiFactory.Center, new Vector2(0, 8), new Vector2(92, 92), node.icon);
            bool hasIcon = tile.icon != null;
            tile.name = hasIcon
                ? UiFactory.Text(tile.rect, "Name", UiFactory.TopCenter, new Vector2(0, -TileSize - 26), new Vector2(TileSize + 40, 34), 22,
                    TextAlignmentOptions.Center, UiTheme.InkText, outline: true)
                : UiFactory.Text(tile.rect, "Name", UiFactory.Center, new Vector2(0, 10), new Vector2(TileSize - 14, 84), 28,
                    TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            tile.name.text = node.nameKo;
            tile.lockIcon = UiFactory.Icon(tile.rect, "Lock", UiFactory.TopRight, new Vector2(-6, -6), new Vector2(36, 36), "lock");
            if (hasIcon) tile.ring.enabled = false;

            var badge = UiFactory.Panel(tile.rect, "Badge", UiFactory.BottomCenter, new Vector2(0, -18), new Vector2(112, 40), FrameStyle.ChipCream);
            tile.level = UiFactory.Text(badge, "Level", UiFactory.Center, Vector2.zero, new Vector2(112, 40), 24,
                TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            return tile;
        }

        void BuildDetail(RectTransform root)
        {
            var panel = UiFactory.Panel(root, "Detail", UiFactory.TopRight, new Vector2(-12, -184), new Vector2(500, 884), FrameStyle.Cream);
            detailPanel = panel;
            // 세로로 늘어나도 바닥까지 채운다 (내용은 위에서부터 배치)
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = new Vector2(-512, 12);
            panel.offsetMax = new Vector2(-12, -184);
            var tl = UiFactory.TopLeft;
            const float x = 28, w = 444;

            eyebrow = UiFactory.Text(panel, "Eyebrow", tl, new Vector2(x, -26), new Vector2(w, 32), 22, TextAlignmentOptions.Left, UiTheme.InkDim, bold: true);
            title = UiFactory.Text(panel, "Title", tl, new Vector2(x, -58), new Vector2(w - 110, 62), 46, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
            detailIcon = UiFactory.Icon(panel, "Icon", UiFactory.TopRight, new Vector2(-24, -20), new Vector2(104, 104), "upgrade-core");
            description = UiFactory.Text(panel, "Description", tl, new Vector2(x, -126), new Vector2(w, 110), 25, TextAlignmentOptions.TopLeft, UiTheme.InkText);

            var nowBox = UiFactory.Panel(panel, "Now", tl, new Vector2(x, -246), new Vector2(w, 76), FrameStyle.ChipCream);
            now = UiFactory.Text(nowBox, "Text", UiFactory.Center, Vector2.zero, new Vector2(w - 32, 76), 26, TextAlignmentOptions.Left, UiTheme.InkText);
            var arrow = UiFactory.Text(panel, "Arrow", tl, new Vector2(x, -322), new Vector2(w, 30), 26, TextAlignmentOptions.Center, UiTheme.InkDim, bold: true);
            arrow.text = "↓";
            var nextRect = UiFactory.Panel(panel, "Next", tl, new Vector2(x, -352), new Vector2(w, 76), FrameStyle.ChipGold);
            nextBox = nextRect.GetComponent<Image>();
            next = UiFactory.Text(nextRect, "Text", UiFactory.Center, Vector2.zero, new Vector2(w - 32, 76), 26, TextAlignmentOptions.Left,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true);
            perLevel = UiFactory.Text(panel, "PerLevel", tl, new Vector2(x, -436), new Vector2(w, 34), 21, TextAlignmentOptions.Left, UiTheme.InkDim);

            for (int i = 0; i < Quantities.Length; i++)
            {
                int index = i;
                var button = UiFactory.Button(panel, $"Qty{i}", tl, new Vector2(x + i * 152, -482), new Vector2(140, 56), QuantityLabels[i], 26,
                    () => SetQuantity(index), FrameStyle.ButtonDark);
                quantityButtons.Add((button, button.GetComponentInChildren<TMP_Text>()));
            }

            buyButton = UiFactory.Button(panel, "Buy", tl, new Vector2(x, -552), new Vector2(w, 96), "", 34, BuySelected);
            buyLabel = buyButton.GetComponentInChildren<TMP_Text>();

            var statsTitle = UiFactory.Text(panel, "StatsTitle", tl, new Vector2(x, -662), new Vector2(w, 34), 24, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
            statsTitle.text = "현재 능력치";
            statsLeft = UiFactory.Text(panel, "StatsLeft", tl, new Vector2(x, -700), new Vector2(w / 2, 176), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            statsRight = UiFactory.Text(panel, "StatsRight", tl, new Vector2(x + w / 2, -700), new Vector2(w / 2, 176), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            SetQuantity(0);
        }

        public void Show(SaveData saveData)
        {
            save = saveData;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetQuantity(int index)
        {
            quantityIndex = index;
            for (int i = 0; i < quantityButtons.Count; i++)
            {
                var style = i == index ? FrameStyle.ButtonGold : FrameStyle.ButtonDark;
                UiFactory.Frame(quantityButtons[i].button.gameObject, style);
                quantityButtons[i].label.color = UiFactory.LabelColorFor(style);
            }
            if (save != null) RefreshDetail();
        }

        public void Select(UpgradeNodeRow node)
        {
            selected = node;
            var tile = tiles.First(t => t.node == node);
            selection.anchoredPosition = tile.rect.anchoredPosition;
            // 패키지 선택 테두리는 노드보다 조금 커서 뒤에 깔아도 보인다. 앞에 두면 레벨 배지를 가린다
            if (UiTheme.KitSprite("node-selected") != null) selection.SetAsFirstSibling();
            else selection.SetAsLastSibling();
            if (save != null) RefreshDetail();
        }

        void OnTileClicked(UpgradeNodeRow node)
        {
            // 첫 탭은 설명, 같은 노드를 다시 누르면 구매 (터치 대응)
            if (selected == node) BuySelected();
            else Select(node);
        }

        void BuySelected()
        {
            if (selected == null || onBuy(selected, Quantity) <= 0) return;
            tiles.First(t => t.node == selected).punch = 1f;
        }

        public void Refresh()
        {
            if (save == null) return;
            var current = db.Stage(save.currentStage);
            gold.text = NumberFormat.ToKorean(save.goldCoin);
            stageChip.text = current.stageId;
            string best = string.IsNullOrEmpty(save.highestStage) ? "아직 없음" : save.highestStage;
            stageInfo.text = $"<b>{current.eraName}</b> · {current.themeName}\n<size=22><color=#{UiTheme.HexString(UiTheme.LineIdle)}>최고 기록 {best}</color></size>";

            bool showPoints = actions.RebirthUnlocked() || save.rebirthPoint > 0;
            rpPill.gameObject.SetActive(showPoints);
            rpText.text = $"환생 포인트 {save.rebirthPoint:0} P";
            if (tab == 1)
            {
                if (rebirth.gameObject.activeSelf) rebirth.Refresh();
                else rebirth.Show(save);
            }

            foreach (var tile in tiles) RefreshTile(tile);
            foreach (var (from, to, image) in lines)
                image.color = tree.Level(from) >= 1 && tree.Level(to) >= 1 ? UiTheme.LineLit
                    : tree.Level(from) >= 1 ? UiTheme.LineIdle : UiTheme.Hex(0x5a4740);
            RefreshDetail();
        }

        void RefreshTile(Tile tile)
        {
            var node = tile.node;
            var status = tree.Status(node);
            int level = tree.Level(node);
            bool dark = status is NodeStatus.Locked or NodeStatus.Unavailable;

            // 패키지 노드 프레임: 잠김 / 골드 부족 / 최대 / 구매 가능
            var nodeSprite = UiTheme.KitSprite(status switch
            {
                NodeStatus.Locked or NodeStatus.Unavailable => "node-locked",
                NodeStatus.TooExpensive => "node-cant",
                NodeStatus.Maxed => "node-max",
                _ => "node-normal",
            });
            if (nodeSprite != null)
            {
                tile.body.sprite = nodeSprite;
                tile.body.type = Image.Type.Sliced;
                tile.body.color = status == NodeStatus.Unavailable ? new Color(1, 1, 1, 0.55f) : Color.white;
            }
            else
                UiFactory.Frame(tile.body.gameObject, dark ? FrameStyle.Ink : FrameStyle.Cream,
                    status == NodeStatus.Unavailable ? new Color(1, 1, 1, 0.55f) : null);
            if (tile.icon != null) tile.icon.color = dark ? new Color(0.55f, 0.5f, 0.52f, 1f) : Color.white;
            if (tile.lockIcon != null) tile.lockIcon.enabled = dark;
            tile.ring.color = status switch
            {
                NodeStatus.Affordable => UiTheme.Gold,
                NodeStatus.Maxed => UiTheme.Jade,
                NodeStatus.Locked => UiTheme.LockedPurple,
                NodeStatus.Unavailable => UiTheme.Hex(0x5a4740),
                _ => level > 0 ? UiTheme.LineLit : UiTheme.LineIdle,
            };
            tile.glow.enabled = status == NodeStatus.Affordable;
            tile.name.color = dark ? UiTheme.LineIdle : UiTheme.InkText;
            tile.level.text = status switch
            {
                NodeStatus.Unavailable => "준비 중",
                NodeStatus.Locked => "잠김",
                NodeStatus.Maxed => "MAX",
                _ => node.maxLevel > 0 ? $"{level}/{node.maxLevel}" : $"Lv {level}",
            };
        }

        void RefreshDetail()
        {
            if (selected == null) return;
            var node = selected;
            var status = tree.Status(node);
            int level = tree.Level(node);
            string max = node.maxLevel > 0 ? $" / {node.maxLevel}" : "";

            eyebrow.text = $"{Branch(node)} · Lv {level}{max}";
            title.text = node.nameKo;
            if (detailIcon != null)
            {
                var icon = UiTheme.KitSprite(node.icon);
                detailIcon.enabled = icon != null;
                detailIcon.sprite = icon;
            }
            description.text = node.description;
            now.text = $"<size=20><color=#{UiTheme.HexString(UiTheme.InkDim)}>지금</color></size>  {UpgradeText.Total(node, level)}";
            perLevel.text = UpgradeText.PerLevel(node);

            var (levels, cost) = tree.Quote(node, Quantity);
            int shownLevels = Math.Max(levels, 1);
            next.text = status == NodeStatus.Maxed
                ? "최대 레벨이에요"
                : $"<size=20>다음 +{shownLevels}</size>  {UpgradeText.Total(node, level + shownLevels)}";
            nextBox.color = status == NodeStatus.Maxed ? new Color(1, 1, 1, 0.5f) : Color.white;

            string costText = $"{NumberFormat.ToKorean(cost)} G";
            buyButton.interactable = status == NodeStatus.Affordable && levels > 0;
            buyLabel.text = status switch
            {
                NodeStatus.Unavailable => "준비 중 (시스템 구현 후)",
                NodeStatus.Locked => $"잠김: {string.Join(", ", node.prerequisites.Select(p => tree.Node(p).nameKo))} 먼저",
                NodeStatus.Maxed => "최대 레벨",
                NodeStatus.TooExpensive => $"골드 부족 · {costText}",
                _ => levels > 1 ? $"{levels}레벨 구매 · {costText}" : $"구매 · {costText}",
            };
            buyLabel.fontSize = buyLabel.text.Length > 16 ? 26 : 34;

            var s = tree.Stats();
            statsLeft.text = $"공격력 {NumberFormat.ToKorean(s.AttackPower)}\n체력 {NumberFormat.ToKorean(s.MaxHp)}\n" +
                             $"기력 {s.MaxStamina:0}\n이동 속도 {s.MoveSpeed:0.##}\n{GrowthText()}";
            statsRight.text = $"치명타 {s.CritChance * 100:0.#}%\n치명 피해 ×{s.CritMultiplier:0.##}\n골드 ×{s.GoldMultiplier:0.##}\n" +
                              $"황금화 {s.GoldenChance * 100:0.#}%\n콤보당 +{s.ChainGoldPerStack * 100:0.#}%";
        }

        /// <summary>성장 단계와 다음 단계까지 남은 누적 사용 골드 (4.1.4)</summary>
        string GrowthText()
        {
            var stage = Growth.Stage(db, save.lifetimeSpentGold);
            var next = Growth.Next(db, save.lifetimeSpentGold);
            return next == null
                ? $"성장 <b>{stage.nameKo}</b>"
                : $"성장 <b>{stage.nameKo}</b> <size=17>(다음 {NumberFormat.ToKorean(next.requiredSpentGold - save.lifetimeSpentGold)})</size>";
        }

        RectTransform celebration;
        CanvasGroup celebrationGroup;
        TMP_Text celebrationTitle, celebrationSub;
        float celebrationAge = float.MaxValue;
        const float CelebrationSeconds = 2.6f;

        /// <summary>성장 단계가 오르면 허브 가운데에 크게 알린다</summary>
        public void Celebrate(string title, string sub)
        {
            if (celebration == null)
            {
                var root = (RectTransform)transform;
                celebration = UiFactory.Panel(root, "Celebration", UiFactory.Center, new Vector2(0, 60), new Vector2(980, 220), FrameStyle.Ink);
                celebrationGroup = celebration.gameObject.AddComponent<CanvasGroup>();
                celebrationGroup.blocksRaycasts = false;
                celebrationTitle = UiFactory.Text(celebration, "Title", UiFactory.TopCenter, new Vector2(0, -24), new Vector2(940, 110), 76,
                    TextAlignmentOptions.Center, UiTheme.Gold, bold: true, outline: true);
                celebrationSub = UiFactory.Text(celebration, "Sub", UiFactory.BottomCenter, new Vector2(0, 26), new Vector2(940, 60), 32, TextAlignmentOptions.Center);
            }
            celebration.SetAsLastSibling();
            celebrationTitle.text = title;
            celebrationSub.text = sub;
            celebrationAge = 0;
        }

        static string Branch(UpgradeNodeRow node)
        {
            if (node.treeY > 1) return node.treeX < 0 ? "머리 스킬" : "몸통 스킬";
            if (node.treeY < -0.5) return "생존";
            if (node.treeX < -0.5) return "골드";
            if (node.treeX > 0.5) return "이동 · 치명";
            return "중심";
        }

        bool treeFocused;

        void Update()
        {
            if (!treeFocused && treeView.gameObject.activeInHierarchy && treeView.rect.width > 200)
            {
                treeFocused = true;
                viewport.Focus(new Vector2(StartCenter.x * Unit, -StartCenter.y * Unit), StartZoom);
            }

            if (celebration != null)
            {
                celebrationAge += Time.unscaledDeltaTime;
                float t = celebrationAge / CelebrationSeconds;
                celebrationGroup.alpha = t >= 1 ? 0 : Mathf.Clamp01(Mathf.Min(t * 8f, (1f - t) * 4f));
                celebration.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(t * 6f));
            }

            // 살 수 있는 노드는 은은히 빛나고, 산 노드는 잠깐 튄다
            float pulse = 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 3.2f);
            foreach (var tile in tiles)
            {
                if (tile.glow.enabled)
                {
                    var c = tile.glow.color;
                    c.a = pulse;
                    tile.glow.color = c;
                }
                if (tile.punch > 0)
                {
                    tile.punch = Mathf.Max(0, tile.punch - Time.unscaledDeltaTime * 4f);
                    tile.rect.localScale = Vector3.one * Mathf.Lerp(1f, PunchScale, tile.punch);
                }
            }
        }
    }
}
