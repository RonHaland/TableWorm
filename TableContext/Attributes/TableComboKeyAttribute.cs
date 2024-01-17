namespace AzureTableContext.Attributes;

/// <summary>
/// Marks the property for linking through a combination of the parents <strong>PartitionKey</strong> and <strong>RowKey</strong><br />
/// <i>PartionKey_RowKey</i>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TableComboKeyAttribute : Attribute
{
}
