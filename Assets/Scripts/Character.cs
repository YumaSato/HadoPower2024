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
    public bool ghost;//��x���񂾂�


    public int attackPower;

    public PointMeter hp;
    public PointMeter stamina;


    //character()//�p������Ă��邽�߃R���X�g���N�^�������ɍ��Ɩʓ|�Ȃ��Ƃ��N����H
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
        //wMgr.gridCtrl.PutCharacter(this);//�����z�u

        hp = this.gameObject.AddComponent<PointMeter>();
        stamina = this.gameObject.AddComponent<PointMeter>();

        hp.setPointMeter(this, "HP", 100);
        stamina.setPointMeter(this, "��", 20);

        deadCountMax = 3; deadCount = 0;//���񂾎��ɉ��^�[���ŕ������邩
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

            if (wMgr.gridCtrl.isPlayerAndCellTypeVacant(xCell + dx, yCell + dy) && stamina.preP > 0)//�ړ��������}�X���󔒂ŁA�X�^�~�i��0�łȂ����
            {
                wMgr.gridCtrl.moveCharacter(xCell, yCell, xCell + dx, yCell + dy);
                xCell = xCell + dx;
                yCell = yCell + dy;

                StartCoroutine(Move(new Vector2(x, y)));//�ړ���Ԃ̉f���A�J�n�B
                stamina.change(-1);
            }
        }
    }

    public void specialWalk()//Vacant�ȊO�̒n�`�̏ꏊ����������s
    {
        if (isMoving == false && ((Input.GetKey(KeyCode.RightShift)) == false && (Input.GetKey(KeyCode.LeftShift)) == false) && (Input.GetAxisRaw("Horizontal") != 0 | Input.GetAxisRaw("Vertical") != 0))
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            dx = (int)x;
            dy = (int)y;
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);

            if (wMgr.gridCtrl.isPlayerVacant(xCell + dx, yCell + dy) && stamina.preP > 0)//�ړ��������}�X���󔒂ŁA�X�^�~�i��0�łȂ����
            {
                wMgr.gridCtrl.moveCharacter(xCell, yCell, xCell + dx, yCell + dy);
                xCell = xCell + dx;
                yCell = yCell + dy;

                StartCoroutine(Move(new Vector2(x, y)));//�ړ���Ԃ̉f���A�J�n�B
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
        //���݂ƃ^�[�Q�b�g�̏ꏊ���������߂Â�������B
        while ((targetPos - transform.position).sqrMagnitude > Mathf.Epsilon)
        {
            //�߂Â���(MoveToward�́A�u1���ݒn, 2�ڕW�n�_, 3���x�v�ŖڕW�Ɍ������Ĉړ�����Ƃ����֐�)
            transform.position = Vector3.MoveTowards(transform.position, targetPos, 5f * Time.deltaTime);
            yield return null;//1�t���[�����҂Ƃ����Ӗ��B
        }
        transform.position = targetPos;
        isMoving = false; 
    }




    public void installPower()
    {
        if (Input.GetKeyDown("z") & ResidueHado > 0)
        {

            ResidueHado = ResidueHado - wMgr.createHado(teamNum, xCell, yCell);//�ݒu�ɐ��������1���Ԃ��Ă���
        }
        
    }

    public bool attack()
    {
        if(Input.GetKeyDown("c") & ResidueHado > 0)
        {
            if(wMgr.gridCtrl.isPlayerVacant(xCell + dx, yCell + dy) == false)//�Ώےn�ɒN������ꍇ
            {
                Debug.Log(string.Format("�U���Bx:", xCell + dx , "y,", yCell + dy));

                beatSound.Play();
                beatSound.volume = 0.3f;
                beatSound.time = 0.6f;
                wMgr.gridCtrl.getCharacter(xCell + dx, yCell + dy).hp.change(-attackPower);
                return true;
            }
        }
        return false;
    }


    public void startTurn()//�^�[�����n�܂�Ƃ���wMgr����Ă΂��B
    {
        if(isMyTurn)return;//���Ɏ����̃^�[���ł���ꍇ�A�����ς��邱�Ƃ͂Ȃ��B
        
        ResidueHado = 1;
        isMyTurn = true;
        if(deadCount == deadCountMax)
        {
            hp.change(hp.maxP);
        }

    }

    public void endTurn()//�����I������Ƃ��ȂǁA������ĂԁBturnNext���Ă�ŁA�Ώۂ����̃L�����ɕς��Ă��炤�B
    {
        isMyTurn = false;
        stamina.change(stamina.naturalRecovery);
        wMgr.turnNext();
    }


    public void generalUpdate()//�A�b�v�f�[�g�i����j���e�X�̃L�����N�^�[�̃R���g���[����Update����Ă�ł��炤�B
    {
        if (isMyTurn)
        {
            bool isTurnEnd = false;
            changeDirection();

            if (hp.preP == 0)//����ł���L�����̓X���[
            {
                //�{���A�����Ŏ��ҕ����^�[���P���Z��\������B
                if (Input.GetKeyDown("x")) isTurnEnd = true;
                if (isTurnEnd)
                {
                    Debug.Log("���҂̃^�[���B�����܂ł���" + (deadCountMax - deadCount - 1) + "�^�[���ɂȂ���");
                    deadCount += 1;
                    endTurn();
                }
            }
            else if (deadCount == deadCountMax)//�����^�[���̓��ʍs��
            {
                specialWalk();
                installPower();
                isTurnEnd = attack();
                if (Input.GetKeyDown("x")) isTurnEnd = true;
                if (isTurnEnd)
                {
                    deadCount = 0;
                    stamina.change(stamina.naturalRecovery);//�X�^�~�i���R��
                    if (wMgr.gridCtrl.isCellTypeVacant(xCell, yCell) == false)//�ʏ�}�X�ɖ߂��Ă��Ȃ���Ύ�
                    {
                        Debug.Log("���ҕ����^�[���I��" + wMgr.gridCtrl.isCellTypeVacant(xCell, yCell));
                        death();
                    }
                    Debug.Log("endTurn���Ă񂾁B");
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
                    stamina.preP = stamina.preP + stamina.naturalRecovery;//�X�^�~�i���R��
                    Debug.Log("endTurn���Ă񂾁B");
                    endTurn();
                }
            }
        }
    }

    public void death()//���̃L�������g�����񂾎�,���̓o�^�n�_�ֈړ������A�����ҋ@��Ԃɂ�����B
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
            if(c.hp.preP == 0)//���̐��J�E���g�̑����Ǝ��̒n�_�i����s�\�n�_�j�o�^
            {
                deadCharacterNum++;
                if (c.xCell == 0 && c.yCell == 0) isVacant[0] = false;
                if (c.xCell == 0 && c.yCell == yStageSize) isVacant[1] = false;
                if (c.xCell == xStageSize && c.yCell == 0) isVacant[2] = false;
                if (c.xCell == xStageSize && c.yCell == yStageSize) isVacant[3] = false;
            }
        }
        

        placeNum = Random.Range(0, 4 - deadCharacterNum);//���̗A����̎���������������̂ŁA����n�_�ԍ��𗐐��œ���B

        Debug.Log("���̎���n�_�� [0]" + isVacant[0] + "  [1]" + isVacant[1] + "  [2]" + isVacant[2] + "  [3]" + isVacant[3] + "     ���̗A����]�n:" + placeNum);

        for (int b=0; b<=placeNum; b++)
        {
            if (isVacant[b] == false)//������菬�������Ɏ���s�\�n�_������΁A����n�_�ԍ���1�ӂ₷
            {
                placeNum++;
            }
        }

        Debug.Log("���̗A���n:" + placeNum);

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
    void Start()//�p������Ă��ď㏑������邩��A�����ɂ͏������Ɏq�N���X�ɋL�q���Ă���
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
