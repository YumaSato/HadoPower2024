using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    private const int MaxInventorySlots = 30;
    private const int RespawnWaitTurns = 2;

    public GameObject myBody;
    public WorkerMgr wMgr;
    public Animator anim;
    public AudioSource beatSound;

    public bool isMyTurn;
    public int ResidueHado = 1;
    public Team teamNum;
    public int xCell;
    public int yCell;
    public int dx = 1;
    public int dy = 0;

    // 復活地点は、各色の結界に斜め隣接する二マスのうち担当キャラクターごとに固定する。
    public int spawnX;
    public int spawnY;
    public bool ghost;

    public PointMeter hp;
    public PointMeter stamina;
    public PointMeter power;

    private bool isMoving;
    private bool hasCompletedWalkThisTurn;
    private int respawnTurnsRemaining;
    private readonly List<BoardItemData> inventory = new List<BoardItemData>();

    public bool IsMoving => isMoving;
    // HUD は入力状態そのものを変更せず、現在ターンに足元へ波動石を置けるかを表示する。
    public bool HasCompletedWalkThisTurn => hasCompletedWalkThisTurn;
    public int RespawnTurnsRemaining => respawnTurnsRemaining;
    public int InventoryCount => inventory.Count;
    public bool HasFreeInventorySlot => InventoryCount < MaxInventorySlots;

    // 波動珠は一枠だが、移動スタミナ上では20個分の重さとして扱う。
    public int InventoryWeight
    {
        get
        {
            int total = 0;
            foreach (BoardItemData item in inventory) total += item.Weight;
            return total;
        }
    }

    public void setCharacter(int x, int y, Team t, WorkerMgr w, GameObject body)
    {
        wMgr = w;
        myBody = body;
        xCell = x;
        yCell = y;
        spawnX = x;
        spawnY = y;
        transform.position = new Vector3(x, y, 0.0f);
        teamNum = t;
        beatSound = GetComponent<AudioSource>();

        hp = gameObject.AddComponent<PointMeter>();
        stamina = gameObject.AddComponent<PointMeter>();
        power = gameObject.AddComponent<PointMeter>();
        hp.setPointMeter(this, "HP", 10000.0f);
        stamina.setPointMeter(this, "Sta", 20.0f);
        power.setPointMeter(this, "Pow", 5000.0f);
        power.setPoint(1.0f);

        ghost = false;
        respawnTurnsRemaining = 0;
    }

    public void walk()
    {
        if (isMoving || IsModifierHeld()) return;
        if (Input.GetAxisRaw("Horizontal") == 0 && Input.GetAxisRaw("Vertical") == 0) return;

        int moveX = (int)Input.GetAxisRaw("Horizontal");
        int moveY = (int)Input.GetAxisRaw("Vertical");
        dx = moveX;
        dy = moveY;
        if (anim != null)
        {
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);
        }

        int targetX = xCell + dx;
        int targetY = yCell + dy;
        if (!wMgr.gridCtrl.isPlayerAndCellTypeVacant(targetX, targetY)) return;

        float movementCost = wMgr.gridCtrl.GetMovementCost(targetX, targetY, this);
        if (stamina.preP + 0.0001f < movementCost) return;

        wMgr.gridCtrl.moveCharacter(xCell, yCell, targetX, targetY);
        xCell = targetX;
        yCell = targetY;
        stamina.change(-movementCost);
        // 通常移動では到着時に自動拾得する。Ctrlを押している時だけ拾わずに通過する。
        bool shouldAutoPickUp = !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        StartCoroutine(Move(new Vector2(moveX, moveY), shouldAutoPickUp));
    }

    // 既存の派生クラスやイベントから呼ばれても、新しい移動規則を通すため walk と同じ処理にする。
    public void specialWalk()
    {
        walk();
    }

    public void changeDirection()
    {
        if (isMoving || !IsModifierHeld()) return;
        if (Input.GetAxisRaw("Horizontal") == 0 && Input.GetAxisRaw("Vertical") == 0) return;

        dx = (int)Input.GetAxisRaw("Horizontal");
        dy = (int)Input.GetAxisRaw("Vertical");
        if (anim != null)
        {
            anim.SetInteger("xd", dx);
            anim.SetInteger("yd", dy);
        }
    }

    private static bool IsModifierHeld()
    {
        return Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift);
    }

    private IEnumerator Move(Vector3 direction, bool shouldAutoPickUp)
    {
        isMoving = true;
        Vector3 targetPosition = transform.position + direction;
        while ((targetPosition - transform.position).sqrMagnitude > Mathf.Epsilon)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, 5.0f * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
        hasCompletedWalkThisTurn = true;
        wMgr.gridCtrl.HandleWalkFinished(this, shouldAutoPickUp);
    }

    // 波動石だけは、実際に歩行を終えたターンに足元へ置ける。
    public void installPower()
    {
        if (!Input.GetKeyDown(KeyCode.Z) || isMoving || !hasCompletedWalkThisTurn || ResidueHado <= 0) return;
        ResidueHado -= wMgr.createHado(teamNum, xCell, yCell);
    }

    private void HandleItemInputs()
    {
        if (isMoving) return;

        // V: 自軍色の波動片20個を波動珠1個へ変換する前に、確認画面を開く。
        if (Input.GetKeyDown(KeyCode.V)) wMgr.gridCtrl.TryOpenFragmentConversionPrompt(this, true);
        // E: 敵色結界に接する5マスから、自軍色の波動珠を一つ投入する。
        if (Input.GetKeyDown(KeyCode.E)) wMgr.gridCtrl.TryDepositPearl(this);
        // 剥離魔法符などのアイテム使用は、Aで開くアイテム行動メニューから行う。
    }

    // 旧来の通常攻撃は、勝利条件ではない補助行動として残す。
    // 地パワーを攻撃力へ直接変換する処理は行わない。
    public bool attack()
    {
        if (!Input.GetKeyDown(KeyCode.C) || isMoving || ResidueHado <= 0) return false;

        Character target = wMgr.gridCtrl.getCharacter(xCell + dx, yCell + dy);
        if (target == null || target.hp.preP <= 0.0f) return false;

        if (beatSound != null)
        {
            beatSound.Play();
            beatSound.volume = 0.3f;
            beatSound.time = 0.6f;
        }
        target.hp.change(-power.preP);
        power.setPoint(1.0f);
        return true;
    }

    public void startTurn()
    {
        if (isMyTurn || wMgr.gridCtrl.IsGameOver) return;

        // 死亡後は本人の二ターンを自動で消費し、三度目の開始時に満タンで復活する。
        if (ghost)
        {
            if (respawnTurnsRemaining > 0)
            {
                respawnTurnsRemaining--;
                wMgr.gridCtrl.HandleTurnEnded();
                wMgr.turnNext();
                return;
            }

            ghost = false;
            wMgr.gridCtrl.RespawnCharacter(this);
            hp.setPoint(hp.maxP);
            stamina.setPoint(stamina.maxP);
        }

        ResidueHado = 1;
        hasCompletedWalkThisTurn = false;
        stamina.setPoint(stamina.maxP);
        isMyTurn = true;
    }

    public void endTurn()
    {
        if (!isMyTurn) return;
        wMgr.gridCtrl.CancelItemInteraction(this);
        isMyTurn = false;
        if (hasCompletedWalkThisTurn && !ghost)
        {
            wMgr.gridCtrl.ApplyEndOfTurnGroundEffect(this);
        }
        wMgr.gridCtrl.HandleTurnEnded();
        wMgr.turnNext();
    }

    public void generalUpdate()
    {
        if (!isMyTurn || ghost || wMgr.gridCtrl.IsGameOver) return;

        // 取捨・投擲画面を開いている間は、移動や他の行動入力を受け付けない。
        if (wMgr.gridCtrl.HandleItemInteractionInput(this)) return;

        changeDirection();
        walk();
        installPower();
        HandleItemInputs();

        bool attackEnded = attack();
        if ((Input.GetKeyDown(KeyCode.X) && !isMoving) || attackEnded)
        {
            endTurn();
        }
    }

    public void death()
    {
        if (ghost) return;

        wMgr.gridCtrl.CancelItemInteraction(this);

        int deathX = xCell;
        int deathY = yCell;
        hp.setPoint(0.0f);
        ghost = true;
        respawnTurnsRemaining = RespawnWaitTurns;
        wMgr.gridCtrl.DropAllItemsAt(this, deathX, deathY);
        wMgr.gridCtrl.RemoveCharacterFromBoard(this);

        if (isMyTurn) endTurn();
    }

    public bool AddItem(BoardItemData item)
    {
        if (item == null || !HasFreeInventorySlot) return false;
        inventory.Add(item);
        return true;
    }

    public int CountItems(BoardItemType type, Team? team)
    {
        int count = 0;
        foreach (BoardItemData item in inventory)
        {
            if (item.type != type) continue;
            if (team.HasValue && item.team != team) continue;
            count++;
        }
        return count;
    }

    // 色なしアイテムを含め、種類と陣営が完全に一致する数を返す。
    public int CountItemsExact(BoardItemType type, Team? team)
    {
        int count = 0;
        foreach (BoardItemData item in inventory)
        {
            if (item.type == type && item.team == team) count++;
        }
        return count;
    }

    public void RemoveItems(BoardItemType type, Team? team, int count)
    {
        for (int index = inventory.Count - 1; index >= 0 && count > 0; index--)
        {
            BoardItemData item = inventory[index];
            if (item.type != type) continue;
            if (team.HasValue && item.team != team) continue;
            inventory.RemoveAt(index);
            count--;
        }
    }

    public BoardItemData RemoveFirstItem(BoardItemType type, Team? team)
    {
        for (int index = 0; index < inventory.Count; index++)
        {
            BoardItemData item = inventory[index];
            if (item.type != type) continue;
            if (team.HasValue && item.team != team) continue;
            inventory.RemoveAt(index);
            return item;
        }
        return null;
    }

    public BoardItemData RemoveFirstItemExact(BoardItemType type, Team? team)
    {
        for (int index = 0; index < inventory.Count; index++)
        {
            BoardItemData item = inventory[index];
            if (item.type != type || item.team != team) continue;
            inventory.RemoveAt(index);
            return item;
        }
        return null;
    }

    public BoardItemData RemoveFirstColorableItemForDrop()
    {
        for (int index = 0; index < inventory.Count; index++)
        {
            BoardItemData item = inventory[index];
            bool isColorable = item.type == BoardItemType.Fragment || item.type == BoardItemType.Pearl;
            if (!isColorable || item.team == teamNum) continue;
            inventory.RemoveAt(index);
            return item;
        }

        for (int index = 0; index < inventory.Count; index++)
        {
            BoardItemData item = inventory[index];
            if (item.type != BoardItemType.Fragment && item.type != BoardItemType.Pearl) continue;
            inventory.RemoveAt(index);
            return item;
        }
        return null;
    }

    public List<BoardItemData> RemoveAllItems()
    {
        List<BoardItemData> droppedItems = new List<BoardItemData>(inventory);
        inventory.Clear();
        return droppedItems;
    }

    // 旧コードから呼ばれても地パワーを攻撃力へ接続しないため、固定の補助攻撃力を維持する。
    public void setPower()
    {
        power.setPoint(1.0f);
    }
}
