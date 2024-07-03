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
        if(wMgr.characters.Count > 0 & controllingFlag == false)//�Տ�ɃL�����N�^�����݂��Ă�����
        {
            float x = wMgr.characters[wMgr.turnOrder].transform.position.x;
            float y = wMgr.characters[wMgr.turnOrder].transform.position.y;
            destination = new Vector3(x, y, transform.position.z);
            if(isMoving == false)
            {
                StartCoroutine(Move(destination, 30f));//�ړ���Ԃ̉f���A�J�n�B

                //Debug.Log("z��" + destination.z + "�Ŏn�܂���");
            }
        }


        var scroll = Input.mouseScrollDelta.y * Time.deltaTime * 20;//�z�C�[���Ŋg��k��
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

        //���݂ƃ^�[�Q�b�g�̏ꏊ���������߂Â�������B
        while ((destination - transform.position).sqrMagnitude > Mathf.Epsilon)//���ݒn�ƖړI�n���Ⴄ�ꍇ
        {
            //�߂Â���(MoveToward�́A�u1���ݒn, 2�ڕW�n�_, 3���x�v�ŖڕW�Ɍ������Ĉړ�����Ƃ����֐�)
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            yield return null;//1�t���[�����҂Ƃ����Ӗ��B
        }
        transform.position = destination;
       isMoving = false;
    }



}
