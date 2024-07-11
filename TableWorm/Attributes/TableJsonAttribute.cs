namespace TableWorm.Attributes;

/// <summary>
/// Marks the property for JsonSerialization and storing it as plain text in the table
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TableJsonAttribute : Attribute
{
}
