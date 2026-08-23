using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public enum CellType : int
{
    VACANT = 0,
    OCEAN = 1,
    ROCK = 2
}

public class GridCtrl : MonoBehaviour
{

    //public List<List<GameObject>> cells;


    public const int BoardWidth = 25;
    public const int BoardHeight = 37;

    public TextMeshProUGUI textMeshPro;

    public WorkerMgr wMgr;
    public int STAGE_SIZE_X = BoardWidth;
    public int STAGE_SIZE_Y = BoardHeight;
    
    public Character[,] playerLayer;
    public HadoCtrl[,] hadoLayer;
    public CellType[,] typeLayer;

    public Canvas ca;

    public GameObject[,] cellTexts;
    public TextMeshProUGUI[,] cellTextContents;
    private Image[,] moyoOverlays;
    private int[,] moyoOverlayTeams;
    private bool[,] eyeOverlayCells;
    // 地パワー計算用。どの陣営の眼かを保持する。
    private bool[,] redEyeCells;
    private bool[,] blueEyeCells;
    private Tilemap fieldTilemap;
    private TileBase plainSeaTile;

    // 盤上アイテムは一つのセルに複数落ちることがある（死亡時の全所持品）。
    // 通常湧きだけは空のセルに限るため、リストが空かどうかで判定する。
    private List<BoardItemData>[,] groundItems;
    private Image[,] itemIcons;

    // 結界は海マスそのものを地形変更せず、表示と投入判定だけを追加する。
    private Team?[,] gateTeams;
    private readonly Dictionary<Image, Team> gateImageTeams = new Dictionary<Image, Team>();
    private int redPearlsDeposited;
    private int bluePearlsDeposited;
    private bool gameOver;

    // キャラクター本体を変更せず、移動中だけ統一領域からの流入を描くための表示専用データ。
    private readonly List<EnergyFlowParticle> energyFlowParticles = new List<EnergyFlowParticle>();
    private readonly Dictionary<Character, Vector3> previousCharacterPositions = new Dictionary<Character, Vector3>();
    private float nextEnergyFlowEmissionTime;

    private enum ItemInteractionMode
    {
        None,
        ActionSelect,
        TakeAndDrop,
        ThrowItemSelect,
        ThrowTargetSelect,
        UseItemSelect,
        ConvertFragmentsConfirm
    }

    private sealed class ItemMenuEntry
    {
        public bool isGroundItem;
        public BoardItemType type;
        public Team? team;
        public int availableCount;
        public int selectedCount;
    }

    // アイテム行動は現在手番の一人だけが操作する。画面表示はキャラクター右側に追従する。
    private ItemInteractionMode itemInteractionMode;
    private Character itemInteractionCharacter;
    private readonly List<ItemMenuEntry> itemMenuEntries = new List<ItemMenuEntry>();
    private int itemMenuCursor;
    private int itemActionCursor;
    private BoardItemData selectedThrowItem;
    private Vector2Int throwTargetCell;
    private GameObject itemMenuObject;
    private TextMeshProUGUI itemMenuText;
    private TMP_FontAsset itemMenuFont;
    private Image itemMenuCursorHighlight;
    private TextMeshProUGUI conversionYesText;
    private TextMeshProUGUI conversionNoText;
    private string itemMenuStatusMessage;
    private readonly HashSet<Character> conversionPromptDismissed = new HashSet<Character>();
    private bool suppressConversionDismissOnClose;
    private readonly List<Image> throwRangeIndicators = new List<Image>();
    private Image throwCursorIndicator;

    public int[,] red_energy;
    public int[,] blue_energy;

    // スクリプト再読み込み直後に、HUDが未初期化の盤面配列へ触れないための状態。
    public bool IsReady => playerLayer != null && hadoLayer != null && typeLayer != null &&
                           red_energy != null && blue_energy != null &&
                           redEyeCells != null && blueEyeCells != null;

    // 模様パワーは小数で保持し、地パワーへ変換するときだけ整数化する。
    // 途中で整数化すると遠距離の波及が0になり、意図しない最大到達距離が生じる。
    private double[,] redMoyoPower;
    private double[,] blueMoyoPower;

    private const double MoyoRegionThreshold = 10.0;
    // 波動石群の各空白接触辺が持つ基礎発出量。
    private const double SurfacePowerUnit = 5.0;
    private const double DistanceFalloffExponent = 1.5;
    private const int MaxEyeCandidateSize = 18;
    // 通常の統一領域は盤面を覆い過ぎない濃さに留め、眼だけを一段濃くする。
    private const float MoyoOverlayBaseAlpha = 0.08f;
    private const float MoyoOverlayPulseAlpha = 0.02f;
    private const float EyeOverlayBaseAlpha = 0.18f;
    private const float EyeOverlayPulseAlpha = 0.03f;
    private const float EnergyFlowEmissionInterval = 0.07f;
    private const float EnergyFlowDuration = 0.52f;
    private const int MaximumEnergyFlowParticles = 24;
    private const float FragmentSpawnChance = 0.01f;
    private const int FragmentPerPearl = 20;
    private const int PearlsRequiredForVictory = 5;
    private const int ExtensionFieldWidth = 11;
    private const int ExtensionFieldDepth = 6;
    private static readonly Color RedMoyoTextColor = new Color(0.92f, 0.27f, 0.24f, 0.42f);
    private static readonly Color BlueMoyoTextColor = new Color(0.25f, 0.58f, 1.0f, 0.42f);

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    private static readonly Vector2Int[] DiagonalDirections =
    {
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(1, 1)
    };

    private sealed class EyeRoom
    {
        public readonly List<Vector2Int> cells;
        public readonly List<Vector2Int> boundaryStoneCells;
        public readonly int eyeCount;

        public EyeRoom(List<Vector2Int> cells, List<Vector2Int> boundaryStoneCells, int eyeCount)
        {
            this.cells = cells;
            this.boundaryStoneCells = boundaryStoneCells;
            this.eyeCount = eyeCount;
        }
    }

    private sealed class EnergyFlowParticle
    {
        public readonly RectTransform rectTransform;
        public readonly Image image;
        public Vector3 source;
        public Vector3 destination;
        public Color color;
        public float startedAt;
        public float duration;
        public bool active;

        public EnergyFlowParticle(RectTransform rectTransform, Image image)
        {
            this.rectTransform = rectTransform;
            this.image = image;
        }
    }

    // Start is called before the first frame update
    

