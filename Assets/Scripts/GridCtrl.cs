using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public enum CellType : int
{
    VACANT = 0,
    OCEAN = 1,
    ROCK = 2
}

public class GridCtrl : MonoBehaviour
{

    //public List<List<GameObject>> cells;


    public TextMeshProUGUI textMeshPro;

    public WorkerMgr wMgr;
    public int STAGE_SIZE_X = 23;
    public int STAGE_SIZE_Y = 23;
    
    public Character[,] playerLayer;
    public HadoCtrl[,] hadoLayer;
    public CellType[,] typeLayer;

    public Canvas ca;

    public GameObject[,] cellTexts;
    public TextMeshProUGUI[,] cellTextContents;

    public int[,] red_energy;
    public int[,] blue_energy;

    // Start is called before the first frame update
    

    private void Awake()//盤面自体の生成
    {
        transform.position = new Vector2(STAGE_SIZE_X / 2, STAGE_SIZE_Y / 2);

        playerLayer = new Character[STAGE_SIZE_X, STAGE_SIZE_Y];


        hadoLayer = new HadoCtrl[STAGE_SIZE_X, STAGE_SIZE_Y];
        typeLayer = new CellType[STAGE_SIZE_X, STAGE_SIZE_Y];

        ca = GetComponentInChildren<Canvas>();
        cellTexts = new GameObject[STAGE_SIZE_X, STAGE_SIZE_Y];
        cellTextContents = new TextMeshProUGUI[STAGE_SIZE_X, STAGE_SIZE_Y];

        red_energy = new int [STAGE_SIZE_X, STAGE_SIZE_Y];
        blue_energy = new int[STAGE_SIZE_X, STAGE_SIZE_Y];




        for (int ix = 0; ix < STAGE_SIZE_X; ix++)
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                typeLayer[ix, iy] = CellType.VACANT;//全マスをVACANTにセット


                cellTexts[ix, iy] = Instantiate(wMgr.CellTextPrefab, new Vector2(ix, iy), Quaternion.identity);//全マスにCellTextを設置
                cellTexts[ix, iy].transform.parent = transform;//波及波動を記すcellTextをGridの子ヒエラルキーにする。
                cellTexts[ix, iy].transform.parent = ca.transform;
                cellTextContents[ix, iy] = cellTexts[ix, iy].GetComponent<TextMeshProUGUI>();



                cellTextContents[ix, iy].text = "";
                cellTextContents[ix, iy].rectTransform.sizeDelta = new Vector2(1.2f, 0.2f);
                cellTextContents[ix, iy].fontSize = 0.5f;
                cellTextContents[ix, iy].alignment = TextAlignmentOptions.Center;
                cellTextContents[ix, iy].color = Color.black;
            }
        }

        for (int i = 0; i< STAGE_SIZE_X; i++)//試しに海をセット
        {
            typeLayer[i, 0] = CellType.OCEAN;
            typeLayer[0, i] = CellType.OCEAN;
            typeLayer[i, STAGE_SIZE_Y -1] = CellType.OCEAN;
            typeLayer[STAGE_SIZE_X - 1, i] = CellType.OCEAN;
        }

        int donutSize = 1;
        for (int ix = ((STAGE_SIZE_X-1)/2) - donutSize; ix < ((STAGE_SIZE_X - 1) / 2) + donutSize+1; ix++)
        {
            for (int iy = ((STAGE_SIZE_Y - 1) / 2) - donutSize; iy < ((STAGE_SIZE_Y - 1) / 2) + donutSize+1; iy++)
            {
                typeLayer[ix, iy] = CellType.OCEAN;//全マスをVACANTにセット
            }
        }


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
        giveHadoPower(_xCell, _yCell);//波動パワーの判定を行う。
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
                    

                    Action<int, int, int, Team> f = (ix, iy, myTeamPoint, team) =>
                    {

                        for (int jx = ix - 4; jx <= ix + 4; jx++)//根源波動の座標から+=5以内にエネルギーを波及させる
                        {
                            for (int jy = iy - 4; jy <= iy + 4; jy++)
                            {
                                if (jx < STAGE_SIZE_X && jx > 0 && jy < STAGE_SIZE_X && jy > 0)//Grid外に出ていないか
                                {

                                    if (hadoLayer[jx,jy] == null && typeLayer[jx,jy] == CellType.VACANT)//波動がないマスかつ海でも岩でもない
                                    {
                                        double p = myTeamPoint /( 3 * Math.Pow(Math.Sqrt(Math.Pow(jx - ix, 2) + Math.Pow(jy - iy, 2)), 2.5));//Energy計算
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


                    hadoLayer[ix, iy].setPower(60);

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
                    cellTextContents[ix, iy].color = Color.red;
                }
                if (blue_energy[ix, iy] > 0)
                {
                    cellTextContents[ix, iy].text = blue_energy[ix, iy].ToString();
                    cellTextContents[ix, iy].color = Color.blue;
                }
            }
        }
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

}
