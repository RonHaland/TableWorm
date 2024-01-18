using Azure.Data.Tables;
using AzureTableContext.Attributes;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace AzureTableContext;

public partial class TableContext
{
    private TableClient? GetClient<TTableModel>() where TTableModel : TableModel
    {
        return _tableClients.TryGetValue(typeof(TTableModel), out var client ) ? client : null;
    }

    public async Task Save<TTableModel>(params TTableModel[] models) where TTableModel : TableModel
    {
        var entities = models.Distinct().Select(m => m.ConvertToTableEntity()).ToList();
        var childMaps = models.Select(m => m.GetChildren()).ToList();

        var distinctKeys = childMaps.SelectMany(m => m.Keys).Distinct();
        var flattenedDict = new Dictionary<string, List<TableEntity>>();
        foreach (var key in distinctKeys)
        {
            var flattenedList = childMaps.SelectMany(m => m.TryGetValue(key, out var list) ? list : [])
                .DistinctBy(e => new { e.RowKey, e.PartitionKey }).ToList();
            flattenedDict[key] = flattenedList;
        }
        if (flattenedDict.TryGetValue(typeof(TTableModel).Name, out var listOfCurrentType))
        {
            listOfCurrentType.AddRange(entities);
        }
        else
        {
            flattenedDict[typeof(TTableModel).Name] = entities;
        }

        foreach (var pair in flattenedDict)
        {
            var clientKey = _tableClients.Keys.FirstOrDefault(k => k.Name == pair.Key);
            if (clientKey == null)
            {
                throw new InvalidOperationException($"No table registered for type {pair.Key}");
            }

            var upsertTasks = pair.Value.Select(async tableEntities =>
            {
                await _tableClients[clientKey].UpsertEntityAsync(tableEntities);
            });
            await Task.WhenAll(upsertTasks);
        }

        var result = childMaps;
    }

    public async Task Delete<TTableModel>(TTableModel model, int cascadeDepth = 0) where TTableModel : TableModel
    {
        await Delete([model], cascadeDepth);
    }
    public async Task Delete<TTableModel>(IEnumerable<TTableModel> models, int cascadeDepth = 0) where TTableModel : TableModel
    {
        var entities = models.Distinct().Select(m => m.ConvertToTableEntity()).ToList();
        var flattenedDict = new Dictionary<string, List<TableEntity>>();
        if (cascadeDepth > 0) 
        {
            var childMaps = models.Select(m => m.GetChildren(cascadeDepth - 1)).ToList();
            var distinctKeys = childMaps.SelectMany(c => c.Keys).Distinct();

            foreach (var key in distinctKeys)
            {
                var flattenedList = childMaps.SelectMany(m => m.TryGetValue(key, out var list) ? list : [])
                    .DistinctBy(e => new { e.RowKey, e.PartitionKey }).ToList();
                flattenedDict[key] = flattenedList;
            }
        }
        if (flattenedDict.TryGetValue(typeof(TTableModel).Name, out var listOfCurrentType))
        {
            listOfCurrentType.AddRange(entities);
        }
        else
        {
            flattenedDict[typeof(TTableModel).Name] = entities;
        }

        foreach (var pair in flattenedDict)
        {
            var clientKey = _tableClients.Keys.FirstOrDefault(k => k.Name == pair.Key);
            if (clientKey == null)
            {
                throw new InvalidOperationException($"No table registered for type {pair.Key}");
            }

            var upsertTasks = pair.Value.Select(async tableEntity =>
            {
                await _tableClients[clientKey].DeleteEntityAsync(tableEntity.PartitionKey, tableEntity.RowKey);
            });
            await Task.WhenAll(upsertTasks);
        }
    }

