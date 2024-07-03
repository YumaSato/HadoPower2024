using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PenguinController : Character
{
    // Start is called before the first frame update
    void Start()
    {
        attackPower = 55;
        this.anim = GetComponent<Animator>();
    }

    //Update is called once per frame
    void Update()
    {
        generalUpdate();
    }
}
