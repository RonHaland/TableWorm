using AzureTableContext.Attributes;

namespace AzureTableContext.Tests.Entities;

[TableName("Roots")]
public class Root : TableModel
{
    [TableForeignKey("MyCustomBaseId")]
    public required Base Base { get; set; }
    public double Number { get; set; }
    public long Hello { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [TableIgnore]
    public List<Branch> Branches { get; set; } = [];
    [TableJson]
    public List<string> Codes { get; set; } = [];
}

[TableName("TreeBases")]
public class Base : TableModel
{
    [TableComboKey]
    public List<Branch> Branches { get; set; } = [];
    [TableParent]
    public Root? Root { get; set; }
    [TableIgnore]
    public bool IsComplete => Root != null;
}

[TableName("Branches")]
public class Branch : TableModel
{
    public List<Leaf> Leafs { get; set; } = [];
    [TableParent]
    public Root? Root { get; set; }
    [TableParent]
    public Base? Base { get; set; }
}

[TableName("Leaves")]
public class Leaf : TableModel
{
    [TableParent]
    public Branch? Branch { get; set; }
}
