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
            StartCoroutine(Move(redV, blueV));//�ړ���Ԃ̉f���A�J�n�B
        }
        else if (Input.GetKeyDown("q") & isMoving == false & isOutOfScreen == true)
        {
            isOutOfScreen = false;
            float shwoing_place = 390f;
            Vector2 redV = new Vector2(-shwoing_place, placeY);
            Vector2 blueV = new Vector2(shwoing_place, placeY);
            StartCoroutine(Move(redV, blueV));//�ړ���Ԃ̉f���A�J�n�B
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
        //���݂ƃ^�[�Q�b�g�̏ꏊ���������߂Â�������B
        while ((RedDestination - redImageRectT.anchoredPosition).sqrMagnitude > Mathf.Epsilon)//���ݒn�ƖړI�n���Ⴄ�ꍇ
        {
            //�߂Â���(MoveToward�́A�u1���ݒn, 2�ڕW�n�_, 3���x�v�ŖڕW�Ɍ������Ĉړ�����Ƃ����֐�)
            redImageRectT.anchoredPosition = Vector2.MoveTowards(redImageRectT.anchoredPosition, RedDestination, speed * Time.deltaTime);
            redTextRectT.anchoredPosition = Vector2.MoveTowards(redTextRectT.anchoredPosition, RedDestination, speed * Time.deltaTime);
            blueImageRectT.anchoredPosition = Vector2.MoveTowards(blueImageRectT.anchoredPosition, BlueDestination, speed * Time.deltaTime);
            blueTextRectT.anchoredPosition = Vector2.MoveTowards(blueTextRectT.anchoredPosition, blueImageRectT.anchoredPosition, speed * Time.deltaTime);

            yield return null;//1�t���[�����҂Ƃ����Ӗ��B
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

    //    //���݂ƃ^�[�Q�b�g�̏ꏊ���������߂Â�������B
    //    while ((RedDestination - rtRedImage.position).sqrMagnitude > Mathf.Epsilon)//���ݒn�ƖړI�n���Ⴄ�ꍇ
    //    {
    //        //�߂Â���(MoveToward�́A�u1���ݒn, 2�ڕW�n�_, 3���x�v�ŖڕW�Ɍ������Ĉړ�����Ƃ����֐�)
    //        rtRedImage.position = Vector3.MoveTowards(rtRedImage.position, RedDestination, 10f * Time.deltaTime);
    //        rtRedText.position = Vector3.MoveTowards(rtRedText.position, RedDestination, 10f * Time.deltaTime);
    //        yield return null;//1�t���[�����҂Ƃ����Ӗ��B
    //    }
    //    rtRedImage.position = RedDestination;
    //    isMoving = false;
    //}

}
