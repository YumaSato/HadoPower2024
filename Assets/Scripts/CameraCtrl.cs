using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Apple;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class CameraCtrl : MonoBehaviour
{
    private Camera mainCam;
    public WorkerMgr wMgr;
    bool controllingFlag;
    public Vector3 destination;
    bool isMoving = false;

    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
        controllingFlag = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(wMgr.characters.Count > 0 & controllingFlag == false)//盤上にキャラクタが存在していたら
        {
            float x = wMgr.characters[wMgr.turnOrder].transform.position.x;
            float y = wMgr.characters[wMgr.turnOrder].transform.position.y;
            destination = new Vector3(x, y, transform.position.z);
            if(isMoving == false)
            {
                StartCoroutine(Move(destination, 30f));//移動状態の映像、開始。

                //Debug.Log("zは" + destination.z + "で始まった");
            }
        }


        var scroll = Input.mouseScrollDelta.y * Time.deltaTime * 20;//ホイールで拡大縮小
        //Debug.Log(scroll);
        if ( 2 < mainCam.orthographicSize)
        {
            mainCam.orthographicSize += scroll;
        }
        else
        {
            mainCam.orthographicSize = 2;
        }
        if (mainCam.orthographicSize < 15)
        {
            mainCam.orthographicSize += scroll;
        }
        else
        {
            mainCam.orthographicSize = 15;
        }
    }


    IEnumerator Move(Vector3 destination, float speed)
    {
        isMoving = true;

        //現在とターゲットの場所が違ったら近づけ続ける。
        while ((destination - transform.position).sqrMagnitude > Mathf.Epsilon)//現在地と目的地が違う場合
        {
            //近づける(MoveTowardは、「1現在地, 2目標地点, 3速度」で目標に向かって移動するという関数)
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            yield return null;//1フレーム分待つという意味。
        }
        transform.position = destination;
       isMoving = false;
    }



}
