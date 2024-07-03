using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

//using DG.Tweening;

public class CanvasCtrl : MonoBehaviour
{

    public WorkerMgr wMgr;
    private GameObject redStatusBack;
    private GameObject blueStatusBack;
    public Image redImageComp;
    public Image blueImageComp;

    public GameObject redText;
    public GameObject blueText;
    public TextMeshProUGUI redTextComp;
    public TextMeshProUGUI blueTextComp;

    bool isMoving = false;
    bool isOutOfScreen = false;

    // Start is called before the first frame update
    void Start()
    {
        redStatusBack = GameObject.Find("RedStatus");
        blueStatusBack = GameObject.Find("BlueStatus");
        redImageComp = redStatusBack.GetComponent<Image>();
        blueImageComp = blueStatusBack.GetComponent<Image>();

        redText = GameObject.Find("RedText");
        blueText = GameObject.Find("BlueText");
        redTextComp = redText.GetComponent<TextMeshProUGUI>();
        blueTextComp = blueText.GetComponent<TextMeshProUGUI>();

        
    }

    // Update is called once per frame
    void Update()
    {
        float placeY = 115f;

        if (Input.GetKeyDown("q") & isMoving == false & isOutOfScreen == false)
        {
            isOutOfScreen = true;
            float hidden_place = 800;
            Vector2 redV = new Vector2(-hidden_place, placeY);
            Vector2 blueV = new Vector2(hidden_place, placeY);
            StartCoroutine(Move(redV, blueV));//移動状態の映像、開始。
        }
        else if (Input.GetKeyDown("q") & isMoving == false & isOutOfScreen == true)
        {
            isOutOfScreen = false;
            float shwoing_place = 390f;
            Vector2 redV = new Vector2(-shwoing_place, placeY);
            Vector2 blueV = new Vector2(shwoing_place, placeY);
            StartCoroutine(Move(redV, blueV));//移動状態の映像、開始。
        }
    }

    void changeStatus()
    {
        //wMgr.characters[0].
    }



    IEnumerator Move(Vector2 RedDestination, Vector2 BlueDestination)
    {
        RectTransform redImageRectT = redImageComp.GetComponent<RectTransform>();
        RectTransform redTextRectT = redTextComp.GetComponent<RectTransform>();

        RectTransform blueImageRectT = blueImageComp.GetComponent<RectTransform>();
        RectTransform blueTextRectT = blueTextComp.GetComponent<RectTransform>();
        float speed = 800f;

        isMoving = true;
        //現在とターゲットの場所が違ったら近づけ続ける。
        while ((RedDestination - redImageRectT.anchoredPosition).sqrMagnitude > Mathf.Epsilon)//現在地と目的地が違う場合
        {
            //近づける(MoveTowardは、「1現在地, 2目標地点, 3速度」で目標に向かって移動するという関数)
            redImageRectT.anchoredPosition = Vector2.MoveTowards(redImageRectT.anchoredPosition, RedDestination, speed * Time.deltaTime);
            redTextRectT.anchoredPosition = Vector2.MoveTowards(redTextRectT.anchoredPosition, RedDestination, speed * Time.deltaTime);
            blueImageRectT.anchoredPosition = Vector2.MoveTowards(blueImageRectT.anchoredPosition, BlueDestination, speed * Time.deltaTime);
            blueTextRectT.anchoredPosition = Vector2.MoveTowards(blueTextRectT.anchoredPosition, blueImageRectT.anchoredPosition, speed * Time.deltaTime);

            yield return null;//1フレーム分待つという意味。
        }
        redImageRectT.anchoredPosition = RedDestination;
        redTextRectT.anchoredPosition = RedDestination;

        isMoving = false;
    }


    //redTextComp.GetComponent<RectTransform>().position = Vector3.MoveTowards(redTextComp.GetComponent<RectTransform>().position, RedDestination, 10f * Time.deltaTime);


    //IEnumerator Move(Vector3 RedDestination, Vector3 BlueDestination)
    //{
    //    isMoving = true;
    //    RectTransform rtRedImage = redImageComp.GetComponent<RectTransform>();
    //    RectTransform rtRedText = redTextComp.GetComponent<RectTransform>();

    //    //現在とターゲットの場所が違ったら近づけ続ける。
    //    while ((RedDestination - rtRedImage.position).sqrMagnitude > Mathf.Epsilon)//現在地と目的地が違う場合
    //    {
    //        //近づける(MoveTowardは、「1現在地, 2目標地点, 3速度」で目標に向かって移動するという関数)
    //        rtRedImage.position = Vector3.MoveTowards(rtRedImage.position, RedDestination, 10f * Time.deltaTime);
    //        rtRedText.position = Vector3.MoveTowards(rtRedText.position, RedDestination, 10f * Time.deltaTime);
    //        yield return null;//1フレーム分待つという意味。
    //    }
    //    rtRedImage.position = RedDestination;
    //    isMoving = false;
    //}

}
