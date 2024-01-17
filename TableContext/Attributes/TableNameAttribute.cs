namespace AzureTableContext.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TableNameAttribute : Attribute
{
    public string Name { get; set; }
    public TableNameAttribute(string name)
    {
        Name = name;
    }
}