    private void Awake()//盤面自体の生成
    {
        // 既存シーンのシリアライズ値に左右されず、横25×縦37の盤面を使う。
        STAGE_SIZE_X = BoardWidth;
        STAGE_SIZE_Y = BoardHeight;
        transform.position = new Vector2(STAGE_SIZE_X / 2, STAGE_SIZE_Y / 2);

        playerLayer = new Character[STAGE_SIZE_X, STAGE_SIZE_Y];


        hadoLayer = new HadoCtrl[STAGE_SIZE_X, STAGE_SIZE_Y];
        typeLayer = new CellType[STAGE_SIZE_X, STAGE_SIZE_Y];

        ca = GetComponentInChildren<Canvas>();
        cellTexts = new GameObject[STAGE_SIZE_X, STAGE_SIZE_Y];
        cellTextContents = new TextMeshProUGUI[STAGE_SIZE_X, STAGE_SIZE_Y];
        moyoOverlays = new Image[STAGE_SIZE_X, STAGE_SIZE_Y];
        moyoOverlayTeams = new int[STAGE_SIZE_X, STAGE_SIZE_Y];
        eyeOverlayCells = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        redEyeCells = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        blueEyeCells = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        groundItems = new List<BoardItemData>[STAGE_SIZE_X, STAGE_SIZE_Y];
        itemIcons = new Image[STAGE_SIZE_X, STAGE_SIZE_Y];
        gateTeams = new Team?[STAGE_SIZE_X, STAGE_SIZE_Y];

        red_energy = new int [STAGE_SIZE_X, STAGE_SIZE_Y];
        blue_energy = new int[STAGE_SIZE_X, STAGE_SIZE_Y];
        redMoyoPower = new double[STAGE_SIZE_X, STAGE_SIZE_Y];
        blueMoyoPower = new double[STAGE_SIZE_X, STAGE_SIZE_Y];




        for (int ix = 0; ix < STAGE_SIZE_X; ix++)
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                typeLayer[ix, iy] = CellType.OCEAN;
                groundItems[ix, iy] = new List<BoardItemData>();


                cellTexts[ix, iy] = Instantiate(wMgr.CellTextPrefab, new Vector2(ix, iy), Quaternion.identity);//全マスにCellTextを設置
                cellTexts[ix, iy].transform.parent = transform;//波及波動を記すcellTextをGridの子ヒエラルキーにする。
                cellTexts[ix, iy].transform.parent = ca.transform;
                cellTextContents[ix, iy] = cellTexts[ix, iy].GetComponent<TextMeshProUGUI>();



                cellTextContents[ix, iy].text = "";
                cellTextContents[ix, iy].rectTransform.sizeDelta = new Vector2(1.2f, 0.2f);
                cellTextContents[ix, iy].fontSize = 0.5f;
                cellTextContents[ix, iy].alignment = TextAlignmentOptions.Center;
                cellTextContents[ix, iy].color = new Color(0.08f, 0.08f, 0.08f, 0.42f);

                CreateMoyoOverlay(ix, iy);
            }
        }

        ConfigureFieldLayout();

        int donutSize = 1;
        for (int ix = ((STAGE_SIZE_X-1)/2) - donutSize; ix < ((STAGE_SIZE_X - 1) / 2) + donutSize+1; ix++)
        {
            for (int iy = ((STAGE_SIZE_Y - 1) / 2) - donutSize; iy < ((STAGE_SIZE_Y - 1) / 2) + donutSize+1; iy++)
            {
                typeLayer[ix, iy] = CellType.OCEAN;//全マスをVACANTにセット
            }
        }

        ConfigureFieldTilemap();

        CreateGate(Team.Red);
        CreateGate(Team.Blue);


    }

    /// <summary>
    /// 中央の野原（23×23）に、上下それぞれ幅11・奥行き6の野原通路を接続する。
    /// それ以外のセルは海とし、結界は上下端の海マスへ置く。
    /// </summary>
    private void ConfigureFieldLayout()
    {
        int centerX = STAGE_SIZE_X / 2;
        int extensionMinX = centerX - ExtensionFieldWidth / 2;
        int extensionMaxX = centerX + ExtensionFieldWidth / 2;
        int centralMinY = ExtensionFieldDepth + 1;
        int centralMaxY = STAGE_SIZE_Y - ExtensionFieldDepth - 2;

        // 中央の広い野原。
        for (int x = 1; x < STAGE_SIZE_X - 1; x++)
        {
            for (int y = centralMinY; y <= centralMaxY; y++)
            {
                typeLayer[x, y] = CellType.VACANT;
            }
        }

        // 結界側へ伸びる上下の野原通路。
        for (int x = extensionMinX; x <= extensionMaxX; x++)
        {
            for (int y = 1; y <= ExtensionFieldDepth; y++)
            {
                typeLayer[x, y] = CellType.VACANT;
            }
            for (int y = STAGE_SIZE_Y - ExtensionFieldDepth - 1; y < STAGE_SIZE_Y - 1; y++)
            {
                typeLayer[x, y] = CellType.VACANT;
            }
        }
    }

    // 既存のTilemapから草・海の縁タイルを取得し、論理盤面と同じ形へ実行時に描き直す。
    // 海は野原に接する方向だけ縁タイルを使い、外海は草線のない水面にする。
    private void ConfigureFieldTilemap()
    {
        fieldTilemap = GetComponentInChildren<Tilemap>();
        if (fieldTilemap == null) return;

        int centerY = STAGE_SIZE_Y / 2;
        TileBase grassTile = fieldTilemap.GetTile(fieldTilemap.WorldToCell(new Vector3(5, centerY, 0.0f)));
        if (grassTile == null) return;

        Dictionary<string, TileBase> seaEdgeTiles = CaptureSeaEdgeTiles();
        plainSeaTile = CreatePlainSeaTile();

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                Vector3Int tileCell = fieldTilemap.WorldToCell(new Vector3(x, y, 0.0f));
                TileBase tile = typeLayer[x, y] == CellType.VACANT
                    ? grassTile
                    : SelectSeaTile(x, y, seaEdgeTiles);
                fieldTilemap.SetTile(tileCell, tile);
            }
        }
        fieldTilemap.RefreshAllTiles();
    }

    private Dictionary<string, TileBase> CaptureSeaEdgeTiles()
    {
        Dictionary<string, TileBase> tiles = new Dictionary<string, TileBase>();
        foreach (Vector3Int position in fieldTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = fieldTilemap.GetTile(position);
            if (tile == null || !tile.name.StartsWith("Sea_")) continue;
            tiles[tile.name] = tile;
        }
        return tiles;
    }

    // Sea_0〜8 は、上下左右の野原へ接する向きごとの縁タイルである。
    // 野原と辺接続しない外海では、草線のない水面タイルを返す。
    private TileBase SelectSeaTile(int x, int y, Dictionary<string, TileBase> seaEdgeTiles)
    {
        bool north = IsVacantFieldCell(x, y + 1);
        bool east = IsVacantFieldCell(x + 1, y);
        bool south = IsVacantFieldCell(x, y - 1);
        bool west = IsVacantFieldCell(x - 1, y);

        string tileName = null;
        if (north && east) tileName = "Sea_6";
        else if (north && west) tileName = "Sea_8";
        else if (south && east) tileName = "Sea_0";
        else if (south && west) tileName = "Sea_2";
        else if (north) tileName = "Sea_7";
        else if (east) tileName = "Sea_3";
        else if (south) tileName = "Sea_1";
        else if (west) tileName = "Sea_5";

        if (tileName != null && seaEdgeTiles.TryGetValue(tileName, out TileBase edgeTile)) return edgeTile;
        return plainSeaTile;
    }

    private bool IsVacantFieldCell(int x, int y)
    {
        return IsInside(x, y) && typeLayer[x, y] == CellType.VACANT;
    }

    private TileBase CreatePlainSeaTile()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;

        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                float ripple = Mathf.PerlinNoise(x * 0.17f, y * 0.17f);
                Color water = Color.Lerp(new Color(0.18f, 0.80f, 0.83f), new Color(0.46f, 0.94f, 0.94f), ripple);
                texture.SetPixel(x, y, water);
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = "RuntimePlainSea";
        tile.sprite = sprite;
        return tile;
    }


    void Start()//WorkerMgr側のAwakeも完了してからでないと実行してはいけないようなことを実行する。
    {
        foreach (Character c in wMgr.characters)//初期位置にキャラを設置
        {
            playerLayer[c.xCell, c.yCell] = c;
            Debug.Log("初期配置" + c.xCell.ToString() +","+  c.yCell.ToString());
        }

        playerLayer[0, 0] = null;
    }

    //public void PutAllCharacters()
    //{
    //    foreach (Character c in wMgr.characters)//初期位置にキャラを設置
    //    {
    //        playerLayer[c.xCell, c.yCell] = c;
    //    }
    //}


    // Update is called once per frame
    void Update()
    {
        AnimateMoyoOverlays();
        UpdateEnergyFlowVisualization();
        AnimateGates();
    }

    public void moveCharacter(int _xCell, int _yCell, Character c)
    {

        playerLayer[(int)c.transform.position.x, (int)c.transform.position.y] = null;
        playerLayer[_xCell, _yCell] = c;
    }
    public void moveCharacter(int _xCell, int _yCell, int new_xCell, int new_yCell)
    {
        playerLayer[new_xCell, new_yCell] = playerLayer[_xCell, _yCell];
        playerLayer[_xCell, _yCell] = null;
    }



    public void createHado(int _xCell, int _yCell, HadoCtrl hado)
    {
        hadoLayer[_xCell, _yCell] = hado;
        ApplyHadoPlacement(_xCell, _yCell);
    }


    public void giveHadoPower(int newX, int newY)
    {
        var ret = new int[STAGE_SIZE_X, STAGE_SIZE_Y];
        //for (int ix = 0; ix < stageSizeX; ix++)
        //{
        //    for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
        //    {
        //        ret[ix, iy] = 0;
        //    }
        //}
        Action generatePower = () =>
        {
            for (int ix = 0; ix < STAGE_SIZE_X; ix++)//HadoPowerを全ての空白マスから伝播させる
            {
                for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
                {
                    var h = hadoLayer[ix, iy];
                    if (h != null || typeLayer[ix, iy] != CellType.VACANT) continue;//HadoPower源でないマスはスルー
                    Action<int, int> f = (xx, yy) =>
                    {
                        var usePath = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
                        provideHadoPower(ret, usePath, xx, yy);
                    };

                    f(ix - 1, iy);
                    f(ix + 1, iy);
                    f(ix, iy - 1);
                    f(ix, iy + 1);//空白マスの全方位調査
                }
            }
        };

        Action<Team> findDeadHado = checkedTeam =>
        {
            for (int ix = 0; ix < STAGE_SIZE_X; ix++)//HadoPowerが敵色かつ0のHadoを探し出して消す。
            {
                for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
                {
                    if (hadoLayer[ix, iy] != null)
                    {
                        
                        if (ret[ix, iy] <= 0 & hadoLayer[ix, iy].teamColor == checkedTeam)//力が0かつ被検査色の波動は消える
                        {
                            string ap = "X:" + ix.ToString() + " Y:" + iy.ToString() + " power:" + ret[ix, iy];
                            Debug.Log(ap);

                            wMgr.deleteHado(hadoLayer[ix, iy].teamColor, hadoLayer[ix, iy].ID);
                            hadoLayer[ix, iy] = null;
                        }
                        //else if(hadoLayer[ix, iy].teamColor == checkedTeam)//非検査色のそれ以外の波動は力がセットされる。
                        //{
                        //    hadoLayer[ix, iy].setPower(100);
                        //}
                    }
                }
            }
        };

        generatePower();

        Team inspectedTeam;
        if (hadoLayer[newX, newY].teamColor == Team.Red) { inspectedTeam = Team.Blue; }//今置いた色でない方の色を検査色にする。先に敵の色の生死を判定。
        else { inspectedTeam = Team.Red; }
        findDeadHado(inspectedTeam);
        
        generatePower();//死んだ敵色が居なくなってからもう一度、HadoPowerを計算。
        findDeadHado(hadoLayer[newX, newY].teamColor);


        spreadHadoEnergy();//波動の生成削除が終わったので、そこから染み出る波動エナジーを計算。

    }

    void provideHadoPower(int[,] ret, bool[,] usePath, int x, int y)
    {

        if (x < 0 || STAGE_SIZE_X <= x || y < 0 || STAGE_SIZE_Y <= y) return;
        var h = hadoLayer[x, y];
        if (h == null) return;
        usePath[x,y] = true;
        ret[x, y]++;//対象地の生きる力を増加させる.
        Action<int, int> f = (xx, yy) =>
        {
            if (xx < 0 || STAGE_SIZE_X <= xx || yy < 0 || STAGE_SIZE_Y <= yy) return;

            if (usePath[xx, yy] || hadoLayer[x, y].teamColor != hadoLayer[xx, yy]?.teamColor) return ;

            provideHadoPower(ret, usePath, xx, yy);//周囲を敵に囲まれた波動の生きる力が残存している。どうする？
        };
        f(x - 1, y);
        f(x + 1, y);
        f(x, y - 1);
        f(x, y + 1);
    }



    private int gatherEnergyLegacy(int x, int y, Team t, ref bool[,] b)
    {
        if(x >= 0 && STAGE_SIZE_X > x && y >= 0 && STAGE_SIZE_Y > y)
        {
            if (b[x,y] == false)//未処理のマスなら
            {
                b[x,y] = true;//処理済に変更
                int p = 0;

                if (t == Team.Red)
                {
                    p = red_energy[x, y];
                }
                if (t == Team.Blue)
                {
                    p = blue_energy[x, y];
                }
                if (p == 0) return 0;

                p += gatherEnergyLegacy(x - 1, y, t, ref b);
                p += gatherEnergyLegacy(x + 1, y, t, ref b);
                p += gatherEnergyLegacy(x, y + 1, t, ref b);
                p += gatherEnergyLegacy(x, y - 1, t, ref b);

                return p;
            }
        }
        return 0;
    }










    public void spreadHadoEnergy()
    {
        for (int ix = 0; ix < STAGE_SIZE_X; ix++)//全マス探索
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                blue_energy[ix, iy] = 0;
                red_energy[ix, iy] = 0;
            }
        }


                for (int ix = 0; ix < STAGE_SIZE_X; ix++)//全マス探索
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                if (hadoLayer[ix, iy] != null)//Hadoがある地点のエネルギーを設定。
                {
                    

                    Action<int, int, int, Team> f = (sourceX, sourceY, myTeamPoint, team) =>
                    {

                        for (int jx = sourceX - 7; jx <= sourceX + 7; jx++)//根源波動の座標から+=7以内にエネルギーを波及させる
                        {
                            for (int jy = sourceY - 4; jy <= sourceY + 4; jy++)
                            {
                                if (jx < STAGE_SIZE_X && jx > 0 && jy < STAGE_SIZE_X && jy > 0)//Grid外に出ていないか
                                {

                                    if (hadoLayer[jx,jy] == null && typeLayer[jx,jy] == CellType.VACANT)//波動がないマスかつ海でも岩でもない
                                    {
                                        double p = myTeamPoint*4 /( 1 * Math.Pow(Math.Sqrt(Math.Pow(jx - sourceX, 2) + Math.Pow(jy - sourceY, 2)), 2.5));//Energy計算
                                        if (team == Team.Red)
                                        {
                                            
                                            blue_energy[jx, jy] -= (int)p;
                                            if (blue_energy[jx, jy] < 0)
                                            {
                                                red_energy[jx, jy] += -blue_energy[jx, jy];
                                                blue_energy[jx, jy] = 0;
                                            }
                                        }
                                        if (team == Team.Blue)
                                        {
                                            red_energy[jx, jy] -= (int)p;
                                            if (red_energy[jx, jy] < 0)
                                            {
                                                blue_energy[jx, jy] += -red_energy[jx, jy];
                                                red_energy[jx, jy] = 0;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    };


                    hadoLayer[ix, iy].setPower(5);

                    switch (hadoLayer[ix, iy].teamColor)
                    {
                        case Team.Red:
                            red_energy[ix, iy] = hadoLayer[ix, iy].powerLevel;
                            f(ix, iy, red_energy[ix, iy], Team.Red);
                            break;
                        case Team.Blue:
                            blue_energy[ix, iy] = hadoLayer[ix, iy].powerLevel;
                            f(ix, iy, blue_energy[ix, iy], Team.Blue);
                            break;
                    }
                }
            }
        }

        for (int ix = 0; ix < STAGE_SIZE_X; ix++)//全マス探索してエネルギーをテキストに反映させる
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                if (red_energy[ix, iy] <= 0 && blue_energy[ix, iy] <= 0)
                {
                    cellTextContents[ix, iy].text = "";
                }
                if (red_energy[ix, iy] > 0)
                {
                    cellTextContents[ix, iy].text = red_energy[ix, iy].ToString();
                    cellTextContents[ix, iy].color = RedMoyoTextColor;
                }
                if (blue_energy[ix, iy] > 0)
                {
                    cellTextContents[ix, iy].text = blue_energy[ix, iy].ToString();
                    cellTextContents[ix, iy].color = BlueMoyoTextColor;
                }
            }
        }
    }



    // 新しい波動石を置いた直後にだけ、既存仕様の「呼吸点が0なら取る」を実行する。
    private void ApplyHadoPlacement(int x, int y)
    {
        HadoCtrl placedHado = hadoLayer[x, y];
        if (placedHado == null)
        {
            RecalculateMoyoAndEnergy();
            return;
        }

        Team placedTeam = placedHado.teamColor;
        Team enemyTeam = placedTeam == Team.Red ? Team.Blue : Team.Red;

        // 敵を先に判定し、その結果を反映してから自軍を判定する。
        RemoveZeroLibertyGroups(enemyTeam);
        RemoveZeroLibertyGroups(placedTeam);
        RecalculateMoyoAndEnergy();
    }

    // 統一領域を辺接続でたどり、その中にある模様領域セル数を返す。
    // 波動石セルは連結の経路にはなるが、地パワーの面積には数えない。
    public int gatherEnergy(int x, int y, Team team, ref bool[,] visited)
    {
        if (!IsInside(x, y) || visited[x, y]) return 0;
        visited[x, y] = true;

        bool isOwnHado = IsOwnHado(x, y, team);
        int groundPower = team == Team.Red ? red_energy[x, y] : blue_energy[x, y];

        // 統一領域は「模様領域の空白」と「自軍波動石」の連結成分である。
        if (!isOwnHado && groundPower <= 0) return 0;

        int total = isOwnHado ? 0 : 1;
        foreach (Vector2Int direction in CardinalDirections)
        {
            total += gatherEnergy(x + direction.x, y + direction.y, team, ref visited);
        }
        return total;
    }

    private void RecalculateMoyoAndEnergy()
    {
        Array.Clear(redMoyoPower, 0, redMoyoPower.Length);
        Array.Clear(blueMoyoPower, 0, blueMoyoPower.Length);
        Array.Clear(red_energy, 0, red_energy.Length);
        Array.Clear(blue_energy, 0, blue_energy.Length);

        List<List<Vector2Int>> redGroups = FindHadoGroups(Team.Red);
        List<List<Vector2Int>> blueGroups = FindHadoGroups(Team.Blue);
        List<EyeRoom> redEyeRooms = FindEyeRooms(Team.Red);
        List<EyeRoom> blueEyeRooms = FindEyeRooms(Team.Blue);
        RecordEyeCells(redEyeRooms, redEyeCells);
        RecordEyeCells(blueEyeRooms, blueEyeCells);

        EmitMoyoForGroups(Team.Red, redGroups, redEyeRooms, redMoyoPower);
        EmitMoyoForGroups(Team.Blue, blueGroups, blueEyeRooms, blueMoyoPower);

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (!IsEmpty(x, y)) continue;

                // 閾値10は、自軍と敵軍の模様パワーの差に適用する。
                double redNetPower = redMoyoPower[x, y] - blueMoyoPower[x, y];
                if (redNetPower >= MoyoRegionThreshold)
                {
                    red_energy[x, y] = Mathf.FloorToInt((float)redNetPower);
                    continue;
                }

                double blueNetPower = -redNetPower;
                if (blueNetPower >= MoyoRegionThreshold)
                {
                    blue_energy[x, y] = Mathf.FloorToInt((float)blueNetPower);
                }
            }
        }

        UpdateMoyoOverlayState(redEyeRooms, blueEyeRooms);
        UpdateEnergyText();
        RecolorGroundItems();
    }

    private void RecordEyeCells(List<EyeRoom> eyeRooms, bool[,] target)
    {
        Array.Clear(target, 0, target.Length);
        foreach (EyeRoom eyeRoom in eyeRooms)
        {
            foreach (Vector2Int cell in eyeRoom.cells)
            {
                if (IsInside(cell.x, cell.y)) target[cell.x, cell.y] = true;
            }
        }
    }

    private void EmitMoyoForGroups(Team team, List<List<Vector2Int>> groups, List<EyeRoom> eyeRooms, double[,] targetPower)
    {
        foreach (List<Vector2Int> group in groups)
        {
            List<Vector2Int> surfaceStarts = new List<Vector2Int>();
            foreach (Vector2Int hadoCell in group)
            {
                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector2Int neighbor = hadoCell + direction;
                    if (IsEmpty(neighbor.x, neighbor.y))
                    {
                        // 同じ空白マスへ向く別の辺も、それぞれ独立した元発信源とする。
                        surfaceStarts.Add(neighbor);
                    }
                }
            }

            int surfaceCount = surfaceStarts.Count;
            if (surfaceCount == 0) continue;

            int eyeCount = GetGroupEyeCount(group, eyeRooms);
            double eyeMultiplier = eyeCount >= 2 ? 2.0 : eyeCount == 1 ? 1.5 : 1.0;

            // 各辺の発出量は、波動石群全体の空白接触辺数で強化する。
            double sourcePower = SurfacePowerUnit * Math.Sqrt(surfaceCount) * eyeMultiplier;
            foreach (Vector2Int sourceStart in surfaceStarts)
            {
                PropagateFromSurface(team, sourceStart, sourcePower, targetPower);
            }
        }
    }

    private void PropagateFromSurface(Team team, Vector2Int sourceStart, double sourcePower, double[,] targetPower)
    {
        int[,] distance = new int[STAGE_SIZE_X, STAGE_SIZE_Y];
        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++) distance[x, y] = -1;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        distance[sourceStart.x, sourceStart.y] = 1;
        queue.Enqueue(sourceStart);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distance[current.x, current.y];

            if (IsEmpty(current.x, current.y))
            {
                targetPower[current.x, current.y] += sourcePower / Math.Pow(currentDistance, DistanceFalloffExponent);
            }

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (!CanTransmitMoyo(next.x, next.y, team) || distance[next.x, next.y] >= 0) continue;

                distance[next.x, next.y] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }
    }

    private List<List<Vector2Int>> FindHadoGroups(Team team)
    {
        bool[,] visited = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (visited[x, y] || !IsOwnHado(x, y, team)) continue;

                List<Vector2Int> group = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                visited[x, y] = true;
                queue.Enqueue(new Vector2Int(x, y));

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    group.Add(current);

                    foreach (Vector2Int direction in CardinalDirections)
                    {
                        Vector2Int next = current + direction;
                        if (!IsInside(next.x, next.y) || visited[next.x, next.y] || !IsOwnHado(next.x, next.y, team)) continue;

                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }

                groups.Add(group);
            }
        }

        return groups;
    }

    private void RemoveZeroLibertyGroups(Team team)
    {
        List<HadoCtrl> hadoToRemove = new List<HadoCtrl>();
        foreach (List<Vector2Int> group in FindHadoGroups(team))
        {
            bool hasLiberty = false;
            foreach (Vector2Int cell in group)
            {
                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector2Int neighbor = cell + direction;
                    if (!IsEmpty(neighbor.x, neighbor.y)) continue;
                    hasLiberty = true;
                    break;
                }
                if (hasLiberty) break;
            }

            if (!hasLiberty)
            {
                foreach (Vector2Int cell in group) hadoToRemove.Add(hadoLayer[cell.x, cell.y]);
            }
        }

        foreach (HadoCtrl hado in hadoToRemove)
        {
            if (hado == null) continue;

            for (int x = 0; x < STAGE_SIZE_X; x++)
            {
                for (int y = 0; y < STAGE_SIZE_Y; y++)
                {
                    if (hadoLayer[x, y] != hado) continue;
                    hadoLayer[x, y] = null;
                    wMgr.deleteHado(hado.teamColor, hado.ID);
                    break;
                }
            }
        }
    }

    private List<EyeRoom> FindEyeRooms(Team team)
    {
        bool[,] visited = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        List<EyeRoom> eyeRooms = new List<EyeRoom>();

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (visited[x, y] || !IsEmpty(x, y)) continue;

                List<Vector2Int> room = FindEmptyRoom(new Vector2Int(x, y), visited);
                if (room.Count > MaxEyeCandidateSize) continue;

                EyeRoom eyeRoom = EvaluateEyeRoom(room, team);
                if (eyeRoom != null) eyeRooms.Add(eyeRoom);
            }
        }

        return eyeRooms;
    }

    private List<Vector2Int> FindEmptyRoom(Vector2Int start, bool[,] visited)
    {
        List<Vector2Int> room = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            room.Add(current);

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (!IsInside(next.x, next.y) || visited[next.x, next.y] || !IsEmpty(next.x, next.y)) continue;
                visited[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        return room;
    }

    private EyeRoom EvaluateEyeRoom(List<Vector2Int> room, Team team)
    {
        bool[,] inRoom = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        bool[,] virtualHado = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        HashSet<Vector2Int> boundaryStoneSet = new HashSet<Vector2Int>();
        HashSet<Vector2Int> diagonalCornerSet = new HashSet<Vector2Int>();
        bool touchesBoardEdge = false;

        foreach (Vector2Int cell in room)
        {
            inRoom[cell.x, cell.y] = true;
            if (cell.x == 0 || cell.y == 0 || cell.x == STAGE_SIZE_X - 1 || cell.y == STAGE_SIZE_Y - 1)
            {
                touchesBoardEdge = true;
            }
        }

        // 自軍波動石・海・岩・盤外だけで囲まれた空白領域を眼候補とする。
        foreach (Vector2Int cell in room)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighbor = cell + direction;
                if (!IsInside(neighbor.x, neighbor.y)) continue; // 盤外は壁
                if (inRoom[neighbor.x, neighbor.y]) continue;

                if (IsOwnHado(neighbor.x, neighbor.y, team))
                {
                    boundaryStoneSet.Add(neighbor);
                    continue;
                }

                if (typeLayer[neighbor.x, neighbor.y] == CellType.OCEAN || typeLayer[neighbor.x, neighbor.y] == CellType.ROCK)
                {
                    continue;
                }

                // 敵波動石を含む境界は自軍に囲まれた空間ではない。
                return null;
            }

            foreach (Vector2Int diagonal in DiagonalDirections)
            {
                Vector2Int corner = cell + diagonal;
                if (!IsInside(corner.x, corner.y) || inRoom[corner.x, corner.y] || !IsEmpty(corner.x, corner.y)) continue;
                diagonalCornerSet.Add(corner);
            }
        }

        if (boundaryStoneSet.Count == 0) return null;

        int weakCornerCount = 0;
        foreach (Vector2Int corner in diagonalCornerSet)
        {
            if (IsNonWeakEmptyCorner(corner, team))
            {
                virtualHado[corner.x, corner.y] = true;
            }
            else
            {
                weakCornerCount++;
            }
        }

        // 盤端の眼候補は、弱点角が1つでもあれば欠け眼である。
        if (touchesBoardEdge && weakCornerCount >= 1) return null;

        int groupCount = CountCorrectedBoundaryGroups(boundaryStoneSet, virtualHado, team);
        int eyeCount = ClassifyEyeCount(room.Count, groupCount);
        if (eyeCount == 0) return null;

        return new EyeRoom(room, new List<Vector2Int>(boundaryStoneSet), eyeCount);
    }

    private bool IsNonWeakEmptyCorner(Vector2Int corner, Team team)
    {
        int ownStoneCount = 0;
        int enemyStoneCount = 0;

        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int neighbor = corner + direction;
            if (!IsInside(neighbor.x, neighbor.y)) continue;

            HadoCtrl hado = hadoLayer[neighbor.x, neighbor.y];
            if (hado == null) continue;
            if (hado.teamColor == team) ownStoneCount++;
            else enemyStoneCount++;
        }

        // 盤外は敵波動石ではない方向として扱う。
        return ownStoneCount >= 3 && enemyStoneCount == 0;
    }

    private int CountCorrectedBoundaryGroups(HashSet<Vector2Int> boundaryStoneSet, bool[,] virtualHado, Team team)
    {
        bool[,] visited = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        int groupCount = 0;

        foreach (Vector2Int boundaryStone in boundaryStoneSet)
        {
            if (visited[boundaryStone.x, boundaryStone.y]) continue;

            groupCount++;
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            visited[boundaryStone.x, boundaryStone.y] = true;
            queue.Enqueue(boundaryStone);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector2Int next = current + direction;
                    if (!IsInside(next.x, next.y) || visited[next.x, next.y]) continue;
                    if (!IsOwnHado(next.x, next.y, team) && !virtualHado[next.x, next.y]) continue;

                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return groupCount;
    }

    private int ClassifyEyeCount(int roomSize, int groupCount)
    {
        switch (roomSize)
        {
            case 1: return groupCount == 1 ? 1 : 0;
            case 2: return groupCount <= 2 ? 1 : 0;
            case 3:
            case 4: return groupCount <= 3 ? 1 : 0;
            case 5: return groupCount <= 3 ? 1 : 0;
            case 6: return groupCount <= 4 ? 1 : 0;
            case 7:
            case 8: return groupCount <= 4 ? 2 : 1;
            default:
                if (roomSize >= 9 && roomSize <= MaxEyeCandidateSize) return groupCount <= 5 ? 2 : 1;
                return 0;
        }
    }

    private int GetGroupEyeCount(List<Vector2Int> group, List<EyeRoom> eyeRooms)
    {
        HashSet<Vector2Int> groupCells = new HashSet<Vector2Int>(group);
        int totalEyeCount = 0;

        foreach (EyeRoom eyeRoom in eyeRooms)
        {
            bool touchesRoom = false;
            foreach (Vector2Int boundaryStone in eyeRoom.boundaryStoneCells)
            {
                if (!groupCells.Contains(boundaryStone)) continue;
                touchesRoom = true;
                break;
            }

            if (touchesRoom) totalEyeCount += eyeRoom.eyeCount;
            if (totalEyeCount >= 2) return 2;
        }

        return totalEyeCount;
    }

    private void UpdateEnergyText()
    {
        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (red_energy[x, y] > 0)
                {
                    cellTextContents[x, y].text = red_energy[x, y].ToString();
                    cellTextContents[x, y].color = RedMoyoTextColor;
                }
                else if (blue_energy[x, y] > 0)
                {
                    cellTextContents[x, y].text = blue_energy[x, y].ToString();
                    cellTextContents[x, y].color = BlueMoyoTextColor;
                }
                else
                {
                    cellTextContents[x, y].text = "";
                }
            }
        }
    }

    // CellText の背面に、統一領域を示す半透明の UI レイヤーを置く。
    // 数値表示や波動石の視認を妨げないよう、通常領域の不透明度は低く保つ。
    private void CreateMoyoOverlay(int x, int y)
    {
        GameObject overlayObject = new GameObject("MoyoOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.layer = cellTexts[x, y].layer;

        RectTransform overlayTransform = overlayObject.GetComponent<RectTransform>();
        overlayTransform.SetParent(ca.transform, false);
        overlayTransform.anchorMin = new Vector2(0.5f, 0.5f);
        overlayTransform.anchorMax = new Vector2(0.5f, 0.5f);
        overlayTransform.pivot = new Vector2(0.5f, 0.5f);
        overlayTransform.position = cellTexts[x, y].transform.position;
        overlayTransform.sizeDelta = new Vector2(1.04f, 1.04f);
        overlayTransform.localScale = Vector3.one;

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = Color.clear;
        // すべての CellText より背面へ送る。セル上の数値は常に読める状態にする。
        overlayTransform.SetAsFirstSibling();
        moyoOverlays[x, y] = overlay;
    }

    // 地パワーを受け取る空白マスと、眼として確定した空白マスを表示用に記録する。
    private void UpdateMoyoOverlayState(List<EyeRoom> redEyeRooms, List<EyeRoom> blueEyeRooms)
    {
        Array.Clear(moyoOverlayTeams, 0, moyoOverlayTeams.Length);
        Array.Clear(eyeOverlayCells, 0, eyeOverlayCells.Length);

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (red_energy[x, y] > 0) moyoOverlayTeams[x, y] = 1;
                else if (blue_energy[x, y] > 0) moyoOverlayTeams[x, y] = 2;
            }
        }

        MarkEyeOverlayCells(redEyeRooms, 1);
        MarkEyeOverlayCells(blueEyeRooms, 2);
        AnimateMoyoOverlays();
    }

    private void MarkEyeOverlayCells(List<EyeRoom> eyeRooms, int teamCode)
    {
        foreach (EyeRoom eyeRoom in eyeRooms)
        {
            foreach (Vector2Int cell in eyeRoom.cells)
            {
                if (!IsInside(cell.x, cell.y)) continue;
                moyoOverlayTeams[cell.x, cell.y] = teamCode;
                eyeOverlayCells[cell.x, cell.y] = true;
            }
        }
    }

    // 波動石と同じく、時間とセル位置に依存した小さな明滅を与える。
    // 透明度だけを変えるため、領域の色が強く点滅する表示にはならない。
    private void AnimateMoyoOverlays()
    {
        if (moyoOverlays == null) return;

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                Image overlay = moyoOverlays[x, y];
                if (overlay == null) continue;

                int teamCode = moyoOverlayTeams[x, y];
                if (teamCode == 0)
                {
                    overlay.color = Color.clear;
                    continue;
                }

                bool isEye = eyeOverlayCells[x, y];
                float pulse = (Mathf.Sin(Time.time * 2.1f + x * 0.71f + y * 1.13f) + 1.0f) * 0.5f;
                float alpha = (isEye ? EyeOverlayBaseAlpha : MoyoOverlayBaseAlpha) +
                              (isEye ? EyeOverlayPulseAlpha : MoyoOverlayPulseAlpha) * pulse;

                Color teamColor = teamCode == 1
                    ? new Color(1.0f, 0.20f, 0.20f, alpha)
                    : new Color(0.15f, 0.50f, 1.0f, alpha);
                overlay.color = teamColor;
            }
        }
    }

    // 実際に統一領域の地パワーを受け取れるキャラクターが移動している間だけ、
    // その統一領域の空白マスからキャラクターへ粒子を流す。
    private void UpdateEnergyFlowVisualization()
    {
        UpdateEnergyFlowParticles();

        if (wMgr == null || wMgr.characters == null) return;

        foreach (Character character in wMgr.characters)
        {
            if (character == null) continue;

            Vector3 currentPosition = character.transform.position;
            bool wasTracked = previousCharacterPositions.TryGetValue(character, out Vector3 previousPosition);
            previousCharacterPositions[character] = currentPosition;

            bool isMoving = wasTracked && (currentPosition - previousPosition).sqrMagnitude > 0.0001f;
            if (!isMoving || !character.isMyTurn || character.hp == null) continue;
            if (!IsInside(character.xCell, character.yCell) || character.hp.preP <= 0) continue;
            if (GetTeamGroundPower(character.xCell, character.yCell, character.teamNum) <= 0) continue;
            if (Time.time < nextEnergyFlowEmissionTime) continue;

            List<Vector2Int> sourceCells = CollectUnifiedGroundCells(character.xCell, character.yCell, character.teamNum);
            if (sourceCells.Count == 0) continue;

            int unifiedPower = 0;
            foreach (Vector2Int sourceCell in sourceCells)
            {
                unifiedPower += GetTeamGroundPower(sourceCell.x, sourceCell.y, character.teamNum);
            }

            // 大きい統一領域では二粒にするが、画面を埋める量にはしない。
            int particleCount = unifiedPower >= 200 ? 2 : 1;
            for (int index = 0; index < particleCount; index++)
            {
                EmitEnergyFlowParticle(character, sourceCells);
            }
            nextEnergyFlowEmissionTime = Time.time + EnergyFlowEmissionInterval;
        }
    }

    // 統一領域の定義どおり、「地パワーを持つ空白」と「自軍波動石」を辺接続で探索する。
    // 描画の発生源として返すのは空白マスだけであり、波動石自体から粒子は出さない。
    private List<Vector2Int> CollectUnifiedGroundCells(int startX, int startY, Team team)
    {
        List<Vector2Int> groundCells = new List<Vector2Int>();
        if (!IsInside(startX, startY)) return groundCells;

        bool[,] visited = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            bool isOwnHado = IsOwnHado(cell.x, cell.y, team);
            int groundPower = GetTeamGroundPower(cell.x, cell.y, team);
            if (!isOwnHado && groundPower <= 0) continue;

            if (!isOwnHado) groundCells.Add(cell);

            foreach (Vector2Int direction in CardinalDirections)
            {
                int nextX = cell.x + direction.x;
                int nextY = cell.y + direction.y;
                if (!IsInside(nextX, nextY) || visited[nextX, nextY]) continue;
                visited[nextX, nextY] = true;
                queue.Enqueue(new Vector2Int(nextX, nextY));
            }
        }

        return groundCells;
    }

    private int GetTeamGroundPower(int x, int y, Team team)
    {
        if (!IsInside(x, y)) return 0;
        return team == Team.Red ? red_energy[x, y] : blue_energy[x, y];
    }

    private void EmitEnergyFlowParticle(Character character, List<Vector2Int> sourceCells)
    {
        EnergyFlowParticle particle = GetReusableEnergyFlowParticle();
        if (particle == null) return;

        Vector2Int sourceCell = sourceCells[UnityEngine.Random.Range(0, sourceCells.Count)];
        // 自分のいるマスだけを発生源に選び続けないよう、可能なら二マス以上離れた場所を選ぶ。
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2Int candidate = sourceCells[UnityEngine.Random.Range(0, sourceCells.Count)];
            float distanceSquared = (candidate.x - character.transform.position.x) * (candidate.x - character.transform.position.x) +
                                    (candidate.y - character.transform.position.y) * (candidate.y - character.transform.position.y);
            if (distanceSquared >= 2.25f)
            {
                sourceCell = candidate;
                break;
            }
        }

        Vector3 source = new Vector3(sourceCell.x, sourceCell.y, 0.0f);
        if ((source - character.transform.position).sqrMagnitude < 0.25f)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.34f;
            source += new Vector3(offset.x, offset.y, 0.0f);
        }

        particle.source = source;
        particle.destination = character.transform.position + new Vector3(0.0f, 0.15f, 0.0f);
        particle.color = character.teamNum == Team.Red
            ? new Color(1.0f, 0.34f, 0.22f, 0.88f)
            : new Color(0.20f, 0.70f, 1.0f, 0.92f);
        particle.startedAt = Time.time;
        particle.duration = EnergyFlowDuration;
        particle.active = true;
        particle.rectTransform.gameObject.SetActive(true);
        particle.rectTransform.position = source;
        particle.image.color = particle.color;
    }

    private EnergyFlowParticle GetReusableEnergyFlowParticle()
    {
        foreach (EnergyFlowParticle particle in energyFlowParticles)
        {
            if (!particle.active) return particle;
        }

        if (energyFlowParticles.Count >= MaximumEnergyFlowParticles) return null;

        GameObject particleObject = new GameObject("EnergyFlowParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        particleObject.layer = cellTexts[0, 0].layer;
        RectTransform particleTransform = particleObject.GetComponent<RectTransform>();
        particleTransform.SetParent(ca.transform, false);
        particleTransform.anchorMin = new Vector2(0.5f, 0.5f);
        particleTransform.anchorMax = new Vector2(0.5f, 0.5f);
        particleTransform.pivot = new Vector2(0.5f, 0.5f);
        particleTransform.sizeDelta = new Vector2(0.16f, 0.16f);
        particleTransform.localScale = Vector3.one;
        particleTransform.SetAsLastSibling();

        Image image = particleObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        particleObject.SetActive(false);

        EnergyFlowParticle newParticle = new EnergyFlowParticle(particleTransform, image);
        energyFlowParticles.Add(newParticle);
        return newParticle;
    }

    private void UpdateEnergyFlowParticles()
    {
        foreach (EnergyFlowParticle particle in energyFlowParticles)
        {
            if (!particle.active) continue;

            float progress = (Time.time - particle.startedAt) / particle.duration;
            if (progress >= 1.0f)
            {
                particle.active = false;
                particle.rectTransform.gameObject.SetActive(false);
                continue;
            }

            // 終点へ近づくほど速くなり、わずかに弧を描く。進行方向が明確になるようにする。
            float easedProgress = 1.0f - Mathf.Pow(1.0f - progress, 2.0f);
            Vector3 travel = particle.destination - particle.source;
            Vector3 side = new Vector3(-travel.y, travel.x, 0.0f).normalized;
            float curve = Mathf.Sin(progress * Mathf.PI) * 0.12f;
            particle.rectTransform.position = Vector3.Lerp(particle.source, particle.destination, easedProgress) + side * curve;
            particle.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(0.16f, 0.05f, progress);
            particle.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, progress * 180.0f);

            Color color = particle.color;
            color.a *= 1.0f - progress;
            particle.image.color = color;
        }
    }

    /// <summary>
    /// 赤は上端、青は下端の中央3海マスに置く。地形は海のまま維持する。
    /// </summary>
    private void CreateGate(Team team)
    {
        int gateY = team == Team.Red ? STAGE_SIZE_Y - 1 : 0;
        int centerX = STAGE_SIZE_X / 2;

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            gateTeams[x, gateY] = team;

            GameObject gateObject = new GameObject("GateLight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gateObject.layer = cellTexts[x, gateY].layer;
            RectTransform gateTransform = gateObject.GetComponent<RectTransform>();
            gateTransform.SetParent(ca.transform, false);
            gateTransform.anchorMin = new Vector2(0.5f, 0.5f);
            gateTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gateTransform.pivot = new Vector2(0.5f, 0.5f);
            gateTransform.position = cellTexts[x, gateY].transform.position;
            gateTransform.sizeDelta = new Vector2(0.90f, 0.90f);
            gateTransform.localScale = Vector3.one;
            gateTransform.SetAsLastSibling();

            Image image = gateObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = GateColor(team, 0.80f);
            gateImageTeams.Add(image, team);
        }
    }

    // 結界は常に明るく、波動珠の累計投入数が増えるほど少しだけ強く脈動する。
    private void AnimateGates()
    {
        foreach (KeyValuePair<Image, Team> gateImage in gateImageTeams)
        {
            if (gateImage.Key == null) continue;
            int deposited = gateImage.Value == Team.Red ? bluePearlsDeposited : redPearlsDeposited;
            float pulse = (Mathf.Sin(Time.time * 3.2f + gateImage.Key.transform.position.x) + 1.0f) * 0.5f;
            float alpha = 0.62f + pulse * (0.20f + deposited * 0.02f);
            gateImage.Key.color = GateColor(gateImage.Value, alpha);
        }
    }

    private Color GateColor(Team team, float alpha)
    {
        return team == Team.Red
            ? new Color(1.0f, 0.28f, 0.18f, alpha)
            : new Color(0.18f, 0.62f, 1.0f, alpha);
    }

    public bool IsGameOver => gameOver;

    /// <summary>
    /// 各プレイヤーのターン終了後に、条件を満たすマスだけへ波動片を独立5%で湧かせる。
    /// </summary>
    public void HandleTurnEnded()
    {
        if (gameOver) return;

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (!CanSpawnFragmentAt(x, y)) continue;
                if (UnityEngine.Random.value >= FragmentSpawnChance) continue;

                BoardItemData fragment = new BoardItemData(BoardItemType.Fragment, GetDominantGroundTeam(x, y));
                groundItems[x, y].Add(fragment);
                RefreshItemVisual(x, y);
            }
        }
    }

    private bool CanSpawnFragmentAt(int x, int y)
    {
        return IsInside(x, y) && typeLayer[x, y] == CellType.VACANT &&
               hadoLayer[x, y] == null && playerLayer[x, y] == null &&
               !gateTeams[x, y].HasValue && groundItems[x, y].Count == 0;
    }

    /// <summary>
    /// 地面に落ちた波動片・波動珠は、そのセルが所属する模様領域へ直ちに再染色される。
    /// 中立地では無色のまま残す。
    /// </summary>
    private void RecolorGroundItems()
    {
        if (groundItems == null) return;

        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                Team? groundTeam = GetDominantGroundTeam(x, y);
                foreach (BoardItemData item in groundItems[x, y])
                {
                    if (item.type == BoardItemType.Fragment || item.type == BoardItemType.Pearl)
                    {
                        item.team = groundTeam;
                    }
                }
                RefreshItemVisual(x, y);
            }
        }
    }

    private Team? GetDominantGroundTeam(int x, int y)
    {
        // Unityのスクリプト再コンパイル直後は、HUDのUpdateが盤面配列の生成より先に
        // 実行されるフレームがある。その間は中立地として扱う。
        if (red_energy == null || blue_energy == null || !IsInside(x, y)) return null;
        if (red_energy[x, y] > 0) return Team.Red;
        if (blue_energy[x, y] > 0) return Team.Blue;
        return null;
    }

    // 右側HUD用。模様領域の陣営だけを返し、波動石や海・岩そのものは中立として扱う。
    public Team? GetMoyoTeamAt(int x, int y)
    {
        return GetDominantGroundTeam(x, y);
    }

    public bool IsInsideCell(int x, int y)
    {
        return IsInside(x, y);
    }

    public float GetMovementCost(int targetX, int targetY, Character character)
    {
        Team? groundTeam = GetDominantGroundTeam(targetX, targetY);
        if (groundTeam.HasValue && groundTeam.Value == character.teamNum) return 1.0f;

        float itemWeight = character.InventoryWeight;
        float baseCost = groundTeam.HasValue ? 2.0f : 1.0f;
        return baseCost + itemWeight * 0.2f;
    }

    // 通常移動の到着時に、足元の盤上アイテムを一つだけ自動で拾う。
    // Ctrlを押した移動では拾わずに通過し、Aで開く取捨メニューから選択する。
    public void HandleWalkFinished(Character character, bool shouldAutoPickUp)
    {
        if (gameOver || character == null || character.hp == null || character.hp.preP <= 0) return;
        if (shouldAutoPickUp) TryPickUpOneItem(character);
    }

    /// <summary>
    /// 歩行したキャラクターのターン終了時に一度だけ実行する。
    /// 自軍統一領域では回復し、敵軍統一領域では同じ統一領域の地パワー総和を受ける。
    /// 中立地では HP を変えない。
    /// </summary>
    public void ApplyEndOfTurnGroundEffect(Character character)
    {
        if (gameOver || character == null || character.hp == null || character.hp.preP <= 0) return;

        int ownPower = GetUnifiedGroundPower(character.xCell, character.yCell, character.teamNum);
        if (ownPower > 0)
        {
            character.hp.change(ownPower);
            return;
        }

        Team enemyTeam = character.teamNum == Team.Red ? Team.Blue : Team.Red;
        int enemyPower = GetUnifiedGroundPower(character.xCell, character.yCell, enemyTeam);
        if (enemyPower > 0) character.hp.change(-enemyPower);
    }

    /// <summary>
    /// 地パワー = 統一領域内の全模様領域セル数 × 10。
    /// その統一領域に有効な眼が一つ以上含まれる場合だけ、固定で100を加算する。
    /// </summary>
    private int GetUnifiedGroundPower(int x, int y, Team team)
    {
        if (red_energy == null || blue_energy == null || redEyeCells == null || blueEyeCells == null || !IsInside(x, y)) return 0;
        bool[,] visited = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
        int moyoCellCount = gatherEnergy(x, y, team, ref visited);
        if (moyoCellCount == 0) return 0;

        bool[,] teamEyeCells = team == Team.Red ? redEyeCells : blueEyeCells;
        bool hasEye = false;
        for (int scanX = 0; scanX < STAGE_SIZE_X && !hasEye; scanX++)
        {
            for (int scanY = 0; scanY < STAGE_SIZE_Y; scanY++)
            {
                if (visited[scanX, scanY] && teamEyeCells[scanX, scanY])
                {
                    hasEye = true;
                    break;
                }
            }
        }

        return moyoCellCount * 10 + (hasEye ? 100 : 0);
    }

    /// <summary>
    /// 指定セルで、指定陣営のキャラクターが行動終了時に受けるHP増減予定値。
    /// 自軍統一領域は正、敵軍統一領域は負、中立地は0を返す。
    /// </summary>
    public int GetEndOfTurnHpDelta(int x, int y, Team characterTeam)
    {
        int ownPower = GetUnifiedGroundPower(x, y, characterTeam);
        if (ownPower > 0) return ownPower;

        Team enemyTeam = characterTeam == Team.Red ? Team.Blue : Team.Red;
        return -GetUnifiedGroundPower(x, y, enemyTeam);
    }

    // 歩き終えたセルでは、空き枠があれば一品だけを自動で拾う。
    // 波動片は自軍色のものだけを取得できる。地面の色が変われば再染色され、
    // その時点から対応する陣営が取得できる。
    private void TryPickUpOneItem(Character character)
    {
        int x = character.xCell;
        int y = character.yCell;
        if (!IsInside(x, y) || groundItems[x, y].Count == 0 || !character.HasFreeInventorySlot) return;

        int pickupIndex = -1;
        for (int index = 0; index < groundItems[x, y].Count; index++)
        {
            BoardItemData candidate = groundItems[x, y][index];
            bool isOtherTeamFragment = candidate.type == BoardItemType.Fragment && candidate.team != character.teamNum;
            if (isOtherTeamFragment) continue;
            pickupIndex = index;
            break;
        }
        if (pickupIndex < 0) return;

        BoardItemData item = groundItems[x, y][pickupIndex];
        groundItems[x, y].RemoveAt(pickupIndex);
        character.AddItem(item);
        RefreshItemVisual(x, y);
        TryOpenFragmentConversionPrompt(character, false);
    }

    /// <summary>
    /// A から開くアイテム行動メニュー、およびその下位メニューの入力を処理する。
    /// true を返したフレームは、キャラクター側で移動や他アクションを実行しない。
    /// </summary>
    public bool HandleItemInteractionInput(Character character)
    {
        if (character == null || character.IsMoving) return itemInteractionMode != ItemInteractionMode.None;

        if (itemInteractionMode == ItemInteractionMode.None)
        {
            // 20個に到達した時だけ、珠への変換確認を一度自動表示する。
            if (TryOpenFragmentConversionPrompt(character, false)) return true;

            // A は常にアイテム行動の入口。左移動は矢印キーで行える。
            bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(KeyCode.A) && !controlHeld)
            {
                OpenItemActionMenu(character);
                return true;
            }
            return false;
        }

        if (itemInteractionCharacter != character)
        {
            CancelItemInteraction(null);
            return false;
        }

        // B は選択・照準のどの段階でも取消。
        if (Input.GetKeyDown(KeyCode.B))
        {
            CancelItemInteraction(character);
            return true;
        }

        if (itemInteractionMode == ItemInteractionMode.ThrowTargetSelect)
        {
            MoveThrowCursor(character);
            if (Input.GetKeyDown(KeyCode.Z)) ConfirmThrowTarget(character);
            RefreshItemMenuVisual();
            return true;
        }

        if (itemInteractionMode == ItemInteractionMode.ActionSelect)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveItemActionCursor(-1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveItemActionCursor(1);
            if (Input.GetKeyDown(KeyCode.Z)) ConfirmItemActionSelection(character);
            RefreshItemMenuVisual();
            return true;
        }

        if (itemInteractionMode == ItemInteractionMode.ConvertFragmentsConfirm)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                itemMenuCursor = 1 - itemMenuCursor;
            }
            if (Input.GetKeyDown(KeyCode.Z)) ConfirmFragmentConversion(character);
            RefreshItemMenuVisual();
            return true;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveItemMenuCursor(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveItemMenuCursor(1);

        if (itemInteractionMode == ItemInteractionMode.TakeAndDrop)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) AdjustSelectedItemCount(character, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) AdjustSelectedItemCount(character, 1);
            if (Input.GetKeyDown(KeyCode.Z)) ConfirmTakeAndDrop(character);
        }
        else if (itemInteractionMode == ItemInteractionMode.ThrowItemSelect && Input.GetKeyDown(KeyCode.Z))
        {
            ConfirmThrowItemSelection(character);
        }
        else if (itemInteractionMode == ItemInteractionMode.UseItemSelect && Input.GetKeyDown(KeyCode.Z))
        {
            ConfirmItemUse(character);
        }

        RefreshItemMenuVisual();
        return true;
    }

    public void CancelItemInteraction(Character character)
    {
        if (itemInteractionMode == ItemInteractionMode.None) return;
        if (character != null && itemInteractionCharacter != character) return;

        // 珠化確認をBで閉じた場合は「いいえ」と同じ扱いにし、直後に再表示しない。
        if (itemInteractionMode == ItemInteractionMode.ConvertFragmentsConfirm && itemInteractionCharacter != null &&
            !suppressConversionDismissOnClose)
        {
            conversionPromptDismissed.Add(itemInteractionCharacter);
        }

        itemInteractionMode = ItemInteractionMode.None;
        itemInteractionCharacter = null;
        itemMenuEntries.Clear();
        selectedThrowItem = null;
        itemMenuStatusMessage = null;
        suppressConversionDismissOnClose = false;
        if (itemMenuObject != null) itemMenuObject.SetActive(false);
        ClearThrowRangeIndicators();
    }

    private bool HasGroundItemsAt(int x, int y)
    {
        return IsInside(x, y) && groundItems != null && groundItems[x, y].Count > 0;
    }

    /// <summary>
    /// A で最初に開く、取捨・投擲・使用の選択メニュー。
    /// </summary>
    private void OpenItemActionMenu(Character character)
    {
        itemInteractionMode = ItemInteractionMode.ActionSelect;
        itemInteractionCharacter = character;
        itemActionCursor = 0;
        itemMenuCursor = 0;
        itemMenuEntries.Clear();
        itemMenuStatusMessage = null;
        EnsureItemMenuUi();
        RefreshItemMenuVisual();
    }

    private void MoveItemActionCursor(int direction)
    {
        itemActionCursor = (itemActionCursor + direction + 3) % 3;
        itemMenuStatusMessage = null;
    }

    private void ConfirmItemActionSelection(Character character)
    {
        itemMenuStatusMessage = null;
        if (itemActionCursor == 0)
        {
            OpenTakeAndDropMenu(character);
            return;
        }
        if (itemActionCursor == 1)
        {
            if (!OpenThrowItemMenu(character)) itemMenuStatusMessage = "投擲できる所持アイテムがありません。";
            return;
        }
        if (!OpenUseItemMenu(character)) itemMenuStatusMessage = "使用できるアイテムがありません。";
    }

    private void OpenTakeAndDropMenu(Character character)
    {
        itemInteractionMode = ItemInteractionMode.TakeAndDrop;
        itemInteractionCharacter = character;
        itemMenuCursor = 0;
        itemMenuEntries.Clear();

        foreach (BoardItemData item in groundItems[character.xCell, character.yCell])
        {
            AddOrIncreaseItemMenuEntry(true, item.type, item.team, 1);
        }
        AddInventoryMenuEntries(character);
        EnsureItemMenuUi();
        RefreshItemMenuVisual();
    }

    private bool OpenThrowItemMenu(Character character)
    {
        itemMenuEntries.Clear();
        AddInventoryMenuEntries(character);
        if (itemMenuEntries.Count == 0) return false;

        itemInteractionMode = ItemInteractionMode.ThrowItemSelect;
        itemInteractionCharacter = character;
        itemMenuCursor = 0;
        EnsureItemMenuUi();
        RefreshItemMenuVisual();
        return true;
    }

    private bool OpenUseItemMenu(Character character)
    {
        itemMenuEntries.Clear();
        // 現在の使用アイテムは剥離魔法符だけだが、メニュー構造は他の使用物にも拡張できる。
        AddInventoryMenuEntry(character, BoardItemType.PeelScroll, null);
        if (itemMenuEntries.Count == 0) return false;

        itemInteractionMode = ItemInteractionMode.UseItemSelect;
        itemInteractionCharacter = character;
        itemMenuCursor = 0;
        EnsureItemMenuUi();
        RefreshItemMenuVisual();
        return true;
    }

    private void AddInventoryMenuEntries(Character character)
    {
        AddInventoryMenuEntry(character, BoardItemType.Fragment, Team.Red);
        AddInventoryMenuEntry(character, BoardItemType.Fragment, Team.Blue);
        AddInventoryMenuEntry(character, BoardItemType.Fragment, null);
        AddInventoryMenuEntry(character, BoardItemType.Pearl, Team.Red);
        AddInventoryMenuEntry(character, BoardItemType.Pearl, Team.Blue);
        AddInventoryMenuEntry(character, BoardItemType.Pearl, null);
        AddInventoryMenuEntry(character, BoardItemType.PeelScroll, null);
    }

    private void AddInventoryMenuEntry(Character character, BoardItemType type, Team? team)
    {
        int count = character.CountItemsExact(type, team);
        if (count > 0) AddOrIncreaseItemMenuEntry(false, type, team, count);
    }

    private void AddOrIncreaseItemMenuEntry(bool isGroundItem, BoardItemType type, Team? team, int count)
    {
        ItemMenuEntry entry = itemMenuEntries.Find(value =>
            value.isGroundItem == isGroundItem && value.type == type && value.team == team);
        if (entry == null)
        {
            entry = new ItemMenuEntry
            {
                isGroundItem = isGroundItem,
                type = type,
                team = team
            };
            itemMenuEntries.Add(entry);
        }
        entry.availableCount += count;
    }

    private void MoveItemMenuCursor(int direction)
    {
        if (itemMenuEntries.Count == 0) return;
        itemMenuCursor = (itemMenuCursor + direction + itemMenuEntries.Count) % itemMenuEntries.Count;
    }

    private void AdjustSelectedItemCount(Character character, int direction)
    {
        if (itemMenuCursor < 0 || itemMenuCursor >= itemMenuEntries.Count) return;
        ItemMenuEntry entry = itemMenuEntries[itemMenuCursor];

        // 敵色・中立色の波動片は、従来ルール通り拾えない。
        if (entry.isGroundItem && entry.type == BoardItemType.Fragment && entry.team != character.teamNum) return;
        entry.selectedCount = Mathf.Clamp(entry.selectedCount + direction, 0, entry.availableCount);
    }

    private void ConfirmTakeAndDrop(Character character)
    {
        int x = character.xCell;
        int y = character.yCell;

        // 先に捨てる処理を行い、拾得のための所持枠を空ける。
        foreach (ItemMenuEntry entry in itemMenuEntries)
        {
            if (entry.isGroundItem || entry.selectedCount <= 0) continue;
            for (int count = 0; count < entry.selectedCount; count++)
            {
                BoardItemData dropped = character.RemoveFirstItemExact(entry.type, entry.team);
                if (dropped != null) groundItems[x, y].Add(dropped);
            }
        }

        foreach (ItemMenuEntry entry in itemMenuEntries)
        {
            if (!entry.isGroundItem || entry.selectedCount <= 0) continue;
            if (entry.type == BoardItemType.Fragment && entry.team != character.teamNum) continue;

            for (int count = 0; count < entry.selectedCount && character.HasFreeInventorySlot; count++)
            {
                BoardItemData item = RemoveFirstGroundItemExact(x, y, entry.type, entry.team);
                if (item == null || !character.AddItem(item))
                {
                    if (item != null) groundItems[x, y].Add(item);
                    break;
                }
            }
        }

        // 足元へ捨てられた波動片・珠は、その地点の模様色へ再染色される。
        RecolorGroundItems();
        RefreshItemVisual(x, y);
        CancelItemInteraction(character);
        TryOpenFragmentConversionPrompt(character, false);
    }

    private BoardItemData RemoveFirstGroundItemExact(int x, int y, BoardItemType type, Team? team)
    {
        List<BoardItemData> items = groundItems[x, y];
        for (int index = 0; index < items.Count; index++)
        {
            BoardItemData item = items[index];
            if (item.type != type || item.team != team) continue;
            items.RemoveAt(index);
            return item;
        }
        return null;
    }

    private void ConfirmThrowItemSelection(Character character)
    {
        if (itemMenuCursor < 0 || itemMenuCursor >= itemMenuEntries.Count) return;
        ItemMenuEntry entry = itemMenuEntries[itemMenuCursor];
        if (entry.isGroundItem || entry.availableCount <= 0) return;

        // 一度の投擲で選べるのは一品目の一個だけ。
        selectedThrowItem = new BoardItemData(entry.type, entry.team);
        // 照準はキャラクターの向きに連動させない。初期位置は常に右隣の一マスにする。
        throwTargetCell = new Vector2Int(character.xCell + 1, character.yCell);

        // 右隣が盤外なら、他の隣接セルを初期位置にする。
        if (!IsInside(throwTargetCell.x, throwTargetCell.y))
        {
            throwTargetCell = new Vector2Int(character.xCell - 1, character.yCell);
            if (!IsInside(throwTargetCell.x, throwTargetCell.y))
            {
                throwTargetCell = new Vector2Int(character.xCell, character.yCell + 1);
            }
        }

        itemInteractionMode = ItemInteractionMode.ThrowTargetSelect;
        DrawThrowRangeIndicators(character);
    }

    private void ConfirmItemUse(Character character)
    {
        if (itemMenuCursor < 0 || itemMenuCursor >= itemMenuEntries.Count) return;
        ItemMenuEntry entry = itemMenuEntries[itemMenuCursor];
        if (entry.type != BoardItemType.PeelScroll || entry.availableCount <= 0) return;

        if (TryUsePeelScroll(character))
        {
            CancelItemInteraction(character);
        }
        else
        {
            itemMenuStatusMessage = "この場所では使用できません。";
        }
    }

    private int GetThrowRange(BoardItemData item)
    {
        return item != null && item.type == BoardItemType.Pearl ? 1 : 6;
    }

    private void MoveThrowCursor(Character character)
    {
        int moveX = 0;
        int moveY = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) moveX = -1;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) moveX = 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) moveY = -1;
        else if (Input.GetKeyDown(KeyCode.UpArrow)) moveY = 1;
        else return;

        Vector2Int candidate = throwTargetCell + new Vector2Int(moveX, moveY);
        int range = GetThrowRange(selectedThrowItem);
        int distance = Mathf.Max(Mathf.Abs(candidate.x - character.xCell), Mathf.Abs(candidate.y - character.yCell));
        if (!IsInside(candidate.x, candidate.y) || distance > range) return;

        throwTargetCell = candidate;
        UpdateThrowCursorIndicator(character);
    }

    private void ConfirmThrowTarget(Character character)
    {
        if (!IsValidThrowTarget(character, throwTargetCell)) return;

        BoardItemData item = character.RemoveFirstItemExact(selectedThrowItem.type, selectedThrowItem.team);
        if (item == null)
        {
            CancelItemInteraction(character);
            return;
        }

        Team? targetGate = gateTeams[throwTargetCell.x, throwTargetCell.y];
        bool isEnemyGateDeposit = targetGate.HasValue && item.type == BoardItemType.Pearl &&
                                  item.team == character.teamNum && targetGate.Value != character.teamNum;
        if (isEnemyGateDeposit)
        {
            AwardPearlDeposit(character);
        }
        else
        {
            // 同じセルへ何個でも重ねて落とせる。
            groundItems[throwTargetCell.x, throwTargetCell.y].Add(item);
            RecolorGroundItems();
            RefreshItemVisual(throwTargetCell.x, throwTargetCell.y);
        }

        CancelItemInteraction(character);
    }

    private bool IsValidThrowTarget(Character character, Vector2Int target)
    {
        if (selectedThrowItem == null || !IsInside(target.x, target.y)) return false;
        int range = GetThrowRange(selectedThrowItem);
        int distance = Mathf.Max(Mathf.Abs(target.x - character.xCell), Mathf.Abs(target.y - character.yCell));
        if (distance == 0 || distance > range) return false;

        Team? targetGate = gateTeams[target.x, target.y];
        if (targetGate.HasValue)
        {
            return selectedThrowItem.type == BoardItemType.Pearl &&
                   selectedThrowItem.team == character.teamNum && targetGate.Value != character.teamNum;
        }

        return typeLayer[target.x, target.y] == CellType.VACANT;
    }

    private void DrawThrowRangeIndicators(Character character)
    {
        ClearThrowRangeIndicators();
        if (selectedThrowItem == null) return;

        int range = GetThrowRange(selectedThrowItem);
        Color rangeColor = character.teamNum == Team.Red
            ? new Color(1.0f, 0.31f, 0.25f, 0.12f)
            : new Color(0.22f, 0.64f, 1.0f, 0.12f);
        for (int x = character.xCell - range; x <= character.xCell + range; x++)
        {
            for (int y = character.yCell - range; y <= character.yCell + range; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (Mathf.Max(Mathf.Abs(x - character.xCell), Mathf.Abs(y - character.yCell)) == 0) continue;
                if (!IsValidThrowTarget(character, cell)) continue;
                throwRangeIndicators.Add(CreateWorldCellMarker(cell, rangeColor, "ThrowRange"));
            }
        }

        throwCursorIndicator = CreateWorldCellMarker(throwTargetCell, new Color(1.0f, 0.92f, 0.28f, 0.62f), "ThrowCursor");
        UpdateThrowCursorIndicator(character);
    }

    private Image CreateWorldCellMarker(Vector2Int cell, Color color, string objectName)
    {
        GameObject markerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        markerObject.layer = cellTexts[cell.x, cell.y].layer;
        RectTransform markerTransform = markerObject.GetComponent<RectTransform>();
        markerTransform.SetParent(ca.transform, false);
        markerTransform.anchorMin = new Vector2(0.5f, 0.5f);
        markerTransform.anchorMax = new Vector2(0.5f, 0.5f);
        markerTransform.pivot = new Vector2(0.5f, 0.5f);
        markerTransform.position = cellTexts[cell.x, cell.y].transform.position;
        markerTransform.sizeDelta = new Vector2(0.88f, 0.88f);
        markerTransform.localScale = Vector3.one;
        markerTransform.SetAsLastSibling();

        Image image = markerObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;
        Outline outline = markerObject.GetComponent<Outline>();
        outline.effectColor = new Color(color.r, color.g, color.b, Mathf.Min(0.9f, color.a + 0.25f));
        outline.effectDistance = new Vector2(0.025f, 0.025f);
        return image;
    }

    private void UpdateThrowCursorIndicator(Character character)
    {
        if (throwCursorIndicator == null || !IsInside(throwTargetCell.x, throwTargetCell.y)) return;
        throwCursorIndicator.rectTransform.position = cellTexts[throwTargetCell.x, throwTargetCell.y].transform.position;
        bool valid = IsValidThrowTarget(character, throwTargetCell);
        throwCursorIndicator.color = valid
            ? new Color(1.0f, 0.92f, 0.28f, 0.62f)
            : new Color(1.0f, 0.18f, 0.18f, 0.55f);
    }

    private void ClearThrowRangeIndicators()
    {
        foreach (Image marker in throwRangeIndicators)
        {
            if (marker != null) Destroy(marker.gameObject);
        }
        throwRangeIndicators.Clear();

        if (throwCursorIndicator != null) Destroy(throwCursorIndicator.gameObject);
        throwCursorIndicator = null;
    }

    private void EnsureItemMenuUi()
    {
        if (itemMenuObject != null)
        {
            itemMenuObject.SetActive(true);
            return;
        }

        GameObject menuObject = new GameObject("ItemInteractionMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        menuObject.transform.SetParent(wMgr.canvas.transform, false);
        RectTransform menuTransform = menuObject.GetComponent<RectTransform>();
        menuTransform.anchorMin = new Vector2(0.5f, 0.5f);
        menuTransform.anchorMax = new Vector2(0.5f, 0.5f);
        menuTransform.pivot = new Vector2(0.0f, 0.0f);
        menuTransform.sizeDelta = new Vector2(560.0f, 460.0f);

        Image background = menuObject.GetComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.075f, 0.94f);
        background.raycastTarget = false;
        Outline outline = menuObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.85f, 1.0f, 0.62f);
        outline.effectDistance = new Vector2(2.0f, -2.0f);

        // 文字色だけに頼らず、選択行そのものを帯で強調する。
        GameObject cursorObject = new GameObject("SelectionHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cursorObject.transform.SetParent(menuObject.transform, false);
        RectTransform cursorTransform = cursorObject.GetComponent<RectTransform>();
        cursorTransform.anchorMin = new Vector2(0.0f, 1.0f);
        cursorTransform.anchorMax = new Vector2(1.0f, 1.0f);
        cursorTransform.pivot = new Vector2(0.0f, 1.0f);
        cursorTransform.anchoredPosition = new Vector2(8.0f, -10.0f);
        cursorTransform.sizeDelta = new Vector2(-16.0f, 36.0f);
        itemMenuCursorHighlight = cursorObject.GetComponent<Image>();
        itemMenuCursorHighlight.color = new Color(0.98f, 0.66f, 0.10f, 0.38f);
        itemMenuCursorHighlight.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(menuObject.transform, false);
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0.0f, 0.0f);
        textTransform.anchorMax = new Vector2(1.0f, 1.0f);
        textTransform.offsetMin = new Vector2(14.0f, 12.0f);
        textTransform.offsetMax = new Vector2(-14.0f, -12.0f);

        itemMenuFont = CreateJapaneseItemMenuFont();
        itemMenuText = textObject.GetComponent<TextMeshProUGUI>();
        itemMenuText.font = itemMenuFont;
        itemMenuText.fontSize = 28;
        itemMenuText.fontStyle = FontStyles.Normal;
        itemMenuText.alignment = TextAlignmentOptions.TopLeft;
        itemMenuText.color = new Color(0.94f, 0.96f, 1.0f);
        itemMenuText.enableWordWrapping = true;
        itemMenuText.overflowMode = TextOverflowModes.Overflow;
        // 選択帯の34px間隔と一致させ、どの行を選んでいるかをずらさず示す。
        itemMenuText.lineSpacing = 1.2f;
        itemMenuText.richText = true;
        itemMenuText.extraPadding = true;
        itemMenuText.raycastTarget = false;
        itemMenuObject = menuObject;

        // 珠化確認の選択肢だけは本文から分離する。
        // これにより、選択帯と「はい／いいえ」の位置を同じ矩形で厳密に揃えられる。
        conversionYesText = CreateConversionChoiceText(menuObject.transform, "ConvertYes", -82.0f);
        conversionNoText = CreateConversionChoiceText(menuObject.transform, "ConvertNo", -124.0f);
        conversionYesText.gameObject.SetActive(false);
        conversionNoText.gameObject.SetActive(false);
    }

    private void RefreshItemMenuVisual()
    {
        if (itemMenuObject == null || itemInteractionCharacter == null) return;
        itemMenuObject.SetActive(true);
        UpdateItemMenuPosition();
        SetConversionChoiceVisibility(false);

        string text = "";
        int cursorLine = -1;
        if (itemInteractionMode == ItemInteractionMode.ActionSelect)
        {
            SetItemMenuSize(500.0f, 290.0f);
            text += "<b>アイテム行動</b>\n\n";
            text += FormatActionMenuRow(0, "取る／捨てる", true) + "\n";
            text += FormatActionMenuRow(1, "投擲（1個）", itemInteractionCharacter.InventoryCount > 0) + "\n";
            text += FormatActionMenuRow(2, "アイテム使用", itemInteractionCharacter.CountItems(BoardItemType.PeelScroll, null) > 0) + "\n";
            text += "\nZ: 選択    B: 閉じる";
            cursorLine = 2 + itemActionCursor;
        }
        else if (itemInteractionMode == ItemInteractionMode.TakeAndDrop)
        {
            SetItemMenuSize(620.0f, 520.0f);
            text += "<b>落ちているアイテム</b>\n";
            int line = 1;
            bool hasGroundItems = false;
            foreach (ItemMenuEntry entry in itemMenuEntries)
            {
                if (!entry.isGroundItem) continue;
                text += FormatItemMenuRow(entry, "拾う数") + "\n";
                if (itemMenuEntries.IndexOf(entry) == itemMenuCursor) cursorLine = line;
                line++;
                hasGroundItems = true;
            }
            if (!hasGroundItems)
            {
                text += "  （なし）\n";
                line++;
            }

            text += "\n<b>所持アイテム</b>\n";
            line += 2;
            bool hasInventoryItems = false;
            foreach (ItemMenuEntry entry in itemMenuEntries)
            {
                if (entry.isGroundItem) continue;
                text += FormatItemMenuRow(entry, "捨てる数") + "\n";
                if (itemMenuEntries.IndexOf(entry) == itemMenuCursor) cursorLine = line;
                line++;
                hasInventoryItems = true;
            }
            if (!hasInventoryItems)
            {
                text += "  （なし）\n";
            }
            text += "\nZ: 確定    B: 取消";
        }
        else if (itemInteractionMode == ItemInteractionMode.ThrowItemSelect)
        {
            SetItemMenuSize(520.0f, 390.0f);
            text += "<b>投擲するアイテム</b>\n";
            for (int index = 0; index < itemMenuEntries.Count; index++)
            {
                ItemMenuEntry entry = itemMenuEntries[index];
                text += FormatSingleItemRow(entry, index == itemMenuCursor) + "\n";
                if (index == itemMenuCursor) cursorLine = 1 + index;
            }
            text += "\n一度に投げられるのは1個です。\nZ: 照準選択    B: 取消";
        }
        else if (itemInteractionMode == ItemInteractionMode.UseItemSelect)
        {
            SetItemMenuSize(520.0f, 300.0f);
            text += "<b>使用するアイテム</b>\n";
            for (int index = 0; index < itemMenuEntries.Count; index++)
            {
                ItemMenuEntry entry = itemMenuEntries[index];
                text += FormatSingleItemRow(entry, index == itemMenuCursor) + "\n";
                if (index == itemMenuCursor) cursorLine = 1 + index;
            }
            text += "\n剥離魔法符は足元中心の3×3へ使います。\nZ: 使用    B: 取消";
        }
        else if (itemInteractionMode == ItemInteractionMode.ConvertFragmentsConfirm)
        {
            SetItemMenuSize(520.0f, 280.0f);
            text += "<b>波動珠への変換</b>\n";
            text += "自軍波動片を20個使い、波動珠にしますか？\n\n\n\n\n";
            text += "↑↓: 選択    Z: 決定    B: 取消";
            SetConversionChoiceVisibility(true);
            conversionYesText.text = itemMenuCursor == 0 ? "> はい" : "  はい";
            conversionNoText.text = itemMenuCursor == 1 ? "> いいえ" : "  いいえ";
            conversionYesText.color = itemMenuCursor == 0 ? new Color(1.0f, 0.96f, 0.74f) : new Color(0.94f, 0.96f, 1.0f);
            conversionNoText.color = itemMenuCursor == 1 ? new Color(1.0f, 0.96f, 0.74f) : new Color(0.94f, 0.96f, 1.0f);
        }
        else if (itemInteractionMode == ItemInteractionMode.ThrowTargetSelect)
        {
            SetItemMenuSize(500.0f, 290.0f);
            text += "<b>投擲先を選択</b>\n" + GetItemLabel(selectedThrowItem.type, selectedThrowItem.team) + " ×1\n";
            text += "対象: (" + throwTargetCell.x + ", " + throwTargetCell.y + ")\n";
            text += IsValidThrowTarget(itemInteractionCharacter, throwTargetCell)
                ? "<color=#6FF3A6>投擲可能</color>"
                : "<color=#FF8080>このセルには投げられません</color>";
            text += "\n\n矢印: 照準移動\nZ: 投擲確定    B: 取消";
        }

        if (!string.IsNullOrEmpty(itemMenuStatusMessage)) text += "\n<color=#FF9A9A>" + itemMenuStatusMessage + "</color>";
        itemMenuText.text = text;
        if (itemInteractionMode == ItemInteractionMode.ConvertFragmentsConfirm)
        {
            SetConversionChoiceHighlight(itemMenuCursor);
        }
        else
        {
            SetItemMenuCursorHighlight(cursorLine);
        }
    }

    private string FormatActionMenuRow(int index, string label, bool available)
    {
        bool selected = index == itemActionCursor;
        string color = available ? "#FFF6BC" : "#8E98A8";
        string prefix = selected ? "<b><color=" + color + ">> " : "<color=" + color + ">  ";
        string suffix = selected ? "</color></b>" : "</color>";
        return prefix + label + suffix;
    }

    private static string FormatSingleItemRow(ItemMenuEntry entry, bool selected)
    {
        string prefix = selected ? "<b><color=#FFF6BC>> " : "  ";
        string suffix = selected ? "</color></b>" : "";
        return prefix + GetItemLabel(entry.type, entry.team) + " ×" + entry.availableCount + suffix;
    }

    private string FormatItemMenuRow(ItemMenuEntry entry, string quantityLabel)
    {
        int index = itemMenuEntries.IndexOf(entry);
        bool selected = index == itemMenuCursor;
        string prefix = selected ? "<b><color=#FFF6BC>> " : "  ";
        string suffix = selected ? "</color></b>" : "";
        bool blockedPickup = entry.isGroundItem && entry.type == BoardItemType.Fragment && entry.team != itemInteractionCharacter.teamNum;
        string quantity = blockedPickup ? "拾えない" : quantityLabel + " " + entry.selectedCount;
        return prefix + GetItemLabel(entry.type, entry.team) + " ×" + entry.availableCount + "　" + quantity + suffix;
    }

    private void SetItemMenuSize(float width, float height)
    {
        if (itemMenuObject == null) return;
        itemMenuObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
    }

    private void SetItemMenuCursorHighlight(int lineIndex)
    {
        if (itemMenuCursorHighlight == null) return;
        bool visible = lineIndex >= 0;
        itemMenuCursorHighlight.gameObject.SetActive(visible);
        if (!visible) return;

        RectTransform highlightTransform = itemMenuCursorHighlight.rectTransform;
        highlightTransform.anchoredPosition = new Vector2(8.0f, -10.0f - 34.0f * lineIndex);
        highlightTransform.sizeDelta = new Vector2(-16.0f, 34.0f);
        highlightTransform.SetAsFirstSibling();
    }

    private TextMeshProUGUI CreateConversionChoiceText(Transform parent, string objectName, float topOffset)
    {
        GameObject choiceObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        choiceObject.transform.SetParent(parent, false);
        RectTransform choiceTransform = choiceObject.GetComponent<RectTransform>();
        choiceTransform.anchorMin = new Vector2(0.0f, 1.0f);
        choiceTransform.anchorMax = new Vector2(1.0f, 1.0f);
        choiceTransform.pivot = new Vector2(0.0f, 1.0f);
        choiceTransform.anchoredPosition = new Vector2(22.0f, topOffset);
        choiceTransform.sizeDelta = new Vector2(-44.0f, 36.0f);

        TextMeshProUGUI choiceText = choiceObject.GetComponent<TextMeshProUGUI>();
        choiceText.font = itemMenuFont;
        choiceText.fontSize = 28;
        choiceText.fontStyle = FontStyles.Bold;
        choiceText.alignment = TextAlignmentOptions.MidlineLeft;
        choiceText.enableWordWrapping = false;
        choiceText.overflowMode = TextOverflowModes.Overflow;
        choiceText.extraPadding = true;
        choiceText.raycastTarget = false;
        return choiceText;
    }

    private void SetConversionChoiceVisibility(bool visible)
    {
        if (conversionYesText != null) conversionYesText.gameObject.SetActive(visible);
        if (conversionNoText != null) conversionNoText.gameObject.SetActive(visible);
    }

    private void SetConversionChoiceHighlight(int selectedChoice)
    {
        if (itemMenuCursorHighlight == null) return;
        itemMenuCursorHighlight.gameObject.SetActive(true);
        RectTransform highlightTransform = itemMenuCursorHighlight.rectTransform;
        highlightTransform.anchoredPosition = new Vector2(8.0f, selectedChoice == 0 ? -80.0f : -122.0f);
        highlightTransform.sizeDelta = new Vector2(-16.0f, 40.0f);
        // 選択帯は本文より前、選択肢テキストより後ろに置く。
        highlightTransform.SetAsFirstSibling();
    }

    private void UpdateItemMenuPosition()
    {
        Camera camera = Camera.main;
        if (camera == null) camera = wMgr.camera;
        if (camera == null) return;

        Vector3 position = itemInteractionCharacter.transform.position + new Vector3(0.9f, 0.35f, 0.0f);
        Vector2 screenPosition = camera.WorldToScreenPoint(position);
        RectTransform canvasTransform = wMgr.canvas.transform as RectTransform;
        RectTransform menuTransform = itemMenuObject.GetComponent<RectTransform>();
        Vector2 localPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, screenPosition, null, out localPosition))
        {
            menuTransform.anchoredPosition = localPosition;
        }
    }

    private static string GetItemLabel(BoardItemType type, Team? team)
    {
        string name = type == BoardItemType.Fragment ? "波動片" :
                      type == BoardItemType.Pearl ? "波動珠" : "剥離魔法符";
        if (type == BoardItemType.PeelScroll) return name;
        if (!team.HasValue) return name + "（無色）";
        return name + (team.Value == Team.Red ? "（赤）" : "（青）");
    }

    private static TMP_FontAsset CreateJapaneseItemMenuFont()
    {
        // HUDと同じく、プロジェクトに同梱した日本語フォントをSDFの元フォントとして使う。
        // 実行PCのOSフォントに依存せず、日本語を動的アトラスへ追加できる。
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

    /// <summary>
    /// 自軍色の波動片を20個所持した時の確認画面を開く。
    /// 「いいえ」を選んだ後は、所持数が20未満になるまで再表示しない。
    /// </summary>
    public bool TryOpenFragmentConversionPrompt(Character character, bool forceOpen)
    {
        if (character == null || character.IsMoving || itemInteractionMode != ItemInteractionMode.None) return false;

        int fragmentCount = character.CountItems(BoardItemType.Fragment, character.teamNum);
        if (fragmentCount < FragmentPerPearl)
        {
            conversionPromptDismissed.Remove(character);
            return false;
        }
        if (!forceOpen && conversionPromptDismissed.Contains(character)) return false;

        itemInteractionMode = ItemInteractionMode.ConvertFragmentsConfirm;
        itemInteractionCharacter = character;
        itemMenuCursor = 0; // 0 = はい、1 = いいえ
        itemMenuEntries.Clear();
        itemMenuStatusMessage = null;
        EnsureItemMenuUi();
        RefreshItemMenuVisual();
        return true;
    }

    private void ConfirmFragmentConversion(Character character)
    {
        if (itemMenuCursor == 0)
        {
            TryConvertFragments(character);
            conversionPromptDismissed.Remove(character);
            suppressConversionDismissOnClose = true;
        }
        else
        {
            conversionPromptDismissed.Add(character);
        }

        CancelItemInteraction(character);
        // 40個以上を持っていた場合は、1個目を作った後も次の20個分を確認できる。
        TryOpenFragmentConversionPrompt(character, false);
    }

    public bool TryConvertFragments(Character character)
    {
        if (character == null || character.IsMoving) return false;
        if (character.CountItems(BoardItemType.Fragment, character.teamNum) < FragmentPerPearl) return false;

        character.RemoveItems(BoardItemType.Fragment, character.teamNum, FragmentPerPearl);
        character.AddItem(new BoardItemData(BoardItemType.Pearl, character.teamNum));
        return true;
    }

    /// <summary>
    /// 敵色の波動片・波動珠を自軍領域へ落として、再染色するための任意行動。
    /// 敵色を優先し、無ければ任意の再染色対象アイテムを一個落とす。
    /// </summary>
    public bool TryDropColorableItem(Character character)
    {
        if (character == null || character.IsMoving || !IsInside(character.xCell, character.yCell)) return false;

        BoardItemData item = character.RemoveFirstColorableItemForDrop();
        if (item == null) return false;
        groundItems[character.xCell, character.yCell].Add(item);
        RecolorGroundItems();
        return true;
    }

    public bool TryDepositPearl(Character character)
    {
        if (gameOver || character == null || character.IsMoving) return false;
        Team targetGate = character.teamNum == Team.Red ? Team.Blue : Team.Red;
        if (!IsAtGateAccessCell(character.xCell, character.yCell, targetGate)) return false;

        BoardItemData pearl = character.RemoveFirstItem(BoardItemType.Pearl, character.teamNum);
        if (pearl == null) return false;

        AwardPearlDeposit(character);
        return true;
    }

    // 歩いて結界へ投入した場合と、珠を結界へ投擲した場合で共通の報酬・勝利処理。
    private void AwardPearlDeposit(Character character)
    {
        // 波動珠を一枠外し、同じ一枠を魔法符で置換するので満杯でも投入できる。
        character.AddItem(new BoardItemData(BoardItemType.PeelScroll));
        if (character.teamNum == Team.Red) redPearlsDeposited++;
        else bluePearlsDeposited++;

        int deposited = character.teamNum == Team.Red ? redPearlsDeposited : bluePearlsDeposited;
        if (deposited >= PearlsRequiredForVictory)
        {
            StartCoroutine(PlayVictory(character.teamNum));
        }
    }

    private bool IsAtGateAccessCell(int x, int y, Team gateTeam)
    {
        int gateY = gateTeam == Team.Red ? STAGE_SIZE_Y - 1 : 0;
        int accessY = gateTeam == Team.Red ? gateY - 1 : gateY + 1;
        int centerX = STAGE_SIZE_X / 2;
        return y == accessY && x >= centerX - 2 && x <= centerX + 2;
    }

    // HUDに「敵結界へ投入可能」と表示するための副作用のない判定。
    public bool CanDepositPearl(Character character)
    {
        if (gameOver || character == null || character.IsMoving) return false;
        Team targetGate = character.teamNum == Team.Red ? Team.Blue : Team.Red;
        return IsAtGateAccessCell(character.xCell, character.yCell, targetGate) &&
               character.CountItems(BoardItemType.Pearl, character.teamNum) > 0;
    }

    public int GetDepositedPearls(Team team)
    {
        return team == Team.Red ? redPearlsDeposited : bluePearlsDeposited;
    }

    public int PearlsNeededForVictory => PearlsRequiredForVictory;

    /// <summary>
    /// 剥離魔法符は足元を中心とする3×3へ作用する。
    /// 波動石と盤上アイテムだけを消し、海・岩などの地形、キャラクター、結界は変更しない。
    /// </summary>
    public bool TryUsePeelScroll(Character character)
    {
        if (gameOver || character == null || character.IsMoving) return false;
        BoardItemData scroll = character.RemoveFirstItem(BoardItemType.PeelScroll, null);
        if (scroll == null) return false;

        List<HadoCtrl> hadoToRemove = new List<HadoCtrl>();
        for (int x = character.xCell - 1; x <= character.xCell + 1; x++)
        {
            for (int y = character.yCell - 1; y <= character.yCell + 1; y++)
            {
                if (!IsInside(x, y)) continue;
                // 海・岩（結界が置かれた海を含む）は剥離範囲から除外する。
                if (typeLayer[x, y] != CellType.VACANT) continue;
                groundItems[x, y].Clear();
                RefreshItemVisual(x, y);

                if (hadoLayer[x, y] != null) hadoToRemove.Add(hadoLayer[x, y]);
            }
        }

        foreach (HadoCtrl hado in hadoToRemove)
        {
            RemoveHado(hado);
        }
        RecalculateMoyoAndEnergy();
        return true;
    }

    public void DropAllItemsAt(Character character, int x, int y)
    {
        if (character == null || !IsInside(x, y)) return;
        foreach (BoardItemData item in character.RemoveAllItems())
        {
            groundItems[x, y].Add(item);
        }
        RecolorGroundItems();
    }

    public void RespawnCharacter(Character character)
    {
        Vector2Int spawn = FindRespawnCell(character);
        character.xCell = spawn.x;
        character.yCell = spawn.y;
        character.transform.position = new Vector3(spawn.x, spawn.y, 0.0f);
        playerLayer[spawn.x, spawn.y] = character;
    }

    public void RemoveCharacterFromBoard(Character character)
    {
        if (character == null || !IsInside(character.xCell, character.yCell)) return;
        if (playerLayer[character.xCell, character.yCell] == character)
        {
            playerLayer[character.xCell, character.yCell] = null;
        }
    }

    private Vector2Int FindRespawnCell(Character character)
    {
        Vector2Int origin = new Vector2Int(character.spawnX, character.spawnY);
        if (CanRespawnAt(origin.x, origin.y, character)) return origin;

        for (int radius = 1; radius < Mathf.Max(STAGE_SIZE_X, STAGE_SIZE_Y); radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                    int x = origin.x + dx;
                    int y = origin.y + dy;
                    if (CanRespawnAt(x, y, character)) return new Vector2Int(x, y);
                }
            }
        }
        return origin;
    }

    private bool CanRespawnAt(int x, int y, Character character)
    {
        return IsInside(x, y) && typeLayer[x, y] == CellType.VACANT &&
               playerLayer[x, y] == null &&
               (hadoLayer[x, y] == null || IsOwnHado(x, y, character.teamNum));
    }

    private void RemoveHado(HadoCtrl hado)
    {
        if (hado == null) return;
        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                if (hadoLayer[x, y] != hado) continue;
                hadoLayer[x, y] = null;
                wMgr.deleteHado(hado.teamColor, hado.ID);
                return;
            }
        }
    }

    private IEnumerator PlayVictory(Team winningTeam)
    {
        if (gameOver) yield break;
        gameOver = true;

        Team losingTeam = winningTeam == Team.Red ? Team.Blue : Team.Red;
        List<HadoCtrl> enemyHado = new List<HadoCtrl>();
        for (int x = 0; x < STAGE_SIZE_X; x++)
        {
            for (int y = 0; y < STAGE_SIZE_Y; y++)
            {
                HadoCtrl hado = hadoLayer[x, y];
                if (hado != null && hado.teamColor == losingTeam) enemyHado.Add(hado);
            }
        }

        // 巨大波動弾の結果として、敵色の波動石だけを短い間隔で剥離していく。
        foreach (HadoCtrl hado in enemyHado)
        {
            RemoveHado(hado);
            yield return new WaitForSeconds(0.045f);
        }
        RecalculateMoyoAndEnergy();
        Debug.Log((winningTeam == Team.Red ? "Red" : "Blue") + " wins by gigantic Hado bullet.");
    }

    private void RefreshItemVisual(int x, int y)
    {
        if (!IsInside(x, y)) return;
        List<BoardItemData> items = groundItems[x, y];
        Image icon = itemIcons[x, y];

        if (items.Count == 0)
        {
            if (icon != null) icon.gameObject.SetActive(false);
            return;
        }

        if (icon == null)
        {
            GameObject iconObject = new GameObject("BoardItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = cellTexts[x, y].layer;
            RectTransform iconTransform = iconObject.GetComponent<RectTransform>();
            iconTransform.SetParent(ca.transform, false);
            iconTransform.anchorMin = new Vector2(0.5f, 0.5f);
            iconTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconTransform.pivot = new Vector2(0.5f, 0.5f);
            iconTransform.position = cellTexts[x, y].transform.position + new Vector3(0.0f, 0.20f, 0.0f);
            iconTransform.localScale = Vector3.one;
            iconTransform.SetAsLastSibling();
            icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            itemIcons[x, y] = icon;
        }

        BoardItemData displayItem = items[0];
        icon.gameObject.SetActive(true);
        RectTransform transform = icon.rectTransform;
        switch (displayItem.type)
        {
            case BoardItemType.Fragment:
                transform.sizeDelta = new Vector2(0.18f, 0.18f);
                icon.color = ItemColor(displayItem.team, 0.96f);
                break;
            case BoardItemType.Pearl:
                transform.sizeDelta = new Vector2(0.34f, 0.34f);
                icon.color = ItemColor(displayItem.team, 1.0f);
                break;
            default:
                transform.sizeDelta = new Vector2(0.28f, 0.28f);
                icon.color = new Color(1.0f, 0.88f, 0.30f, 1.0f);
                break;
        }
    }

    private Color ItemColor(Team? team, float alpha)
    {
        if (!team.HasValue) return new Color(0.92f, 0.92f, 0.92f, alpha);
        return team.Value == Team.Red
            ? new Color(1.0f, 0.22f, 0.20f, alpha)
            : new Color(0.18f, 0.55f, 1.0f, alpha);
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < STAGE_SIZE_X && y >= 0 && y < STAGE_SIZE_Y;
    }

    private bool IsEmpty(int x, int y)
    {
        return IsInside(x, y) && hadoLayer[x, y] == null && typeLayer[x, y] == CellType.VACANT;
    }

    private bool IsOwnHado(int x, int y, Team team)
    {
        return IsInside(x, y) && hadoLayer[x, y] != null && hadoLayer[x, y].teamColor == team;
    }

    private bool CanTransmitMoyo(int x, int y, Team team)
    {
        // 自軍波動石は透過する。敵波動石、海、岩、盤外は遮断する。
        return IsInside(x, y) && typeLayer[x, y] == CellType.VACANT &&
               (hadoLayer[x, y] == null || hadoLayer[x, y].teamColor == team);
    }



    public bool isPlayerAndCellTypeVacant(int _xCell, int _yCell)//歩行可能判定
    {
        if (_xCell>=0&_xCell<STAGE_SIZE_X & _yCell>=0 & _yCell < STAGE_SIZE_Y)
        {
            if (playerLayer[_xCell, _yCell] == null & typeLayer[_xCell, _yCell] == CellType.VACANT) return true;
        }
        return false;
    }


    public bool isPlayerVacant(int _xCell, int _yCell)//キャラクタがいるか判定
    {
        if (_xCell >= 0 & _xCell < STAGE_SIZE_X & _yCell >= 0 & _yCell < STAGE_SIZE_Y)
        {
            if (playerLayer[_xCell, _yCell] == null) return true;
        }
        return false;
    }

    public bool isCellTypeVacant(int _xCell, int _yCell)//キャラクタがいるか判定
    {
        if (_xCell >= 0 & _xCell < STAGE_SIZE_X & _yCell >= 0 & _yCell < STAGE_SIZE_Y)
        {
            if (typeLayer[_xCell, _yCell] == CellType.VACANT) return true;
        }
        return false;
    }




    public bool isHadoVacant(int _xCell, int _yCell)
    {
        if (_xCell >= 0 & _xCell < STAGE_SIZE_X & _yCell >= 0 & _yCell < STAGE_SIZE_Y)
        {
            if (hadoLayer[_xCell, _yCell] == null & typeLayer[_xCell, _yCell] == CellType.VACANT) return true;
        }
        return false;
    }

    public Character getCharacter(int _xCell, int _yCell)
    {
        if (_xCell >= 0 & _xCell < STAGE_SIZE_X & _yCell >= 0 & _yCell < STAGE_SIZE_Y)
        {
            if (playerLayer[_xCell, _yCell] != null) return playerLayer[_xCell, _yCell];
        }
        return null;
    }


    public int getEnergy(int _xCell, int _yCell, Team t, bool isMyEnergy)
    {
        if (isMyEnergy)
        {
            switch (t)
            {
                case Team.Red: return red_energy[_xCell, _yCell];
                case Team.Blue: return blue_energy[_xCell, _yCell];
            }
        }
        else
        {
            switch (t) {
                case Team.Red: return blue_energy[_xCell, _yCell];
                case Team.Blue: return red_energy[_xCell, _yCell];
            }
        }
        return -1;
    }

}
