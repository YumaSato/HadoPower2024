using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//‚¢‚ç‚È‚¢

public class CellCtrl : MonoBehaviour
{
    public Character player;
    public HadoCtrl hado;

    public CellType cellType;

    // Start is called before the first frame update
    void Start()
    {
        player = null;
        hado = null;
        cellType = CellType.VACANT;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
