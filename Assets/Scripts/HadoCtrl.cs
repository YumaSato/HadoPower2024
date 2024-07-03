using System.Collections;
using System.Collections.Generic;
using UnityEngine;





public class HadoCtrl : MonoBehaviour
{

    static int ID_COUNTER;


    public WorkerMgr wMgr;
    public Animator anim;


    [SerializeField]//ID���C���X�y�N�^�Ō����邯�ǕҏW�ł��Ȃ�����
    private int id;
    public int ID
    {
        get { return id; }
        private set { id = value; }
    }
    
    public Team teamColor { get; private set; }
    public int powerLevel;

    // Start is called before the first frame update
    void Start()
    {
        this.anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void setPower(int power)
    {
        powerLevel = power;
    }

    public void kill()
    {
        Destroy(gameObject);
    }


    public void init(Team team, WorkerMgr w)
    {
        ID = ID_COUNTER++;
        teamColor = team;
        wMgr = w;
    }
}
