using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Networking.Types;

public class Character : MonoBehaviour
{
    public GameObject myBody;
    public WorkerMgr wMgr;
    public Animator anim;

    public AudioSource beatSound;

    public bool isMyTurn = false;

    bool isMoving = false;

    public int ResidueHado = 1;

    public int id;
    public Team teamNum;

    public int xCell;
    public int yCell;

    public int dx;
    public int dy;

    public int deadCount;
    public int deadCountMax;
    public bool ghost;//一度死んだか


    public int attackPower;

    public PointMeter hp;
    public PointMeter stamina;


    //character()//継承されているためコンストラクタをここに作ると面倒なことが起きる？
    //{

    //}


    public void setCharacter(int x, int y, Team t, WorkerMgr w, GameObject _myBody)
    {
        wMgr = w;
        myBody = _myBody;
        xCell = x;
        yCell = y;
        transform.position = new Vector3(x, y, 0);
        dx = 1;
        dy = 1;
        teamNum = t;
        beatSound = GetComponent<AudioSource>();
        //wMgr.gridCtrl.PutCharacter(this);//初期配置

        hp = this.gameObject.AddComponent<PointMeter>();
        stamina = this.gameObject.AddComponent<PointMeter>();

        hp.setPointMeter(this, "HP", 100);
        stamina.setPointMeter(this, "力", 20);

        deadCountMax = 3; deadCount = 0;//死んだ時に何ターンで復活するか
        ghost = false;
    }



    public void walk()
    {
        if (isMoving == false && ((Input.GetKey(KeyCode.RightShift)) == false && (Input.GetKey(KeyCode.LeftShift)) == false) && (Input.GetAxisRaw("Horizontal") != 0 | Input.GetAxisRaw("Vertical") != 0))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            dx = (int)x;
            dy = (int)y;
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);

