namespace AzureTableContext.Attributes;

/// <summary>
/// Marks the property to be ignored when saving or reading from tables
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TableIgnoreAttribute : Attribute
{
}
