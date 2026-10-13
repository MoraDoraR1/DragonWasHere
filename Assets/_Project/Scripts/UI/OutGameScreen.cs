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
    /// 아웃게임 허브 (가이드 6.7, 웹 프로토타입 v0.3 참고).
    /// 상단 바: 보유 골드·도전 스테이지·최고 스테이지·언어·출격 / 탭: 업그레이드 트리·환생, 환생 포인트.
    /// 트리 탭: 왼쪽 업그레이드 트리 28노드(드래그·확대, 배치는 TreeLayout), 오른쪽 선택한 노드 상세와 구매. 환생 탭: RebirthPanel.
    /// 노드를 누르면 선택, 선택된 노드를 다시 누르면 구매한다. 노드 상태(기본·구매 가능·잠김·최대)는 UI 에셋 03_트리노드와탭을 쓴다.
    /// </summary>
    public class OutGameScreen : MonoBehaviour
    {
        const float Unit = 175f;
        const float TileSize = 128f;
        const float LineWidth = 10f;
        /// <summary>레벨 배지가 노드 아래로 튀어나온 만큼 (배지 아래 끝 -16 + 여유). 테두리 아래 변을 이만큼 늘린다</summary>
        const float BadgeDrop = 22f;
        const float PunchScale = 1.18f;
        static readonly int[] Quantities = { 1, 10, UpgradeTree.MaxQuantity };
        static readonly string[] QuantityKeys = { null, "ui_buy_x10", "ui_buy_max" };

        class Tile
        {
            public UpgradeRow node;
            public RectTransform rect;
            public Image body, glow, icon, lockIcon;
            public RectTransform namePlate;
            public TMP_Text name, level;
            public float punch;
        }

        GameDatabase db;
        ArtCatalog art;
        UpgradeTree tree;
        SaveData save;
        OutGameActions actions;
        Dictionary<string, Vector2> layout;
        RectTransform treeView, detailPanel, rpPill;
        RebirthPanel rebirth;
        TMP_Text rpText, departLabel, languageLabel, hint;
        readonly List<(Button button, TMP_Text label)> tabs = new();
        int tab;
        readonly List<Tile> tiles = new();
        readonly List<(UpgradeRow from, UpgradeRow to, Image image)> lines = new();
        TreeViewport viewport;
        RectTransform selection;
        UpgradeRow selected;
        int quantityIndex;

        TMP_Text gold, stageChip, stageInfo;
        TMP_Text eyebrow, title, description, now, next, statsLeft, statsRight, statsTitle;
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

        public UpgradeRow Selected => selected;
        public int Quantity => Quantities[quantityIndex];

        void Build(RectTransform root, GameDatabase database, ArtCatalog catalog, UpgradeTree upgradeTree, OutGameActions outGameActions)
        {
            db = database;
            art = catalog;
            tree = upgradeTree;
            actions = outGameActions;
            layout = TreeLayout.Positions(db);

            var backdrop = UiFactory.Stretch(root, "Backdrop");
            backdrop.gameObject.AddComponent<Image>().color = UiTheme.Hex(0x2f211e);

            BuildTopBar(root);
            BuildTabs(root);
            BuildTree(root);
            BuildDetail(root);
            rebirth = RebirthPanel.Create(root, db, art, actions);
            rebirth.Hide();
            Select(db.upgrades[0]);
            SetTab(0);
        }

        Sprite Icon(string resId) => art != null ? art.SpriteOrNull(resId) : null;

        void BuildTopBar(RectTransform root)
        {
            var bar = UiFactory.Panel(root, "TopBar", UiFactory.TopCenter, new Vector2(0, -12), new Vector2(1896, 104));
            var left = new Vector2(0, 0.5f);

            var logo = Localization.Current == Language.En ? "logo-en" : "logo-ko";
            if (UiFactory.Icon(bar, "Logo", left, new Vector2(16, 0), new Vector2(250, 76), logo) == null)
                UiFactory.Text(bar, "Title", left, new Vector2(28, 0), new Vector2(250, 80), 44, TextAlignmentOptions.Left, UiTheme.Gold, bold: true, outline: true).text = "다녀가 용!";

            var pill = UiFactory.Panel(bar, "GoldPill", left, new Vector2(290, 0), new Vector2(380, 72), FrameStyle.Ink);
            UiFactory.Icon(pill, "Coin", left, new Vector2(12, 0), new Vector2(52, 52), "icon-gold");
            gold = UiFactory.Text(pill, "Gold", left, new Vector2(74, 0), new Vector2(296, 72), 38, TextAlignmentOptions.Left, UiTheme.Gold, bold: true);

            var chip = UiFactory.Panel(bar, "StageChip", left, new Vector2(700, 0), new Vector2(120, 64), FrameStyle.ChipGold);
            stageChip = UiFactory.Text(chip, "Id", UiFactory.Center, Vector2.zero, new Vector2(120, 64), 34, TextAlignmentOptions.Center,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true);
            stageInfo = UiFactory.Text(bar, "StageInfo", left, new Vector2(836, 0), new Vector2(560, 84), 26, TextAlignmentOptions.Left);
            UiFactory.Fit(stageInfo, 18, false);

            var language = UiFactory.Button(bar, "Language", new Vector2(1, 0.5f), new Vector2(-292, 0), new Vector2(150, 64), "", 22,
                () => actions.ToggleLanguage?.Invoke(), FrameStyle.ButtonDark);
            languageLabel = language.GetComponentInChildren<TMP_Text>();
            var depart = UiFactory.Button(bar, "Depart", new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(260, 80), "", 44, () => actions.Depart(), FrameStyle.ButtonGreen);
            departLabel = depart.GetComponentInChildren<TMP_Text>();
        }

        static readonly string[] TabKeys = { "ui_tab_upgrade", "ui_tab_rebirth" };

        void BuildTabs(RectTransform root)
        {
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int index = i;
                var button = UiFactory.Button(root, $"Tab{i}", UiFactory.TopLeft, new Vector2(12 + i * 232, -124), new Vector2(220, 52), "", 24,
                    () => SetTab(index), FrameStyle.ButtonDark);
                tabs.Add((button, button.GetComponentInChildren<TMP_Text>()));
            }

            rpPill = UiFactory.Panel(root, "RebirthPoints", UiFactory.TopRight, new Vector2(-12, -124), new Vector2(340, 52), FrameStyle.Ink);
            UiFactory.Icon(rpPill, "Icon", new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(44, 44), "icon-rebirth");
            rpText = UiFactory.Text(rpPill, "Text", UiFactory.Center, new Vector2(16, 0), new Vector2(290, 52), 24, TextAlignmentOptions.Center, UiTheme.Soul, bold: true);
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
                if (tabs[i].button.TryGetComponent(out ButtonLabelTint tint)) tint.SetActiveColor(tabs[i].label.color);
            }
            treeView.gameObject.SetActive(index == 0);
            detailPanel.gameObject.SetActive(index == 0);
            if (index == 1 && save != null) rebirth.Show(save);
            else rebirth.Hide();
        }

        Vector2 Position(UpgradeRow node) => layout[node.upgradeId] * Unit;

        void BuildTree(RectTransform root)
        {
            var view = UiFactory.Panel(root, "TreeView", UiFactory.TopLeft, new Vector2(12, -184), new Vector2(1380, 884));
            treeView = view;
            view.anchorMin = new Vector2(0, 0);
            view.anchorMax = new Vector2(1, 1);
            view.pivot = new Vector2(0.5f, 0.5f);
            view.offsetMin = new Vector2(12, 12);
            view.offsetMax = new Vector2(-(12 + 500 + 16), -184);
            view.gameObject.AddComponent<RectMask2D>().padding = new Vector4(8, 8, 8, 8);
            var content = UiFactory.Rect(view, "Content", UiFactory.Center, Vector2.zero, Vector2.zero);
            var lineLayer = UiFactory.Rect(content, "Lines", UiFactory.Center, Vector2.zero, Vector2.zero);
            var nodeLayer = UiFactory.Rect(content, "Nodes", UiFactory.Center, Vector2.zero, Vector2.zero);

            foreach (var node in db.upgrades)
                foreach (var p in node.Prerequisites())
                    lines.Add((db.Upgrade(p), node, CreateLine(lineLayer, db.Upgrade(p), node)));
            foreach (var node in db.upgrades)
                tiles.Add(CreateTile(nodeLayer, node));

            // 선택 테두리: 아래 변만 늘려 노드 아래로 튀어나온 레벨 배지(0/12, [잠김])까지 감싼다
            selection = UiFactory.Rect(nodeLayer, "Selection", UiFactory.Center, Vector2.zero, new Vector2(TileSize + 18, TileSize + 18 + BadgeDrop));
            var sel = selection.gameObject.AddComponent<Image>();
            sel.sprite = UiTheme.KitSprite("node-selected-border");
            sel.color = sel.sprite != null ? Color.white : UiTheme.Ember;
            sel.type = Image.Type.Sliced;
            sel.fillCenter = false;
            sel.raycastTarget = false;

            var positions = db.upgrades.Select(Position).ToArray();
            float half = TileSize / 2 + 40;
            var min = new Vector2(positions.Min(p => p.x) - half, positions.Min(p => p.y) - half);
            var max = new Vector2(positions.Max(p => p.x) + half, positions.Max(p => p.y) + half);
            viewport = view.gameObject.AddComponent<TreeViewport>();
            viewport.Init(view, content, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            // 창 크기는 캔버스 배치가 끝난 뒤에 정해지므로 처음 보일 때 맞춘다 (Update)

            UiFactory.Button(view, "ZoomIn", UiFactory.BottomLeft, new Vector2(18, 18), new Vector2(64, 64), "+", 40,
                () => viewport.ZoomBy(1.25f), FrameStyle.ButtonDark);
            UiFactory.Button(view, "ZoomOut", UiFactory.BottomLeft, new Vector2(90, 18), new Vector2(64, 64), "-", 44,
                () => viewport.ZoomBy(0.8f), FrameStyle.ButtonDark);
            UiFactory.Button(view, "Fit", UiFactory.BottomLeft, new Vector2(162, 18), new Vector2(64, 64), "□", 30,
                viewport.Fit, FrameStyle.ButtonDark);

            hint = UiFactory.Text(view, "Hint", UiFactory.BottomRight, new Vector2(-20, 18), new Vector2(900, 64), 20,
                TextAlignmentOptions.BottomRight, UiTheme.InkDim);
        }

        static Image CreateLine(RectTransform layer, Vector2 a, Vector2 b, string name)
        {
            var rect = UiFactory.Rect(layer, name, UiFactory.Center, (a + b) / 2, new Vector2((b - a).magnitude, LineWidth));
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        Image CreateLine(RectTransform layer, UpgradeRow from, UpgradeRow to) =>
            CreateLine(layer, Position(from), Position(to), $"{from.upgradeId}>{to.upgradeId}");

        Tile CreateTile(RectTransform layer, UpgradeRow node)
        {
            var tile = new Tile { node = node };
            tile.rect = UiFactory.Rect(layer, node.upgradeId, UiFactory.Center, Position(node), Vector2.one * TileSize);

            var glow = UiFactory.Stretch(tile.rect, "Glow", -12);
            glow.offsetMin = new Vector2(glow.offsetMin.x, -12 - BadgeDrop);
            tile.glow = glow.gameObject.AddComponent<Image>();
            tile.glow.sprite = UiTheme.KitSprite("node-selected-border");
            tile.glow.type = Image.Type.Sliced;
            tile.glow.color = UiTheme.Gold;
            tile.glow.raycastTarget = false;

            var body = UiFactory.Stretch(tile.rect, "Body");
            tile.body = body.gameObject.AddComponent<Image>();
            tile.body.type = Image.Type.Sliced;
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = tile.body;
            button.onClick.AddListener(() => OnTileClicked(node));

            tile.icon = UiFactory.Icon(tile.rect, "Icon", UiFactory.Center, new Vector2(0, 6), new Vector2(92, 92), Icon(node.resId));
            // 이름은 레벨 배지 아래. 길면(영어) 두 줄까지 줄여서 맞춘다 (DWH-53)
            // 이름표 바탕: 연결선은 노드 뒤 레이어라 이름표에 가려진다. 이름이 선에 씹히지 않게 한다 (크기는 이름 길이에 맞춘다, Refresh)
            tile.namePlate = UiFactory.Panel(tile.rect, "NamePlate", UiFactory.TopCenter, new Vector2(0, -TileSize - BadgeDrop - 10), new Vector2(TileSize + 40, 44),
                FrameStyle.ChipCream, new Color(1f, 1f, 1f, 0.95f));
            tile.namePlate.GetComponent<Image>().raycastTarget = false;
            tile.name = UiFactory.Text(tile.rect, "Name", UiFactory.TopCenter, new Vector2(0, -TileSize - BadgeDrop - 12), new Vector2(TileSize + 40, 44), 21,
                TextAlignmentOptions.Top, UiTheme.InkText, bold: true);
            UiFactory.Fit(tile.name, 14, false);
            tile.lockIcon = UiFactory.Icon(tile.rect, "Lock", UiFactory.TopRight, new Vector2(-4, -4), new Vector2(36, 36), "icon-lock");

            var badge = UiFactory.Panel(tile.rect, "Badge", UiFactory.BottomCenter, new Vector2(0, -16), new Vector2(104, 36), FrameStyle.ChipCream);
            tile.level = UiFactory.Text(badge, "Level", UiFactory.Center, Vector2.zero, new Vector2(104, 36), 21,
                TextAlignmentOptions.Center, UiTheme.InkText, bold: true);
            return tile;
        }

        void BuildDetail(RectTransform root)
        {
            var panel = UiFactory.Panel(root, "Detail", UiFactory.TopRight, new Vector2(-12, -184), new Vector2(500, 884), FrameStyle.Cream);
            detailPanel = panel;
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = new Vector2(-512, 12);
            panel.offsetMax = new Vector2(-12, -184);
            var tl = UiFactory.TopLeft;
            const float x = 28, w = 444;

            eyebrow = UiFactory.Text(panel, "Eyebrow", tl, new Vector2(x, -26), new Vector2(w, 32), 22, TextAlignmentOptions.Left, UiTheme.InkDim, bold: true);
            title = UiFactory.Text(panel, "Title", tl, new Vector2(x, -58), new Vector2(w - 110, 62), 40, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
            UiFactory.Fit(title, 24, true);
            var iconRect = UiFactory.Rect(panel, "Icon", UiFactory.TopRight, new Vector2(-24, -20), new Vector2(104, 104));
            detailIcon = iconRect.gameObject.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            description = UiFactory.Text(panel, "Description", tl, new Vector2(x, -130), new Vector2(w, 100), 23, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            UiFactory.Fit(description, 16, false);

            var nowBox = UiFactory.Panel(panel, "Now", tl, new Vector2(x, -238), new Vector2(w, 90), FrameStyle.Card);
            now = UiFactory.Text(nowBox, "Text", UiFactory.Center, Vector2.zero, new Vector2(w - 32, 90), 23, TextAlignmentOptions.Left, UiTheme.InkText);
            var arrow = UiFactory.Text(panel, "Arrow", tl, new Vector2(x, -330), new Vector2(w, 30), 26, TextAlignmentOptions.Center, UiTheme.InkDim, bold: true);
            arrow.text = "↓";
            var nextRect = UiFactory.Panel(panel, "Next", tl, new Vector2(x, -360), new Vector2(w, 90), FrameStyle.ChipGold);
            nextBox = nextRect.GetComponent<Image>();
            next = UiFactory.Text(nextRect, "Text", UiFactory.Center, Vector2.zero, new Vector2(w - 32, 90), 23, TextAlignmentOptions.Left,
                UiFactory.LabelColorFor(FrameStyle.ChipGold), bold: true);

            for (int i = 0; i < Quantities.Length; i++)
            {
                int index = i;
                var button = UiFactory.Button(panel, $"Qty{i}", tl, new Vector2(x + i * 152, -470), new Vector2(140, 56), "", 26,
                    () => SetQuantity(index), FrameStyle.ButtonDark);
                quantityButtons.Add((button, button.GetComponentInChildren<TMP_Text>()));
            }

            buyButton = UiFactory.Button(panel, "Buy", tl, new Vector2(x, -540), new Vector2(w, 96), "", 32, BuySelected);
            buyLabel = buyButton.GetComponentInChildren<TMP_Text>();

            statsTitle = UiFactory.Text(panel, "StatsTitle", tl, new Vector2(x, -650), new Vector2(w, 34), 24, TextAlignmentOptions.Left, UiTheme.InkText, bold: true);
            statsLeft = UiFactory.Text(panel, "StatsLeft", tl, new Vector2(x, -688), new Vector2(w / 2, 186), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
            statsRight = UiFactory.Text(panel, "StatsRight", tl, new Vector2(x + w / 2, -688), new Vector2(w / 2, 186), 20, TextAlignmentOptions.TopLeft, UiTheme.InkText);
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
                if (quantityButtons[i].button.TryGetComponent(out ButtonLabelTint tint)) tint.SetActiveColor(quantityButtons[i].label.color);
            }
            if (save != null) RefreshDetail();
        }

        public void Select(UpgradeRow node)
        {
            selected = node;
            var tile = tiles.First(t => t.node == node);
            selection.anchoredPosition = tile.rect.anchoredPosition + new Vector2(0, -BadgeDrop / 2f);
            selection.SetAsLastSibling();
            if (save != null) RefreshDetail();
        }

        void OnTileClicked(UpgradeRow node)
        {
            // 첫 탭은 설명, 같은 노드를 다시 누르면 구매 (터치 대응)
            if (selected == node) BuySelected();
            else Select(node);
        }

        void BuySelected()
        {
            if (selected == null || actions.BuyNode(selected, Quantity) <= 0) return;
            tiles.First(t => t.node == selected).punch = 1f;
        }

        /// <summary>언어가 바뀌면 고정 문구까지 다시 쓴다</summary>
        public void Refresh()
        {
            if (save == null) return;
            var current = db.Stage(save.currentStage);
            gold.text = NumberFormat.ToKorean(save.goldCoin);
            stageChip.text = db.StageLabel(current);
            stageInfo.text = $"<b>{db.Text(current.nameStr)}</b> · {db.Text(db.Era(current).nameStr)} | {db.Text(db.Theme(current).nameStr)}\n" +
                             $"<size=21><color=#{UiTheme.HexString(UiTheme.InkDim)}>{UiText.Get("ui_result_best")} {db.StageLabel(save.highestStage)}</color></size>";
            departLabel.text = UiText.Get("ui_depart");
            languageLabel.text = Localization.Current == Language.Ko ? "English" : "한국어";
            for (int i = 0; i < tabs.Count; i++) tabs[i].label.text = UiText.Get(TabKeys[i]);
            for (int i = 0; i < quantityButtons.Count; i++) quantityButtons[i].label.text = QuantityKeys[i] == null ? "×1" : UiText.Get(QuantityKeys[i]);
            statsTitle.text = UiText.Get("ui_stats");
            hint.text = UiText.Get("ui_tree_hint");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            hint.text += "\n<color=#8b7a66>DEV: PageUp/PageDown 스테이지 · G 골드 +1,000 · Shift+G +10만 · Ctrl+G +1억</color>";
#endif

            bool showPoints = actions.RebirthUnlocked() || save.rebirthPoint > 0 || save.rebirthCount > 0;
            rpPill.gameObject.SetActive(showPoints);
            rpText.text = $"{UiText.Get("ui_rebirth_point")} {save.rebirthPoint:0} P";
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
            bool locked = status == NodeStatus.Locked;
            bool core = node.Prerequisites().Length == 0;

            string prefix = core ? "node-core" : node.category == UpgradeCategory.PassiveSkill ? "node-soul" : "node";
            var sprite = UiTheme.KitSprite(locked ? $"{prefix}-disabled" : status == NodeStatus.Maxed ? $"{prefix}-pressed" : $"{prefix}-normal");
            tile.body.sprite = sprite;
            tile.body.color = sprite != null ? Color.white : locked ? UiTheme.Well : UiTheme.Cream;
            if (tile.icon != null) tile.icon.color = locked ? new Color(0.5f, 0.45f, 0.47f, 1f) : status == NodeStatus.TooExpensive && level == 0 ? new Color(0.8f, 0.78f, 0.78f) : Color.white;
            if (tile.lockIcon != null) tile.lockIcon.enabled = locked;
            tile.glow.enabled = status == NodeStatus.Affordable;
            tile.name.text = db.Text(node.nameStr);
            FitNamePlate(tile);
            tile.name.color = locked ? UiTheme.InkDim : UiTheme.InkText;
            tile.level.text = status switch
            {
                NodeStatus.Locked => UiText.Get("ui_locked"),
                NodeStatus.Maxed => UiText.Get("ui_max"),
                _ => node.maxLevel > 0 ? $"{level}/{node.maxLevel}" : $"Lv {level}",
            };
        }

        /// <summary>이름표 바탕을 실제 글자 크기(+여백)에 맞춘다</summary>
        static void FitNamePlate(Tile tile)
        {
            tile.name.ForceMeshUpdate();
            var size = tile.name.textBounds.size;
            if (size.x <= 0 || float.IsNaN(size.x)) return;
            var max = tile.name.rectTransform.sizeDelta;
            tile.namePlate.sizeDelta = new Vector2(Mathf.Min(max.x + 8, size.x + 18), Mathf.Min(max.y + 4, size.y + 8));
        }

        void RefreshDetail()
        {
            if (selected == null) return;
            var node = selected;
            var status = tree.Status(node);
            int level = tree.Level(node);
            string max = node.maxLevel > 0 ? $" / {node.maxLevel}" : "";

            eyebrow.text = $"{UiText.Get($"ui_branch_{(int)node.category}")} · Lv {level}{max}";
            title.text = db.Text(node.nameStr);
            var icon = Icon(node.resId);
            detailIcon.enabled = icon != null;
            detailIcon.sprite = icon;
            description.text = db.Text(node.descStr);
            now.text = $"<size=19><color=#{UiTheme.HexString(UiTheme.InkDim)}>{UiText.Get("ui_now")}</color></size>  {UpgradeText.Total(db, node, level) ?? "-"}";

            var (levels, cost) = tree.Quote(node, Quantity);
            int shownLevels = Math.Max(levels, 1);
            next.text = status == NodeStatus.Maxed
                ? UiText.Get("ui_max")
                : $"<size=19>{UiText.Get("ui_next")} +{shownLevels}</size>  {UpgradeText.Total(db, node, level + shownLevels)}";
            nextBox.color = status == NodeStatus.Maxed ? new Color(1, 1, 1, 0.5f) : Color.white;

            string costText = $"{NumberFormat.ToKorean(cost)} G";
            buyButton.interactable = status == NodeStatus.Affordable && levels > 0;
            buyLabel.text = status switch
            {
                NodeStatus.Locked => UiText.Get("ui_need_prereq"),
                NodeStatus.Maxed => UiText.Get("ui_max"),
                _ => levels > 1 ? $"{UiText.Get("ui_buy")} +{levels} · {costText}" : $"{UiText.Get("ui_buy")} · {costText}",
            };
            buyLabel.fontSize = buyLabel.text.Length > 18 ? 24 : 32;

            var s = tree.Stats();
            statsLeft.text = $"{UiText.Get("ui_stat_attack")} {NumberFormat.ToKorean(s.AttackPower)}\n{UiText.Get("ui_stat_hp")} {NumberFormat.ToKorean(s.MaxHp)}\n" +
                             $"{UiText.Get("ui_stat_stamina")} {s.MaxStamina:0}\n{UiText.Get("ui_stat_speed")} {s.MoveSpeed * 100:0}\n{GrowthText()}";
            statsRight.text = $"{UiText.Get("ui_stat_crit")} {s.CritChance * 100:0.#}%\n{UiText.Get("ui_stat_crit_damage")} ×{s.CritMultiplier:0.##}\n" +
                              $"{UiText.Get("ui_gold")} ×{s.GoldMultiplier:0.##}\n{UiText.Get("ui_stat_golden")} {s.GoldenChance * 100:0.#}%";
        }

        /// <summary>성장 단계와 다음 단계까지 남은 골드 (이번 회차 업그레이드 누적 사용 골드)</summary>
        string GrowthText()
        {
            var stage = Growth.Stage(db, save.runSpentGold);
            var nextStage = Growth.Next(db, save.runSpentGold);
            string name = db.Text(stage.nameStr);
            return nextStage == null
                ? $"<b>{name}</b> <size=17>({UiText.Get("ui_growth_max")})</size>"
                : $"<b>{name}</b> <size=17>({UiText.Format("ui_growth_next", NumberFormat.ToKorean(nextStage.reqGold - save.runSpentGold))})</size>";
        }

        RectTransform celebration;
        CanvasGroup celebrationGroup;
        TMP_Text celebrationTitle, celebrationSub;
        float celebrationAge = float.MaxValue;
        const float CelebrationSeconds = 2.6f;

        /// <summary>성장 단계가 오르면 허브 가운데에 크게 알린다</summary>
        public void Celebrate(string text, string sub, Sprite picture = null)
        {
            if (celebration == null)
            {
                var root = (RectTransform)transform;
                celebration = UiFactory.Panel(root, "Celebration", UiFactory.Center, new Vector2(0, 60), new Vector2(980, 420), FrameStyle.Cream);
                celebrationGroup = celebration.gameObject.AddComponent<CanvasGroup>();
                celebrationGroup.blocksRaycasts = false;
                var pic = UiFactory.Rect(celebration, "Picture", UiFactory.TopCenter, new Vector2(0, -20), new Vector2(360, 220)).gameObject.AddComponent<Image>();
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                celebrationTitle = UiFactory.Text(celebration, "Title", UiFactory.BottomCenter, new Vector2(0, 80), new Vector2(940, 100), 64,
                    TextAlignmentOptions.Center, UiTheme.GoldDeep, bold: true);
                celebrationSub = UiFactory.Text(celebration, "Sub", UiFactory.BottomCenter, new Vector2(0, 26), new Vector2(940, 56), 28, TextAlignmentOptions.Center);
            }
            var image = celebration.Find("Picture").GetComponent<Image>();
            image.sprite = picture;
            image.enabled = picture != null;
            celebration.SetAsLastSibling();
            celebrationTitle.text = text;
            celebrationSub.text = sub;
            celebrationAge = 0;
        }

        bool treeFocused;

        void Update()
        {
            if (!treeFocused && treeView.gameObject.activeInHierarchy && treeView.rect.width > 200)
            {
                treeFocused = true;
                viewport.Fit();
            }

            if (celebration != null)
            {
                celebrationAge += Time.unscaledDeltaTime;
                float t = celebrationAge / CelebrationSeconds;
                celebrationGroup.alpha = t >= 1 ? 0 : Mathf.Clamp01(Mathf.Min(t * 8f, (1f - t) * 4f));
                celebration.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(t * 6f));
            }

            // 살 수 있는 노드는 은은히 빛나고, 산 노드는 잠깐 튄다
            float pulse = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.2f);
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
