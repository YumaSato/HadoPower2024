using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum Team : int
{
    Red = 0,
    Blue = 1
}





public class WorkerMgr : MonoBehaviour
{

    //public BearController bear;

    public GameObject RedBearPrefab;//�v���n�u��u���Ă����B
    public GameObject BlueBearPrefab;
    public GameObject RedBullPrefab;
    public GameObject BlueBullPrefab;
    public GameObject RedPenguinPrefab;
    public GameObject BluePenguinPrefab;

    public GameObject RedHadoPrefab;
    public GameObject BlueHadoPrefab;

    public GameObject meterPrefab;
    public GameObject barPrefab;
    public GameObject namePrefab;

    public GameObject CellTextPrefab;



    public List<GameObject> players;//�v���n�u���������C���X�^���X���i�[����z��B
    public List<GameObject> redHado;
    public List<GameObject> blueHado;

    //public List<GameObject> cellText;


    public List<Character> characters;//�C���X�^���X���炻�̃X�N���v�g�����𒊏o���Ċi�[�B
    public List<HadoCtrl> redHadoCtrl;
    public List<HadoCtrl> blueHadoCtrl;

 

    public Camera camera;
    public CameraCtrl cameraCtrl;
    public Canvas canvas;
    public CanvasCtrl canvasCtrl;
    public Grid grid;
    public GridCtrl gridCtrl;


    public int turnOrder;
    int turnNumSum = 0;

    // Start is called before the first frame update
    void Awake()
    {
        gridCtrl = grid.GetComponent<GridCtrl>();
        gridCtrl.wMgr = this;
        cameraCtrl = camera.GetComponent<CameraCtrl>();
        cameraCtrl.wMgr = this;
        canvasCtrl = canvas.GetComponent<CanvasCtrl>();
        canvasCtrl.wMgr = this;

        players = new List<GameObject>();
        redHado = new List<GameObject>();
        blueHado = new List<GameObject>();


        characters = new List<Character>();
        redHadoCtrl = new List<HadoCtrl>();
        blueHadoCtrl = new List<HadoCtrl>();



        Action<GameObject, Character,int,int,Team> registerCharacter = (g, c, x,y,t) =>
        {
            players.Add(g);
            characters.Add(c);
            c.setCharacter(x, y, t, this, g);
        };

        var p0 = Instantiate(RedBearPrefab, new Vector2(-1.52f, -1.48f), Quaternion.identity);//�v���n�u�����ꂽ�ԌF�𐶐�
        var c0 = p0.GetComponent<BearController>();
        registerCharacter(p0, c0, 7, 7, Team.Red);

        var p1 = Instantiate(BlueBearPrefab, new Vector2(2.52f, -1.48f), Quaternion.identity);//�v���n�u�����ꂽ�F�𐶐�
        var c1 = p1.GetComponent<BearController>();
        registerCharacter(p1, c1, 7, 15, Team.Blue);

        var p2 = Instantiate(RedPenguinPrefab, new Vector2(3.52f, -2.48f), Quaternion.identity);//�ԃy���M��
        var c2 = p2.GetComponent<PenguinController>();
        registerCharacter(p2, c2, 15, 15, Team.Red);

        var p3 = Instantiate(BluePenguinPrefab, new Vector2(4.52f, -2.48f), Quaternion.identity);//�y���M��
        var c3 = p3.GetComponent<PenguinController>();
        registerCharacter(p3, c3, 15, 7, Team.Blue);




        //gridCtrl.PutAllCharacters();//Grid�̃}�X�ɍ쐬�����S�L�����N�^�̃|�C���^���i�[�����

        turnOrder = 1;
        Debug.Log("turnOrder��" + turnOrder + "�Ŏn�܂���");
        
        //characters[turnOrder].startTurn();///�ŏ��̃^�[���J�n
    }

    // Update is called once per frame
    void Update()
    {
            characters[turnOrder].startTurn();
    }







    /// <summary>
    /// �ݒu�ɐ��������1���Ԃ��Ă���
    /// </summary>
    /// <param name="teamNum"></param>
    /// <param name="_xCell"></param>
    /// <param name="_yCell"></param>
    /// <returns></returns>
    public int createHado(Team teamNum, int _xCell, int _yCell)
    {
        if (!gridCtrl.isHadoVacant(_xCell, _yCell))//�g�����C���[��NULL�łȂ��ꍇ�A�g���쐬�Ɏ��s
        {
            return 0;
        }
        if (teamNum == Team.Red)
        {
            var p = Instantiate(RedHadoPrefab, new Vector2(_xCell, _yCell), Quaternion.identity);
            redHado.Add(p);
            var c = p.GetComponent<HadoCtrl>();
            redHadoCtrl.Add(c);
            c.init(teamNum, this);
            gridCtrl.createHado(_xCell, _yCell, c);
        }
        if (teamNum == Team.Blue)
        {
            //Debug.Log("�Ƃ肠������x�C���X�^���X�����Ă݂�");
            var p = Instantiate(BlueHadoPrefab, new Vector2(_xCell, _yCell), Quaternion.identity);
            blueHado.Add(p);
            var c = p.GetComponent<HadoCtrl>();
            blueHadoCtrl.Add(c);
            c.init(teamNum, this);
            gridCtrl.createHado(_xCell, _yCell, c);
        }
        

        return 1;
    }

    public void deleteHado(Team team, int num)
    {
        int p;
        Action<List<GameObject>, List<HadoCtrl>> f = (hado, hadoCtrl) =>
        {
            p = hadoCtrl.FindIndex(obj => obj.ID == num);
            hadoCtrl[p].kill();
            hadoCtrl.RemoveAt(p);
            hado.RemoveAt(p);
        };
        switch (team)
        {
            case Team.Red:
                f(redHado, redHadoCtrl);
                break;
            case Team.Blue:
                f(blueHado, blueHadoCtrl);
                break;
        }

    }


    public void turnNext()
    {
        turnOrder++;
        if (turnOrder==characters.Count) turnOrder = 0;//�L���������������߂�B

        //Debug.Log("turnOrder��" + turnOrder + "�ɂȂ���");
        //�{���͂����ɁA���̃L������startTurn���Ăԕ���������ׂ��Ȃ̂����AUpdate�Ɉڐ݂����B


        Debug.Log("0,0"+ gridCtrl.isPlayerVacant(0, 0));
        //Debug.Log(gridCtrl.playerLayer[0,0].attackPower);
    }
}
