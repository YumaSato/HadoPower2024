using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 既存の左右表示板を置き換える、画面右端常駐の戦況HUDです。
/// </summary>
public class CanvasCtrl : MonoBehaviour
{
    // 常時表示する情報を残したまま、盤面を圧迫しない横幅にする。
    private const float HudWidth = 420.0f;
    private const float HudHeight = 840.0f;
    private const float CollapsedHudHeight = 450.0f;
    private const int FragmentPerPearl = 20;

    public WorkerMgr wMgr;

    private RectTransform hudRoot;
    private RectTransform cardsRoot;
    // HUDは日本語表示を確実にするため、Windowsの動的フォントを使う。
    private TMP_FontAsset fontAsset;
    private readonly List<CharacterCard> characterCards = new List<CharacterCard>();
    private TextMeshProUGUI locationText;
    private TextMeshProUGUI actionText;
    private TextMeshProUGUI progressText;
    private Image redProgressFill;
    private Image blueProgressFill;
    private RectTransform detailContentRoot;
    private Image detailToggleButton;
    private TextMeshProUGUI detailToggleText;
    private bool detailsExpanded;
    private int selectedX = -1;
    private int selectedY = -1;
    private int lastTurnOrder = -1;

    private sealed class CharacterCard
    {
        public Character character;
        public RectTransform root;
        public Image background;
        public Image portrait;
        public Image hpFill;
        public Image staminaFill;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI staminaText;
        public TextMeshProUGUI inventoryText;
    }

    private void Start()
    {
        // 非表示にするのではなく、旧来の表示板はプレイ開始時に削除する。
        Destroy(GameObject.Find("RedStatus"));
        Destroy(GameObject.Find("BlueStatus"));

        if (wMgr == null) wMgr = FindObjectOfType<WorkerMgr>();
        BuildHud();
    }

    private void Update()
    {
        if (wMgr == null || wMgr.gridCtrl == null || !wMgr.gridCtrl.IsReady ||
            wMgr.characters == null || wMgr.characters.Count == 0) return;
        UpdateDetailToggleInput();
        UpdateSelectedCell();
        UpdateCharacterCards();
        UpdateLocationInformation();
        UpdateActionInformation();
        UpdateVictoryProgress();
    }

    private void BuildHud()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 低解像度のGameビューでも文字の実ピクセル数を確保する。
        // 縦方向を基準にするため、HUDが画面外へはみ出しにくい。
        scaler.referenceResolution = new Vector2(1600.0f, 900.0f);
        scaler.matchWidthOrHeight = 1.0f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        fontAsset = CreateJapaneseFont();

        GameObject rootObject = new GameObject("RightHud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        hudRoot = rootObject.GetComponent<RectTransform>();
        hudRoot.anchorMin = Vector2.one;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.pivot = Vector2.one;
        hudRoot.anchoredPosition = new Vector2(-18.0f, -18.0f);
        hudRoot.sizeDelta = new Vector2(HudWidth, HudHeight);
        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0.025f, 0.045f, 0.075f, 0.91f);
        rootImage.raycastTarget = false;

        TextMeshProUGUI heading = CreateText("Heading", hudRoot, 34, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        SetTopLeft(heading.rectTransform, 18.0f, 12.0f, HudWidth - 36.0f, 40.0f);
        heading.text = "戦況ボード";

        cardsRoot = CreateRect("CharacterCards", hudRoot);
        SetTopLeft(cardsRoot, 14.0f, 56.0f, HudWidth - 28.0f, 334.0f);

        CreateDetailToggle();

        detailContentRoot = CreateRect("DetailContent", hudRoot);
        SetTopLeft(detailContentRoot, 0.0f, 0.0f, HudWidth, HudHeight);

        Image locationPanel = CreatePanel("LocationPanel", 14.0f, 450.0f, HudWidth - 28.0f, 112.0f, detailContentRoot);
        CreateSectionTitle("現在地・選択地点", locationPanel.transform);
        locationText = CreateText("LocationText", locationPanel.rectTransform, 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.88f, 0.93f, 1.0f));
        SetTopLeft(locationText.rectTransform, 16.0f, 38.0f, HudWidth - 60.0f, 68.0f);
        locationText.enableWordWrapping = true;
        locationText.overflowMode = TextOverflowModes.Overflow;

