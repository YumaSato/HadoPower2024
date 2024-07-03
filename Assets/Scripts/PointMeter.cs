using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PointMeter : MonoBehaviour
{

    public Character c;

    public string meterName;

    public int maxP;
    //public int minP;
    public int preP;

    public int naturalRecovery;



    //public GameObject meterPrefab;
    //public GameObject barPrefab;
    //public GameObject namePrefab;

    public GameObject meter;
    //public GameObject bar;
    //public GameObject comment;

    public Slider slider;
    public GameObject meterNameObject;

    public void setPointMeter(Character _c, string _meterName, int _maxP)
    {
        c = _c;
        meterName = _meterName;
        meter = null;

        naturalRecovery = 10;
        maxP = _maxP;
        if (maxP <= 0) maxP = 1;
        preP = maxP;

        //Debug.Log(c.transform.position);

        Vector3 a = new Vector3(c.xCell, c.yCell, 0);
        if (meterName == "��")
        {
            a = new Vector3(c.xCell, c.yCell - 0.2f, 0);
        }


        meter = Instantiate(c.wMgr.meterPrefab, a, Quaternion.identity);
        meter.SetActive(false);
        meter.transform.parent = c.transform;//Character(C)��Transform��Ǐ]����悤�ɂ����B
        meter.name = meterName;

        slider = meter.GetComponentInChildren<Slider>();

        slider.value = preP / maxP;

        //meterNameObject =meter.transform.Find("Canvas/MeterName").gameObject;
        TextMeshProUGUI nameText = meter.GetComponentInChildren<TextMeshProUGUI>();

        if (meterName == "��")
        {
            nameText.text = meterName;
        }


    }



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("UpdatePointMeter");


    }



    public void change(int changeHP)
    {
        bool showMeterFlag = true;

        preP = preP + changeHP;
        if (preP <= 0)//���񂾂Ƃ�
        {
            preP = 0;
            showMeterFlag = false;
        }
        if (preP >= maxP)
        {
            preP = maxP;
            showMeterFlag = false;
        }


        slider.value = (float)preP / (float)maxP;
        //Debug.Log(preP.ToString() + "/" + maxP.ToString() + "=" + slider.value.ToString());

        if (showMeterFlag)//�\������
        {

            meter.SetActive(true);


        }
        else
        {

            meter.SetActive(false);
        }

        if(preP == 0 && meterName == "HP")//0�ɂȂ������[�^�[�̒l��HP�ł������玀
        {
            c.death();
        }
    }
}