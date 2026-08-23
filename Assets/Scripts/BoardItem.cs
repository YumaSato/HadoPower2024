using System;

// 盤上・所持品で共通に使うアイテム種別です。
public enum BoardItemType
{
    Fragment,
    Pearl,
    PeelScroll
}

// 波動片と波動珠だけは、盤上に置かれた場所の陣営色へ再染色されます。
[Serializable]
public sealed class BoardItemData
{
    public BoardItemType type;
    public Team? team;

    public BoardItemData(BoardItemType type, Team? team = null)
    {
        this.type = type;
        this.team = team;
    }

    public int Weight
    {
        get { return type == BoardItemType.Pearl ? 20 : 1; }
    }
}