        Image actionPanel = CreatePanel("ActionPanel", 14.0f, 574.0f, HudWidth - 28.0f, 142.0f, detailContentRoot);
        CreateSectionTitle("このターンに可能な行動", actionPanel.transform);
        actionText = CreateText("ActionText", actionPanel.rectTransform, 20, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetTopLeft(actionText.rectTransform, 16.0f, 38.0f, HudWidth - 60.0f, 98.0f);
        actionText.enableWordWrapping = true;
        actionText.overflowMode = TextOverflowModes.Overflow;
        actionText.lineSpacing = 0.84f;

        Image progressPanel = CreatePanel("ProgressPanel", 14.0f, 728.0f, HudWidth - 28.0f, 104.0f, detailContentRoot);
        CreateSectionTitle("勝利進行", progressPanel.transform);
        progressText = CreateText("ProgressText", progressPanel.rectTransform, 21, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetTopLeft(progressText.rectTransform, 16.0f, 34.0f, HudWidth - 60.0f, 44.0f);
        progressText.enableWordWrapping = true;
        progressText.overflowMode = TextOverflowModes.Overflow;
        redProgressFill = CreateProgressBar(progressPanel.transform, 16.0f, 72.0f, new Color(1.0f, 0.22f, 0.19f));
        blueProgressFill = CreateProgressBar(progressPanel.transform, 16.0f, 88.0f, new Color(0.18f, 0.58f, 1.0f));

        foreach (Character character in wMgr.characters) characterCards.Add(CreateCharacterCard(character));
        SetDetailsExpanded(false);
    }

    private Image CreatePanel(string objectName, float x, float y, float width, float height, Transform parent = null)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent == null ? hudRoot : parent, false);
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        SetTopLeft(rectTransform, x, y, width, height);
        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.23f, 0.9f);
        image.raycastTarget = false;
        return image;
    }

    // 常時表示するキャラクター欄と、必要時だけ見る詳細欄を分けるための開閉ボタン。
    private void CreateDetailToggle()
    {
        GameObject buttonObject = new GameObject("DetailToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonObject.transform.SetParent(hudRoot, false);
        detailToggleButton = buttonObject.GetComponent<Image>();
        SetTopLeft(detailToggleButton.rectTransform, 14.0f, 402.0f, HudWidth - 28.0f, 36.0f);
        detailToggleButton.color = new Color(0.13f, 0.34f, 0.53f, 0.98f);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.82f, 1.0f, 0.62f);
        outline.effectDistance = new Vector2(1.0f, -1.0f);

        detailToggleText = CreateText("Label", buttonObject.transform, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        detailToggleText.rectTransform.anchorMin = Vector2.zero;
        detailToggleText.rectTransform.anchorMax = Vector2.one;
        detailToggleText.rectTransform.offsetMin = Vector2.zero;
        detailToggleText.rectTransform.offsetMax = Vector2.zero;
    }

    private void UpdateDetailToggleInput()
    {
        if (detailToggleButton == null || !Input.GetMouseButtonDown(0)) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(detailToggleButton.rectTransform, Input.mousePosition)) return;
        SetDetailsExpanded(!detailsExpanded);
    }

    private void SetDetailsExpanded(bool expanded)
    {
        detailsExpanded = expanded;
        if (detailContentRoot != null) detailContentRoot.gameObject.SetActive(expanded);
        if (hudRoot != null) hudRoot.sizeDelta = new Vector2(HudWidth, expanded ? HudHeight : CollapsedHudHeight);
        if (detailToggleText != null) detailToggleText.text = expanded ? "詳細を閉じる  ▲" : "詳細を見る  ▼";
        if (detailToggleButton != null)
        {
            detailToggleButton.color = expanded
                ? new Color(0.19f, 0.47f, 0.68f, 0.98f)
                : new Color(0.13f, 0.34f, 0.53f, 0.98f);
        }
    }

    private void CreateSectionTitle(string label, Transform parent)
    {
        TextMeshProUGUI title = CreateText("Title", parent, 24, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.96f, 0.80f, 0.37f));
        SetTopLeft(title.rectTransform, 12.0f, 8.0f, HudWidth - 70.0f, 30.0f);
        title.text = label;
    }

    private CharacterCard CreateCharacterCard(Character character)
    {
        GameObject cardObject = new GameObject("CharacterCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObject.transform.SetParent(cardsRoot, false);
        RectTransform cardRoot = cardObject.GetComponent<RectTransform>();
        cardRoot.anchorMin = new Vector2(0.0f, 1.0f);
        cardRoot.anchorMax = new Vector2(0.0f, 1.0f);
        cardRoot.pivot = new Vector2(0.0f, 1.0f);
        cardRoot.sizeDelta = new Vector2(HudWidth - 28.0f, 74.0f);

        Image background = cardObject.GetComponent<Image>();
        background.raycastTarget = false;
        Image stripe = CreateImage("TeamStripe", cardRoot, TeamColor(character.teamNum));
        stripe.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
        stripe.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        stripe.rectTransform.pivot = new Vector2(0.0f, 0.5f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;
        stripe.rectTransform.sizeDelta = new Vector2(6.0f, 0.0f);

        // 名前をアイコンの上に置き、横方向をアイテム欄へ回す。
        TextMeshProUGUI nameText = CreateText("Name", cardRoot, 18, FontStyle.Bold, TextAnchor.UpperCenter, TeamColor(character.teamNum));
        SetTopLeft(nameText.rectTransform, 7.0f, 3.0f, 64.0f, 21.0f);
        Image portrait = CreateImage("Portrait", cardRoot, Color.white);
        SetTopLeft(portrait.rectTransform, 20.0f, 28.0f, 38.0f, 38.0f);

        // 数字とメーターを同じ縦位置へ置かず、二段を明確に分離する。
        TextMeshProUGUI hpText = CreateText("HpNumber", cardRoot, 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetTopLeft(hpText.rectTransform, 78.0f, 3.0f, 116.0f, 19.0f);
        Image hpFill = CreateMeter(cardRoot, "Hp", 78.0f, 29.0f, new Color(0.33f, 0.95f, 0.48f));

        TextMeshProUGUI staminaText = CreateText("StaminaNumber", cardRoot, 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetTopLeft(staminaText.rectTransform, 78.0f, 38.0f, 116.0f, 19.0f);
        Image staminaFill = CreateMeter(cardRoot, "Stamina", 78.0f, 63.0f, new Color(1.0f, 0.83f, 0.25f));

        // カード右端までを所持品欄に割り当て、空いた横幅を残さない。
        TextMeshProUGUI inventoryText = CreateText("Inventory", cardRoot, 20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.91f, 0.94f, 1.0f));
        SetTopLeft(inventoryText.rectTransform, 198.0f, 6.0f, HudWidth - 236.0f, 60.0f);
        inventoryText.enableWordWrapping = true;
        inventoryText.overflowMode = TextOverflowModes.Overflow;
        inventoryText.lineSpacing = 0.84f;

        /*
         * 左から 名前・アイコン / HP・スタミナ / 所持品 の三列。
         * 所持品列はカード右端の10px手前までを使う。
         */

        return new CharacterCard
        {
            character = character,
            root = cardRoot,
            background = background,
            portrait = portrait,
            hpFill = hpFill,
            staminaFill = staminaFill,
            nameText = nameText,
            hpText = hpText,
            staminaText = staminaText,
            inventoryText = inventoryText
        };
    }

    private Image CreateMeter(Transform parent, string objectName, float x, float y, Color fillColor)
    {
        Image back = CreateImage(objectName + "Back", parent, new Color(0.015f, 0.025f, 0.045f, 0.9f));
        SetTopLeft(back.rectTransform, x, y, 111.0f, 9.0f);
        Image fill = CreateImage(objectName + "Fill", back.rectTransform, fillColor);
        fill.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
        fill.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        fill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        fill.rectTransform.sizeDelta = new Vector2(111.0f, 0.0f);
        return fill;
    }

    private Image CreateProgressBar(Transform parent, float x, float y, Color color)
    {
        Image back = CreateImage("ProgressBack", parent, new Color(0.015f, 0.025f, 0.045f, 0.9f));
        SetTopLeft(back.rectTransform, x, y, HudWidth - 60.0f, 12.0f);
        Image fill = CreateImage("ProgressFill", back.rectTransform, color);
        fill.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
        fill.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        fill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        fill.rectTransform.sizeDelta = Vector2.zero;
        return fill;
    }

    private void UpdateSelectedCell()
    {
        Character activeCharacter = GetActiveCharacter();
        if (activeCharacter == null) return;

        // 新しい手番では、最初に操作キャラクターの現在地を表示する。
        if (lastTurnOrder != wMgr.turnOrder || selectedX < 0 || selectedY < 0)
        {
            selectedX = activeCharacter.xCell;
            selectedY = activeCharacter.yCell;
            lastTurnOrder = wMgr.turnOrder;
        }

        // HUD上のクリックは無視し、盤面だけを地点選択に用いる。
        if (!Input.GetMouseButtonDown(0) || RectTransformUtility.RectangleContainsScreenPoint(hudRoot, Input.mousePosition)) return;
        Camera mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = wMgr.camera;
        if (mainCamera == null) return;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        int cellX = Mathf.RoundToInt(worldPosition.x);
        int cellY = Mathf.RoundToInt(worldPosition.y);
        if (!wMgr.gridCtrl.IsInsideCell(cellX, cellY)) return;
        selectedX = cellX;
        selectedY = cellY;
    }

    private void UpdateCharacterCards()
    {
        if (wMgr.characters.Count != characterCards.Count) return;
        float nextY = 0.0f;

        // 手番のキャラクターを先頭へ。座標と高さを補間して、手番交代時に並び替える。
        for (int offset = 0; offset < wMgr.characters.Count; offset++)
        {
            int index = (wMgr.turnOrder + offset) % wMgr.characters.Count;
            Character character = wMgr.characters[index];
            CharacterCard card = characterCards.Find(value => value.character == character);
            if (card == null) continue;

            bool isActive = offset == 0;
            float targetHeight = isActive ? 90.0f : 74.0f;
            card.root.anchoredPosition = Vector2.Lerp(card.root.anchoredPosition, new Vector2(0.0f, -nextY), Time.unscaledDeltaTime * 10.0f);
            card.root.sizeDelta = Vector2.Lerp(card.root.sizeDelta, new Vector2(HudWidth - 28.0f, targetHeight), Time.unscaledDeltaTime * 10.0f);
            card.root.SetSiblingIndex(offset);
            nextY += targetHeight + 5.0f;
            UpdateCharacterCard(card, isActive);
        }
    }

    private void UpdateCharacterCard(CharacterCard card, bool isActive)
    {
        Character character = card.character;
        Color teamColor = TeamColor(character.teamNum);
        card.background.color = isActive
            ? new Color(teamColor.r * 0.31f, teamColor.g * 0.31f, teamColor.b * 0.31f, 0.97f)
            : new Color(0.10f, 0.14f, 0.20f, 0.96f);

        SpriteRenderer spriteRenderer = character.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null) card.portrait.sprite = spriteRenderer.sprite;
        card.portrait.color = character.ghost ? new Color(1.0f, 1.0f, 1.0f, 0.30f) : Color.white;
        card.nameText.color = teamColor;
        card.nameText.text = GetCharacterName(character) + (character.ghost ? "\n復活待ち " + character.RespawnTurnsRemaining : "");

        float hpRatio = character.hp == null || character.hp.maxP <= 0.0f ? 0.0f : character.hp.preP / character.hp.maxP;
        float staminaRatio = character.stamina == null || character.stamina.maxP <= 0.0f ? 0.0f : character.stamina.preP / character.stamina.maxP;
        SetHorizontalFill(card.hpFill, hpRatio, 111.0f);
        SetHorizontalFill(card.staminaFill, staminaRatio, 111.0f);
        card.hpText.text = "HP " + Mathf.CeilToInt(character.hp.preP) + " / " + Mathf.CeilToInt(character.hp.maxP);
        card.staminaText.text = "STA " + character.stamina.preP.ToString("0.0") + " / " + character.stamina.maxP.ToString("0");

        int ownFragments = character.CountItems(BoardItemType.Fragment, character.teamNum);
        int ownPearls = character.CountItems(BoardItemType.Pearl, character.teamNum);
        int scrolls = character.CountItems(BoardItemType.PeelScroll, null);
        card.inventoryText.text = "片×" + ownFragments + "  珠×" + ownPearls + "\n符×" + scrolls + "  枠 " + character.InventoryCount + "/30";
    }

    private void UpdateLocationInformation()
    {
        Character activeCharacter = GetActiveCharacter();
        if (activeCharacter == null) return;
        Team? owner = wMgr.gridCtrl.GetMoyoTeamAt(selectedX, selectedY);
        string territory = !owner.HasValue ? "中立地" : owner.Value == activeCharacter.teamNum ? "自軍模様領域" : "敵模様領域";
        int hpDelta = wMgr.gridCtrl.GetEndOfTurnHpDelta(selectedX, selectedY, activeCharacter.teamNum);
        string hpDeltaLabel = hpDelta > 0 ? "HP +" + hpDelta : hpDelta < 0 ? "HP " + hpDelta : "HP ±0";
        locationText.text = "(" + selectedX + ", " + selectedY + ")  " + territory + "\n"
                          + "行動終了時の予定: " + hpDeltaLabel + "\n"
                          + "盤面をクリックすると、この欄の地点を調べられます。";
    }

    private void UpdateActionInformation()
    {
        Character activeCharacter = GetActiveCharacter();
        if (activeCharacter == null) return;

        bool canPlaceHado = !activeCharacter.ghost && !activeCharacter.IsMoving && activeCharacter.ResidueHado > 0 &&
                            activeCharacter.HasCompletedWalkThisTurn && wMgr.gridCtrl.isHadoVacant(activeCharacter.xCell, activeCharacter.yCell);
        int fragments = activeCharacter.CountItems(BoardItemType.Fragment, activeCharacter.teamNum);
        bool canConvert = !activeCharacter.IsMoving && fragments >= FragmentPerPearl;
        bool canManageItems = !activeCharacter.IsMoving;
        bool canDeposit = wMgr.gridCtrl.CanDepositPearl(activeCharacter);

        actionText.text = ActionLine("Z", "波動石", canPlaceHado, canPlaceHado ? "足元に設置できます" : "歩行後、空白セルで設置") + "\n"
                        + ActionLine("V", "波動珠化", canConvert, "自軍波動片 " + fragments + "/" + FragmentPerPearl + "（確認あり）") + "\n"
                        + ActionLine("A", "アイテム行動", canManageItems, "取捨・投擲・使用を選択") + "\n"
                        + ActionLine("E", "敵結界へ投入", canDeposit, canDeposit ? "自軍珠を投入できます" : "敵結界の隣で自軍珠が必要") + "\n"
                        + "<color=#D7DDEA>X</color>  手番を終了";
    }

    private void UpdateVictoryProgress()
    {
        int redDeposits = wMgr.gridCtrl.GetDepositedPearls(Team.Red);
        int blueDeposits = wMgr.gridCtrl.GetDepositedPearls(Team.Blue);
        int required = wMgr.gridCtrl.PearlsNeededForVictory;
        progressText.text = "赤  敵結界へ " + redDeposits + " / " + required + " 個投入\n"
                            + "青  敵結界へ " + blueDeposits + " / " + required + " 個投入";
        SetHorizontalFill(redProgressFill, (float)redDeposits / required, HudWidth - 60.0f);
        SetHorizontalFill(blueProgressFill, (float)blueDeposits / required, HudWidth - 60.0f);
    }

    private Character GetActiveCharacter()
    {
        if (wMgr == null || wMgr.characters == null || wMgr.characters.Count == 0) return null;
        if (wMgr.turnOrder < 0 || wMgr.turnOrder >= wMgr.characters.Count) return null;
        return wMgr.characters[wMgr.turnOrder];
    }

    private static string ActionLine(string key, string action, bool available, string detail)
    {
        string color = available ? "#6FF3A6" : "#AAB6C8";
        return "<color=" + color + ">" + key + "  " + action + "  [" + (available ? "可能" : "不可") + "]</color>  " + detail;
    }

    private static string GetCharacterName(Character character)
    {
        string species = character is BearController ? "クマ" : character is PenguinController ? "ペンギン" : "キャラクター";
        return (character.teamNum == Team.Red ? "赤" : "青") + species;
    }

    private static Color TeamColor(Team team)
    {
        return team == Team.Red ? new Color(1.0f, 0.33f, 0.28f) : new Color(0.28f, 0.65f, 1.0f);
    }

    private static void SetHorizontalFill(Image image, float ratio, float maxWidth)
    {
        ratio = Mathf.Clamp01(ratio);
        image.rectTransform.sizeDelta = new Vector2(maxWidth * ratio, 0.0f);
    }

    // プロジェクトに同梱した日本語フォントから、拡大縮小しても輪郭を保つ動的SDFフォントを作る。
    // OSのフォント名に依存すると環境によって日本語グリフを取得できないため、必ず同梱フォントを優先する。
    private static TMP_FontAsset CreateJapaneseFont()
    {
        Font bundledJapaneseFont = Resources.Load<Font>("Fonts/NotoSansJP-VF");
        if (bundledJapaneseFont != null)
        {
            TMP_FontAsset bundledFontAsset = TMP_FontAsset.CreateFontAsset(
                bundledJapaneseFont, 128, 8, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (bundledFontAsset != null) return bundledFontAsset;
        }

        // 同梱フォントのUnityインポート前だけに使う予備経路です。
        string[] candidates = { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic" };
        foreach (string fontName in candidates)
        {
            Font sourceFont = Font.CreateDynamicFontFromOSFont(fontName, 128);
            if (sourceFont == null) continue;

            TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(
                sourceFont, 128, 8, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (tmpFont != null) return tmpFont;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = ToTmpAlignment(alignment);
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.extraPadding = true;
        text.richText = true;
        return text;
    }

    private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
    {
        if (alignment == TextAnchor.UpperCenter) return TextAlignmentOptions.Top;
        if (alignment == TextAnchor.MiddleCenter) return TextAlignmentOptions.Center;
        return TextAlignmentOptions.TopLeft;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void SetTopLeft(RectTransform rectTransform, float x, float y, float width, float height)
    {
        rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
        rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        rectTransform.pivot = new Vector2(0.0f, 1.0f);
        rectTransform.anchoredPosition = new Vector2(x, -y);
        rectTransform.sizeDelta = new Vector2(width, height);
    }
}