    public IEnumerable<TTableModel>? Query<TTableModel>(string query, int maxDepth = 5) where TTableModel : TableModel
    {
        var client = GetClient<TTableModel>();
        if (client == null) return null;

        var result = client.Query<TableEntity>(query);
        var resultList = result.ToList();
        if (maxDepth >= 0)
        {
            var models = ConstructFrom<TTableModel>(resultList, maxDepth);
            return models;
        }
        return [];
    }
    public async Task<IEnumerable<TTableModel>?> QueryAsync<TTableModel>(string query, int maxDepth = 5) where TTableModel : TableModel
    {
        var client = GetClient<TTableModel>();
        if (client == null) return null;

        var result = client.Query<TableEntity>(query);
        var resultList = result.ToList();
        if (maxDepth >= 0)
        {
            var models = await ConstructFromAsync<TTableModel>(resultList, maxDepth);
            return models;
        }
        return [];
    }

    internal List<TTableModel> ConstructFrom<TTableModel>(List<TableEntity> entities, int maxDepth = 5) where TTableModel : TableModel
    {
        var models = entities.Select(e =>
        {
            var model = Activator.CreateInstance<TTableModel>();
            model.Id = e.RowKey;
            model.PartitionKey = e.PartitionKey;
            model.ModifiedDate = e.Timestamp;

            var valueProps = model.DirectTablePropertiesMap.TryGetValue(true, out var directProps) ? directProps : [];

            foreach (var prop in valueProps)
            {
                var propValue = e[prop.Name];
                if (propValue == null) { continue; }
                prop.SetValue(model, propValue);
            }
            var foreignKeyProps = model.DirectTablePropertiesMap.TryGetValue(false, out var childProps) 
                                            ? childProps.Where(p => p.GetCustomAttribute<TableForeignKeyAttribute>() != null) 
                                            : [];
            foreach (var prop in foreignKeyProps)
            {
                var key = prop.GetCustomAttribute<TableForeignKeyAttribute>()?.Name ?? prop.Name + "Id";
                var propValue = e[key];
                model._foreignKeys.Add(key, (string)propValue!);
            }

            return model;
        }).ToList();

        if (maxDepth > 0)
        {
            PopulateChildElements(models, maxDepth - 1);
        }

        return models;
    }
    internal async Task<List<TTableModel>> ConstructFromAsync<TTableModel>(List<TableEntity> entities, int maxDepth = 5) where TTableModel : TableModel
    {
        var modelTasks = entities.Select(async e => await Task.Run(() => 
        {
            var model = Activator.CreateInstance<TTableModel>();
            model.Id = e.RowKey;
            model.PartitionKey = e.PartitionKey;
            model.ModifiedDate = e.Timestamp;

            var valueProps = model.DirectTablePropertiesMap.TryGetValue(true, out var directProps) ? directProps : [];

            foreach (var prop in valueProps)
            {
                var propValue = e[prop.Name];
                if (propValue == null) { continue; }
                prop.SetValue(model, propValue);
            }
            if (model.DirectTablePropertiesMap.TryGetValue(false, out var childProps))
            {
                var foreignKeyProps = childProps.Where(p => p.GetCustomAttribute<TableForeignKeyAttribute>() != null);
                foreach (var prop in foreignKeyProps)
                {
                    var key = prop.GetCustomAttribute<TableForeignKeyAttribute>()?.Name ?? prop.Name + "Id";
                    var propValue = e[key];
                    model._foreignKeys.Add(key, (string)propValue!);
                }

                var jsonProps = childProps.Where(p => p.GetCustomAttribute<TableJsonAttribute>() != null);
                foreach (var prop in jsonProps)
                {
                    var propValue = (string)e[prop.Name];
                    var method = typeof(JsonSerializer).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(JsonSerializerOptions)]);
                    var genericMethod = method.MakeGenericMethod([prop.PropertyType]);
                    var deserializedObject = genericMethod.Invoke(null, [propValue, null]);

                    prop.SetValue(model, deserializedObject);
                }
            }
            

            return model;
        })).ToList();

        await Task.WhenAll(modelTasks);
        var models = modelTasks.Select(t => t.Result).ToList();

        if (maxDepth > 0)
        {
            await PopulateChildElementsAsync(models, maxDepth - 1);
        }

        return models;
    }

    private void PopulateChildElements<TTableModel>(List<TTableModel> parents, int maxDepth) where TTableModel : TableModel
    {
        var firstParent = parents.FirstOrDefault();
        if (firstParent == null || !firstParent.DirectTablePropertiesMap.TryGetValue(false, out var childProps)) { return; }

        foreach (var prop in childProps)
        {
            var isCollection = prop.PropertyType.IsAssignableTo(typeof(IEnumerable<TableModel>));
            var foreignKeyProp = prop.GetCustomAttribute<TableForeignKeyAttribute>();
            var propType = isCollection
                ? prop.PropertyType.GetGenericArguments().First(a => a.IsAssignableTo(typeof(TableModel)))
                : prop.PropertyType;

            if (foreignKeyProp != null)
            {
                var foreignKeyField = foreignKeyProp.Name ?? prop.Name + "Id";
                var parentsWithForeignKey = parents.Where(p => p._foreignKeys.ContainsKey(foreignKeyField)).ToList();
                PopulateChildrenByForeignKeys(parentsWithForeignKey, foreignKeyField, prop, maxDepth);
            }
            else
            {
                PopulateChildrenByPartitionKeys(parents, prop, maxDepth);
            }
        }

    }
    private async Task PopulateChildElementsAsync<TTableModel>(List<TTableModel> parents, int maxDepth) where TTableModel : TableModel
    {
        var firstParent = parents.FirstOrDefault();
        if (firstParent == null || !firstParent.DirectTablePropertiesMap.TryGetValue(false, out var childProps)) { return; }

        var childPropsTasks = childProps.Select(async prop =>
        {
            var isCollection = prop.PropertyType.IsAssignableTo(typeof(IEnumerable<TableModel>));
            var foreignKeyProp = prop.GetCustomAttribute<TableForeignKeyAttribute>();
            var propType = isCollection
                ? prop.PropertyType.GetGenericArguments().First(a => a.IsAssignableTo(typeof(TableModel)))
                : prop.PropertyType;

            if (foreignKeyProp != null)
            {
                var foreignKeyField = foreignKeyProp.Name ?? prop.Name + "Id";
                var parentsWithForeignKey = parents.Where(p => p._foreignKeys.ContainsKey(foreignKeyField)).ToList();
                await PopulateChildrenByForeignKeysAsync(parentsWithForeignKey, foreignKeyField, prop, maxDepth);
            }
            else
            {
                await PopulateChildrenByPartitionKeysAsync(parents, prop, maxDepth);
            }
        });

        await Task.WhenAll(childPropsTasks);
    }

    private void PopulateChildrenByForeignKeys<TTableModel>(List<TTableModel> parents, string foreignKeyField, PropertyInfo childProperty, int maxDepth) where TTableModel : TableModel
    {
        if (!_tableClients.TryGetValue(childProperty.PropertyType, out var tableClient)) { return; }

        //get foreignKeys into groups of max 40
        var foreignKeyGroups = parents
                            .Select(p => p._foreignKeys.TryGetValue(foreignKeyField, out var key) ? key : null)
                            .Where(f => !string.IsNullOrWhiteSpace(f))
                            .Select((f,i) => new {Ind = i, Val = f})
                            .GroupBy(g => g.Ind / 40)
                            .Select(g => g.Select(g => g.Val!).ToList());

        List<TableEntity> children = [];
        foreach (var foreignKeyGroup in foreignKeyGroups)
        {
            var querys = foreignKeyGroup.Select(k => $"RowKey eq '{k}'");
            var query = string.Join(" or ", querys);

            var entities = tableClient.Query<TableEntity>(query);
            children.AddRange(entities);
        }

        var constructmethod = GetType().GetMethod(nameof(ConstructFrom), BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(childProperty.PropertyType);
        var models = (IEnumerable<TableModel>)constructmethod!.Invoke(this, [children, maxDepth])!;

        var modelDict = models.GroupBy(m => new { m.Id}).ToDictionary(k => k.Key.Id, v => v.Select(g => g).ToList());
        foreach (var parent in parents)
        {
            var id = parent._foreignKeys.TryGetValue(foreignKeyField, out var key) ? key : null;
            if (modelDict.TryGetValue(key ?? "", out var value))
            {
                childProperty.SetValue(parent, value.FirstOrDefault());
            }
        }
    }
    private async Task PopulateChildrenByForeignKeysAsync<TTableModel>(List<TTableModel> parents, string foreignKeyField, PropertyInfo childProperty, int maxDepth) where TTableModel : TableModel
    {
        if (!_tableClients.TryGetValue(childProperty.PropertyType, out var tableClient)) { return; }

        //get foreignKeys into groups of max 40
        var foreignKeyGroups = parents
                            .Select(p => p._foreignKeys.TryGetValue(foreignKeyField, out var key) ? key : null)
                            .Where(f => !string.IsNullOrWhiteSpace(f))
                            .Select((f, i) => new { Ind = i, Val = f })
                            .GroupBy(g => g.Ind / 40)
                            .Select(g => g.Select(g => g.Val!).ToList());

        var fetchEntitiesTasks = foreignKeyGroups.Select(async foreignKeyGroup => await Task.Run(() =>
        {
            var querys = foreignKeyGroup.Select(k => $"RowKey eq '{k}'");
            var query = string.Join(" or ", querys);

            var entities = tableClient.Query<TableEntity>(query);
            return entities;

        }));
        await Task.WhenAll(fetchEntitiesTasks);

        var children = fetchEntitiesTasks.SelectMany(e => e.Result).ToList();

        var constructmethod = GetType().GetMethod(nameof(ConstructFromAsync), BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(childProperty.PropertyType);
        var constructFromTask = (Task)constructmethod!.Invoke(this, [children, maxDepth])!;

        if (constructFromTask == null) return;

        await constructFromTask.ConfigureAwait(false);
        var resultProperty = constructFromTask.GetType().GetProperty("Result");
        var models = (IEnumerable<TableModel>)resultProperty!.GetValue(constructFromTask)!;

        var modelDict = models.GroupBy(m => new { m.Id }).ToDictionary(k => k.Key.Id, v => v.Select(g => g).ToList());

        var updateParentsTasks = parents.Select(async parent => await Task.Run(() =>
        {
            var id = parent._foreignKeys.TryGetValue(foreignKeyField, out var key) ? key : null;
            if (modelDict.TryGetValue(key ?? "", out var value))
            {
                childProperty.SetValue(parent, value.FirstOrDefault());
                value.FirstOrDefault()?.SetParent(parent);
            }
        }));

        await Task.WhenAll(updateParentsTasks);
    }

    private void PopulateChildrenByPartitionKeys<TTableModel>(List<TTableModel> parents, PropertyInfo childProperty, int maxDepth) where TTableModel : TableModel
    {
        var isCollection = childProperty.PropertyType.IsAssignableTo(typeof(IEnumerable<TableModel>));
        var childType = isCollection
                    ? childProperty.PropertyType.GetGenericArguments().First(g => g.IsAssignableTo(typeof(TableModel)))
                    : childProperty.PropertyType;
        if (!_tableClients.TryGetValue(childType, out var tableClient)) { return; }

        var isComboKey = childProperty.GetCustomAttribute<TableComboKeyAttribute>() != null;

        var childPartitionKeyGroups = parents.Select(p => 
                                    isComboKey ?
                                    $"{p.PartitionKey}_{p.Id}"
                                    : p.Id )
                            .Select((f, i) => new { Ind = i, Val = f })
                            .GroupBy(g => g.Ind / 40)
                            .Select(g => g.Select(g => g.Val!).ToList());

        List<TableEntity> children = [];
        foreach (var childPartitionKeyGroup in childPartitionKeyGroups)
        {
            var querys = childPartitionKeyGroup.Select(k => $"PartitionKey eq '{k}'");
            var query = string.Join(" or ", querys);

            var entities = tableClient.Query<TableEntity>(query);
            children.AddRange(entities);
        }

        var constructmethod = GetType().GetMethod(nameof(ConstructFrom), BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(childType);
        var models = (IEnumerable<TableModel>)constructmethod!.Invoke(this, [children, maxDepth])!;

        var modelDict = models.GroupBy(m => m.PartitionKey).ToDictionary(k => k.Key, v => v.ToList().AsEnumerable());

        foreach (var parent in parents)
        {
            var key = isComboKey ? $"{parent.PartitionKey}_{parent.Id}" : parent.Id;
            if (modelDict.TryGetValue(key, out var list))
            {
                object? childValue = list.FirstOrDefault();
                if (isCollection)
                {
                    var genericListType = childProperty.PropertyType;
                    var enumerableType = typeof(IList<>).MakeGenericType(genericListType.GenericTypeArguments.First());
                    var ctor = genericListType.GetConstructor([]);
                    var newList = ctor?.Invoke([]) as IList;
                    foreach (var item in list)
                    {
                        newList?.Add(item);
                    }
                    childValue = newList;
                }
                childProperty.SetValue(parent, childValue);
            }
        }
    }
    private async Task PopulateChildrenByPartitionKeysAsync<TTableModel>(List<TTableModel> parents, PropertyInfo childProperty, int maxDepth) where TTableModel : TableModel
    {
        var isCollection = childProperty.PropertyType.IsAssignableTo(typeof(IEnumerable<TableModel>));
        var childType = isCollection
                    ? childProperty.PropertyType.GetGenericArguments().First(g => g.IsAssignableTo(typeof(TableModel)))
                    : childProperty.PropertyType;
        if (!_tableClients.TryGetValue(childType, out var tableClient)) { return; }

        var isComboKey = childProperty.GetCustomAttribute<TableComboKeyAttribute>() != null;

        var childPartitionKeyGroups = parents.Select(p =>
                                    isComboKey ?
                                    $"{p.PartitionKey}_{p.Id}"
                                    : p.Id)
                            .Select((f, i) => new { Ind = i, Val = f })
                            .GroupBy(g => g.Ind / 40)
                            .Select(g => g.Select(g => g.Val!).ToList());

        var fetchEntitiesTasks = childPartitionKeyGroups.Select(async childPartitionKeyGroup => await Task.Run(() =>
        {
            var querys = childPartitionKeyGroup.Select(k => $"PartitionKey eq '{k}'");
            var query = string.Join(" or ", querys);

            var entities = tableClient.Query<TableEntity>(query);
            return entities;
        }));
        await Task.WhenAll(fetchEntitiesTasks);
        var children = fetchEntitiesTasks.SelectMany(e => e.Result).ToList();
        var generic = isCollection
            ? childProperty.PropertyType.GetGenericArguments().First(a => a.IsAssignableTo(typeof(TableModel)))
            : childProperty.PropertyType;
        var constructmethod = GetType().GetMethod(nameof(ConstructFromAsync), BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(generic);
        var constructFromTask = (Task)constructmethod!.Invoke(this, [children, maxDepth])!;

        if (constructFromTask == null) return;

        await constructFromTask.ConfigureAwait(false);
        var resultProperty = constructFromTask.GetType().GetProperty("Result");
        var models = (IEnumerable<TableModel>)resultProperty!.GetValue(constructFromTask)!;

        var modelDict = models.GroupBy(m => m.PartitionKey).ToDictionary(k => k.Key, v => v.ToList().AsEnumerable());

        var updateParentsTasks = parents.Select(async parent => await Task.Run(() =>
        {
            var key = isComboKey ? $"{parent.PartitionKey}_{parent.Id}" : parent.Id;
            if (modelDict.TryGetValue(key, out var list))
            {
                object? childValue = list.FirstOrDefault();
                if (isCollection)
                {
                    var genericListType = childProperty.PropertyType;
                    var enumerableType = typeof(IList<>).MakeGenericType(genericListType.GenericTypeArguments.First());
                    var ctor = genericListType.GetConstructor([]);
                    var newList = ctor?.Invoke([]) as IList;
                    foreach (var item in list)
                    {
                        item.SetParent(parent);
                        newList?.Add(item);
                    }
                    childValue = newList;
                }
                else if (childValue != null)
                {
                    ((TableModel)childValue!).SetParent(parent);
                }
                childProperty.SetValue(parent, childValue);
            }
        }));

        await Task.WhenAll(updateParentsTasks);
    }
}