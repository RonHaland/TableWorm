using AzureTableContext.Attributes;


namespace AzureTableContext.Tests.Entities;

public class Tournaments : TableModel
{
    public string Name { get; set; } = null!;
    public int RoundCount { get; set; }
    public List<Participants> Participants { get; set; }
    public List<Rounds> Rounds { get; set; }
}
public class Participants : TableModel
{
    public string Name { set; get; } = null!;
    public int Score { get; set; }
}

public class Rounds : TableModel
{
    [TableComboKey]
    public List<Matches> Matches { get; set; } = [];
    [TableComboKey]
    public List<Teams> Teams { get; set; } = [];
}

public class Matches : TableModel
{
    public int MatchNumber { get; set; }
    public int RoundNumber { get; set; }
    public bool IsCompleted { get; set; }


    [TableForeignKey]
    public Teams? Team1 { get; set; }
    public int Team1Score { get; set; }
    [TableForeignKey]
    public Teams? Team2 { get; set; }
    public int Team2Score { get; set; }
}

public class Teams : TableModel
{
    public int Score { set; get; }
    [TableForeignKey]
    public Participants? Player1 { get; set; }
    [TableForeignKey]
    public Participants? Player2 { get; set; }
}

public class Users : TableModel
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Locale { get; set; } = null!;
    public string Roles { get; set; } = null!;
}
