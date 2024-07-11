# TableContext

Library for interacting with azure storage account tables in a relational way.

Setup:
Direct
``` C#
var tableStorage = new TableStorage();
tableStorage
    .ConfigureConnectionString("Connstr") // or .ConfigureTokenCredential()
    .RegisterTable<Root>() // Register all Models
    .RegisterTable<Base>()
    .RegisterTable<Trunk>()
    .RegisterTable<Branch>()
    .RegisterTable<Leaf>();
```

Register DI Service
``` C#
    services.AddTableStorage(c =>
        c.ConfigureConnectionString("Connstr")
        .AddTable<Root>()
        .AddTable<Base>()
        .AddTable<Trunk>()
        .AddTable<Branch>()
        .AddTable<Leaf>());
```

Models must inherit from the `TableModel` class.

- The `TableForeignKey` Attribute is used to genererate a foreignkey field when saving and reading the table.
- The `TableComboKey` Attribute makes the child-object's partitionkey a combination of this objects partitionKey and Id (RowKey).
- The `TableIgnore` Attribute is used to ignore a property when saving and reading from the table.
- The `TableParent` Attribute tries to set the property to the parent of this object.
- The `TableJson` Attribute is used to serialize/deserialize the value as json when reading and writing to the table.

Example:
``` C#
public class Root : TableModel
{
    [TableForeignKey("MyCustomBaseId")]
    public required Base Base { get; set; }
    public double SomeNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [TableIgnore]
    public List<Branch> Branches { get; set; } = [];
}

public class Base : TableModel
{
    [TableComboKey]
    public List<Branch> Branches { get; set; } = [];
    [TableParent]
    public Root? Root { get; set; }
}

public class Branch : TableModel
{
    [TableParent]
    public Base? Base { get; set; }
}
```

To save/query/delete:
``` C#
var allTrees = await tableStorage.QueryAsync<Root>(""); //Returns IEnumerable
var myTree = await tableStorage.QueryAsync<Root>("RowKey eq 'myTree'"); //Returns IEnumerable
await tableStorage.Save(newTree);
await tableStorage.Save(manyNewTrees); //Array
await tableStorage.Delete(badTree);
await tableStorage.Delete(allBadTrees); //Array
``` 
