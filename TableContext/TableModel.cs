using Azure.Data.Tables;
using AzureTableContext.Attributes;
using Microsoft.VisualBasic;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace AzureTableContext;

public abstract class TableModel
{
    [TableIgnore]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [TableIgnore]
    public string PartitionKey { get; set; } = null!;
    [TableIgnore]
    public DateTimeOffset? ModifiedDate { get; set; } = DateTimeOffset.UtcNow;

    internal readonly Dictionary<string, string> _foreignKeys = new();

    internal Dictionary<bool, List<PropertyInfo>> DirectTablePropertiesMap => GetType()
                        .GetProperties()
                        .Where(p => p.GetCustomAttribute<TableIgnoreAttribute>() == null)
                        .Where(p => p.GetCustomAttribute<TableParentAttribute>() == null)
                        .GroupBy(p => Helper.AllowedTypes.Contains(p.PropertyType))
                        .ToDictionary(k => k.Key, v => v.ToList());
    internal List<PropertyInfo> ParentProps => GetType().GetProperties().Where(p => p.GetCustomAttribute<TableParentAttribute>() != null).ToList();

    internal TableEntity ConvertToTableEntity()
    {
        var entity = new TableEntity(PartitionKey, Id);
        entity.Timestamp = ModifiedDate;
        var valueProps = DirectTablePropertiesMap.TryGetValue(true, out var directProps) ? directProps : [];

        foreach (var prop in valueProps)
        {
            entity.Add(prop.Name, prop.GetValue(this));
        }
        if (!DirectTablePropertiesMap.TryGetValue(false, out List<PropertyInfo>? childProperties)) { return entity; }

        var foreignKeyProps = childProperties.Where(c => c.GetCustomAttribute<TableForeignKeyAttribute>() != null);
        foreach (var prop in foreignKeyProps)
        {
            var attribute = prop.GetCustomAttribute<TableForeignKeyAttribute>()!;
            var foreignKeyName = attribute.Name ?? prop.Name + "Id";
            //_foreignKeys.Add(foreignKeyName, ((TableModel)prop.GetValue(this)!).Id);
            entity.Add(foreignKeyName, ((TableModel)prop.GetValue(this)!).Id);
        }

        var jsonProps = childProperties.Where(c => c.GetCustomAttribute<TableJsonAttribute>() != null);
        foreach (var prop in jsonProps)
        { 
            var value = prop.GetValue(this);
            var jsonString = JsonSerializer.Serialize(value);
            entity.Add(prop.Name, jsonString);
        }

        return entity;
    }

    internal Dictionary<string, List<TableEntity>> GetChildren(int maxDepth = 5)
    {
        var result = new Dictionary<string, List<TableEntity>>();
        if (!DirectTablePropertiesMap.TryGetValue(false, out List<PropertyInfo>? childProperties)) { return result; }

        var childProps = childProperties.Where(p => p.PropertyType.IsAssignableTo(typeof(TableModel)) || p.PropertyType.IsAssignableTo(typeof(IEnumerable<TableModel>)));
        foreach (var prop in childProps)
        {
            var isCollection = prop.PropertyType.IsAssignableTo(typeof(IEnumerable));
            var isForeignKeyProp = prop.GetCustomAttribute<TableForeignKeyAttribute>() != null;
            var isComboKey = prop.GetCustomAttribute<TableComboKeyAttribute>() != null;
            var values = isCollection ? (IEnumerable<TableModel>)prop.GetValue(this)! : [(TableModel)prop.GetValue(this)!];
            foreach (var value in values)
            {
                if (!isForeignKeyProp) { value.EnsureConnected(this, isComboKey); }
                var valueTableEntity = value.ConvertToTableEntity();
                var valueTableName = value.GetType().Name;
                if (result.TryGetValue(valueTableName, out List<TableEntity>? valueTableList))
                {
                    valueTableList.Add(valueTableEntity);
                }
                else
                {
                    result[valueTableName] = [valueTableEntity];
                }

                if (maxDepth < 1) { continue; }
                var valueChildren = value.GetChildren(maxDepth - 1);
                foreach (var child in valueChildren)
                {
                    if (result.TryGetValue(child.Key, out List<TableEntity>? resultList))
                    {
                        resultList.AddRange(child.Value);
                        continue;
                    }
                    result[child.Key] = child.Value;
                }
            }
        }

        return result;
    }

    internal void EnsureConnected(TableModel parent, bool isComboKey = false)
    {
        PartitionKey = parent.Id;
        if (isComboKey)
        {
            PartitionKey = $"{parent.PartitionKey}_{parent.Id}";
        }
    }

    internal void SetParent(TableModel potentialParent)
    {
        var parentType = potentialParent.GetType();
        var propsToAssign = ParentProps.Where(p => p.PropertyType.IsAssignableTo(parentType) && p.GetValue(this) == null);

        foreach (var prop in propsToAssign)
        {
            prop.SetValue(this, potentialParent);
        }
    }
}