            if (wMgr.gridCtrl.isPlayerAndCellTypeVacant(xCell + dx, yCell + dy) && stamina.preP > 0)//移動したいマスが空白で、スタミナが0でなければ
            {
                wMgr.gridCtrl.moveCharacter(xCell, yCell, xCell + dx, yCell + dy);
                xCell = xCell + dx;
                yCell = yCell + dy;

                StartCoroutine(Move(new Vector2(x, y)));//移動状態の映像、開始。
                stamina.change(-1);
            }
        }
    }

    public void specialWalk()//Vacant以外の地形の場所も歩ける歩行
    {
        if (isMoving == false && ((Input.GetKey(KeyCode.RightShift)) == false && (Input.GetKey(KeyCode.LeftShift)) == false) && (Input.GetAxisRaw("Horizontal") != 0 | Input.GetAxisRaw("Vertical") != 0))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            dx = (int)x;
            dy = (int)y;
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);

            if (wMgr.gridCtrl.isPlayerVacant(xCell + dx, yCell + dy) && stamina.preP > 0)//移動したいマスが空白で、スタミナが0でなければ
            {
                wMgr.gridCtrl.moveCharacter(xCell, yCell, xCell + dx, yCell + dy);
                xCell = xCell + dx;
                yCell = yCell + dy;

                StartCoroutine(Move(new Vector2(x, y)));//移動状態の映像、開始。
                stamina.change(-1);
            }
        }
    }


    public void changeDirection()
    {
        if (isMoving == false & (Input.GetKey(KeyCode.RightShift)) || (Input.GetKey(KeyCode.LeftShift)) & (Input.GetAxisRaw("Horizontal") != 0 | Input.GetAxisRaw("Vertical") != 0))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            dx = (int)x;
            dy = (int)y;
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);
        }
    }




    public void jump(int x, int y)
    {
    }

    IEnumerator Move(Vector3 direction)
    {
        isMoving = true;

        Vector3 targetPos = transform.position + direction;
        //現在とターゲットの場所が違ったら近づけ続ける。
        while ((targetPos - transform.position).sqrMagnitude > Mathf.Epsilon)
        {
            //近づける(MoveTowardは、「1現在地, 2目標地点, 3速度」で目標に向かって移動するという関数)
            transform.position = Vector3.MoveTowards(transform.position, targetPos, 5f * Time.deltaTime);
            yield return null;//1フレーム分待つという意味。
        }
        transform.position = targetPos;
        isMoving = false; 
    }




    public void installPower()
    {
        if (Input.GetKeyDown("z") & ResidueHado > 0)
        {

            ResidueHado = ResidueHado - wMgr.createHado(teamNum, xCell, yCell);//設置に成功すると1が返ってくる
        }
        
    }

    public bool attack()
    {
        if(Input.GetKeyDown("c") & ResidueHado > 0)
        {
            if(wMgr.gridCtrl.isPlayerVacant(xCell + dx, yCell + dy) == false)//対象地に誰かいる場合
            {
                Debug.Log(string.Format("攻撃。x:", xCell + dx , "y,", yCell + dy));

                beatSound.Play();
                beatSound.volume = 0.3f;
                beatSound.time = 0.6f;
                wMgr.gridCtrl.getCharacter(xCell + dx, yCell + dy).hp.change(-attackPower);
                return true;
            }
        }
        return false;
    }


    public void startTurn()//ターンが始まるときにwMgrから呼ばれる。
    {
        if(isMyTurn)return;//既に自分のターンである場合、何も変えることはない。
        
        ResidueHado = 1;
        isMyTurn = true;
        if(deadCount == deadCountMax)
        {
            hp.change(hp.maxP);
        }

    }

    public void endTurn()//歩き終わったときなど、これを呼ぶ。turnNextを呼んで、対象を次のキャラに変えてもらう。
    {
        isMyTurn = false;
        stamina.change(stamina.naturalRecovery);
        wMgr.turnNext();
    }


    public void generalUpdate()//アップデート（これ）を各々のキャラクターのコントローラのUpdateから呼んでもらう。
    {
        if (isMyTurn)
        {
            bool isTurnEnd = false;
            changeDirection();

            if (hp.preP == 0)//死んでいるキャラはスルー
            {
                //本来、ここで死者復活ターン１加算を表示する。
                if (Input.GetKeyDown("x")) isTurnEnd = true;
                if (isTurnEnd)
                {
                    Debug.Log("死者のターン。復活まであと" + (deadCountMax - deadCount - 1) + "ターンになった");
                    deadCount += 1;
                    endTurn();
                }
            }
            else if (deadCount == deadCountMax)//復活ターンの特別行動
            {
                specialWalk();
                installPower();
                isTurnEnd = attack();
                if (Input.GetKeyDown("x")) isTurnEnd = true;
                if (isTurnEnd)
                {
                    deadCount = 0;
                    stamina.change(stamina.naturalRecovery);//スタミナ自然回復
                    if (wMgr.gridCtrl.isCellTypeVacant(xCell, yCell) == false)//通常マスに戻っていなければ死
                    {
                        Debug.Log("死者復活ターン終了" + wMgr.gridCtrl.isCellTypeVacant(xCell, yCell));
                        death();
                    }
                    Debug.Log("endTurnを呼んだ。");
                    endTurn();
                }

            }
            else{
                walk();
                installPower();
                isTurnEnd = attack();
                if (Input.GetKeyDown("x")) isTurnEnd = true;
                if (isTurnEnd)
                {
                    stamina.preP = stamina.preP + stamina.naturalRecovery;//スタミナ自然回復
                    Debug.Log("endTurnを呼んだ。");
                    endTurn();
                }
            }
        }
    }

    public void death()//このキャラ自身が死んだ時,死体登録地点へ移動させ、復活待機状態にさせる。
    {
        hp.preP = 0;
        ghost = true;
        deadCount = 0;
        //myBody.SetActive(false);
        int x=0, y=0;
        int xStageSize = wMgr.gridCtrl.STAGE_SIZE_X - 1;
        int yStageSize = wMgr.gridCtrl.STAGE_SIZE_Y - 1;

        bool[] isVacant = { true, true, true, true };
        int placeNum = 0;
        int deadCharacterNum = 0;
        foreach (var c in wMgr.characters)
        {
            if(c.hp.preP == 0)//死体数カウントの増加と死体地点（受入不能地点）登録
            {
                deadCharacterNum++;
                if (c.xCell == 0 && c.yCell == 0) isVacant[0] = false;
                if (c.xCell == 0 && c.yCell == yStageSize) isVacant[1] = false;
                if (c.xCell == xStageSize && c.yCell == 0) isVacant[2] = false;
                if (c.xCell == xStageSize && c.yCell == yStageSize) isVacant[3] = false;
            }
        }
        

        placeNum = Random.Range(0, 4 - deadCharacterNum);//死体輸送先の受入数が分かったので、受入地点番号を乱数で得る。

        Debug.Log("死体受入地点状況 [0]" + isVacant[0] + "  [1]" + isVacant[1] + "  [2]" + isVacant[2] + "  [3]" + isVacant[3] + "     死体輸送希望地:" + placeNum);

        for (int b=0; b<=placeNum; b++)
        {
            if (isVacant[b] == false)//乱数より小さい数に受入不能地点があれば、受入地点番号を1ふやす
            {
                placeNum++;
            }
        }

        Debug.Log("死体輸送地:" + placeNum);

        switch (placeNum)
        {
            case 0:
                x = 0;
                y = 0;
                break;
            case 1:
                x = 0;
                y = yStageSize;

                break;
            case 2:
                x = xStageSize;
                y = 0;
                break;
            case 3:
                x = xStageSize;
                y = yStageSize;
                break;
        }


        wMgr.gridCtrl.moveCharacter(xCell, yCell, x, y);
        xCell = x;
        yCell = y;
        transform.position = new Vector3(xCell, yCell, 0);
    }










    // Start is called before the first frame update
    void Start()//継承されていて上書きされるから、ここには書かずに子クラスに記述している
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
