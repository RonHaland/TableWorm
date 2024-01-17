namespace AzureTableContext.Attributes;
/// <summary>
/// Marks that the property should be referenced through a 
/// foreign key on this object rather than using the partitionkey on the child
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TableForeignKeyAttribute : Attribute
{
    public string? Name { get; set; }

    /// <param name="name">Name to use for the foreignkey field, default is the name of property + Id</param>
    public TableForeignKeyAttribute(string? name = null)
    {
        Name = name;
    }
}
