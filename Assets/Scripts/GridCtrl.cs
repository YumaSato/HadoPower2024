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
    public GameObject[,] cellTexts;

    public int[,] red_energy;
    public int[,] blue_energy;

    // Start is called before the first frame update
    

    private void Awake()//�Ֆʎ��̂̐���
    {
        transform.position = new Vector2(STAGE_SIZE_X / 2, STAGE_SIZE_Y / 2);

        playerLayer = new Character[STAGE_SIZE_X, STAGE_SIZE_Y];


        hadoLayer = new HadoCtrl[STAGE_SIZE_X, STAGE_SIZE_Y];
        typeLayer = new CellType[STAGE_SIZE_X, STAGE_SIZE_Y];

        cellTexts = new GameObject[STAGE_SIZE_X, STAGE_SIZE_Y];
        red_energy = new int [STAGE_SIZE_X, STAGE_SIZE_Y];
        blue_energy = new int[STAGE_SIZE_X, STAGE_SIZE_Y];




        for (int ix = 0; ix < STAGE_SIZE_X; ix++)
        {
            for (int iy = 0; iy < STAGE_SIZE_X; iy++)
            {
                typeLayer[ix, iy] = CellType.VACANT;//�S�}�X��VACANT�ɃZ�b�g


            }
        }

        for (int i = 0; i< STAGE_SIZE_X; i++)//�����ɊC���Z�b�g
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
                typeLayer[ix, iy] = CellType.OCEAN;//�S�}�X��VACANT�ɃZ�b�g
            }
        }


    }


    void Start()//WorkerMgr����Awake���������Ă���łȂ��Ǝ��s���Ă͂����Ȃ��悤�Ȃ��Ƃ����s����B
    {
        foreach (Character c in wMgr.characters)//�����ʒu�ɃL������ݒu
        {
            playerLayer[c.xCell, c.yCell] = c;
            Debug.Log("�����z�u" + c.xCell.ToString() +","+  c.yCell.ToString());
        }

        playerLayer[0, 0] = null;
    }

    //public void PutAllCharacters()
    //{
    //    foreach (Character c in wMgr.characters)//�����ʒu�ɃL������ݒu
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
        giveHadoPower(_xCell, _yCell);//�g���p���[�̔�����s���B
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
            for (int ix = 0; ix < STAGE_SIZE_X; ix++)//HadoPower��S�Ă̋󔒃}�X����`�d������
            {
                for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
                {
                    var h = hadoLayer[ix, iy];
                    if (h != null || typeLayer[ix, iy] != CellType.VACANT) continue;//HadoPower���łȂ��}�X�̓X���[
                    Action<int, int> f = (xx, yy) =>
                    {
                        var usePath = new bool[STAGE_SIZE_X, STAGE_SIZE_Y];
                        provideHadoPower(ret, usePath, xx, yy);
                    };

                    f(ix - 1, iy);
                    f(ix + 1, iy);
                    f(ix, iy - 1);
                    f(ix, iy + 1);//�󔒃}�X�̑S���ʒ���
                }
            }
        };

        Action<Team> findDeadHado = checkedTeam =>
        {
            for (int ix = 0; ix < STAGE_SIZE_X; ix++)//HadoPower���G�F����0��Hado��T���o���ď����B
            {
                for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
                {
                    if (hadoLayer[ix, iy] != null)
                    {
                        
                        if (ret[ix, iy] <= 0 & hadoLayer[ix, iy].teamColor == checkedTeam)//�͂�0���팟���F�̔g���͏�����
                        {
                            string ap = "X:" + ix.ToString() + " Y:" + iy.ToString() + " power:" + ret[ix, iy];
                            Debug.Log(ap);

                            wMgr.deleteHado(hadoLayer[ix, iy].teamColor, hadoLayer[ix, iy].ID);
                            hadoLayer[ix, iy] = null;
                        }
                        else if(hadoLayer[ix, iy].teamColor == checkedTeam)//�񌟍��F�̂���ȊO�̔g���͗͂��Z�b�g�����B
                        {
                            hadoLayer[ix, iy].setPower(ret[ix, iy]);
                        }
                    }
                }
            }
        };

        generatePower();

        Team inspectedTeam;
        if (hadoLayer[newX, newY].teamColor == Team.Red) { inspectedTeam = Team.Blue; }//���u�����F�łȂ����̐F�������F�ɂ���B��ɓG�̐F�̐����𔻒�B
        else { inspectedTeam = Team.Red; }
        findDeadHado(inspectedTeam);
        
        generatePower();//���񂾓G�F�����Ȃ��Ȃ��Ă��������x�AHadoPower���v�Z�B
        findDeadHado(hadoLayer[newX, newY].teamColor);


        spreadHadoEnergy();//�g���̐����폜���I������̂ŁA����������ݏo��g���G�i�W�[���v�Z�B

    }

    void provideHadoPower(int[,] ret, bool[,] usePath, int x, int y)
    {

        if (x < 0 || STAGE_SIZE_X <= x || y < 0 || STAGE_SIZE_Y <= y) return;
        var h = hadoLayer[x, y];
        if (h == null) return;
        usePath[x,y] = true;
        ret[x, y]++;//�Ώےn�̐�����͂𑝉�������.
        Action<int, int> f = (xx, yy) =>
        {
            if (xx < 0 || STAGE_SIZE_X <= xx || yy < 0 || STAGE_SIZE_Y <= yy) return;

            if (usePath[xx, yy] || hadoLayer[x, y].teamColor != hadoLayer[xx, yy]?.teamColor) return ;

            provideHadoPower(ret, usePath, xx, yy);//���͂�G�Ɉ͂܂ꂽ�g���̐�����͂��c�����Ă���B�ǂ�����H
        };
        f(x - 1, y);
        f(x + 1, y);
        f(x, y - 1);
        f(x, y + 1);
    }










    public void spreadHadoEnergy()
    {
        int[] dx = { 0, 1, 0, -1, 1, 1, -1, -1 };
        int[] dy = { 1, 0, -1, 0, 1, -1, 1, -1 };
        double[] factor = { 0.2, 0.2, 0.2, 0.2, 0.1, 0.1, 0.1, 0.1 };


        for (int ix = 0; ix < STAGE_SIZE_X; ix++)//�S�}�X�T��
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                if (hadoLayer[ix, iy] != null)//Hado������n�_�̃G�l���M�[��ݒ�B
                {
                    switch (hadoLayer[ix, iy].teamColor)
                    {
                        case Team.Red:
                        red_energy[ix, iy] = hadoLayer[ix, iy].powerLevel;
                            break;
                        case Team.Blue:
                            blue_energy[ix, iy] = hadoLayer[ix, iy].powerLevel;
                            break;
                    }
                }
            }
        }


        for (int ix = 0; ix < STAGE_SIZE_X; ix++)//�S�}�X�T�����āA�G�l���M�[�����͂ɔg�y
        {
            for (int iy = 0; iy < STAGE_SIZE_Y; iy++)
            {
                Action<int, int, Team> f = (ix, iy, t) =>
                {
                    
                };

                if (red_energy[ix, iy] != 0)//energy����������B
                {
                    f(ix, iy, Team.Red);
                }
                if (blue_energy[ix, iy] != 0)//energy����������B
                {
                    f(ix, iy, Team.Blue);
                }
            }
        }


    }



    public bool isPlayerAndCellTypeVacant(int _xCell, int _yCell)//���s�\����
    {
        if (_xCell>=0&_xCell<STAGE_SIZE_X & _yCell>=0 & _yCell < STAGE_SIZE_Y)
        {
            if (playerLayer[_xCell, _yCell] == null & typeLayer[_xCell, _yCell] == CellType.VACANT) return true;
        }
        return false;
    }


    public bool isPlayerVacant(int _xCell, int _yCell)//�L�����N�^�����邩����
    {
        if (_xCell >= 0 & _xCell < STAGE_SIZE_X & _yCell >= 0 & _yCell < STAGE_SIZE_Y)
        {
            if (playerLayer[_xCell, _yCell] == null) return true;
        }
        return false;
    }

    public bool isCellTypeVacant(int _xCell, int _yCell)//�L�����N�^�����邩����
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