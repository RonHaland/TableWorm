namespace TableWorm.Attributes;

/// <summary>
/// Marks the property for linking as parent <br />
/// Ignore the property during save and delete, but try to populate during query
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TableParentAttribute : Attribute
{
}
