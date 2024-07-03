using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BearController : Character
{





    //Start is called before the first frame update
    void Start()
    {
        attackPower = 100;
        this.anim = GetComponent<Animator>();
        
    }

    //Update is called once per frame
    void Update()
    {
        generalUpdate();
    }
}
