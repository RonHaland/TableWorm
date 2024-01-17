using AzureTableContext;
using Ronhaland.TableContext.Attributes;

namespace AzureTableContext.Tests.Entities;


public abstract class CustomTableModel : TableModel
{
    public CustomTableModel()
    {
        Id = Guid.NewGuid().ToString();
        PartitionKey = "";
    }
}

public class Root : TableModel
{
    [TableForeignKey("MyCustomBaseId")]
    public required Base Base { get; set; }
    public double Number { get; set; }
    public long Hello { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [TableIgnore]
    public List<Branch> Branches { get; set; } = [];
}

public class Base : CustomTableModel
{
    [TableComboKey]
    public List<Branch> Branches { get; set; } = [];
    [TableParent]
    public Root? Root { get; set; }
}

public class Trunk : CustomTableModel
{
    public List<Branch> Branches { get; set; } = [];
    [TableParent]
    public Base? Base { get; set; }
}

public class Branch : CustomTableModel
{
    public List<Leaf> Leafs { get; set; } = [];
    [TableParent]
    public Root? Root { get; set; }
    [TableParent]
    public Trunk? Trunk { get; set; }
}

public class Leaf : CustomTableModel
{
    [TableParent]
    public Branch? Branch { get; set; }
}
