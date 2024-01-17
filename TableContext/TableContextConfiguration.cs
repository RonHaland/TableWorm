using Azure.Core;
using Azure.Data.Tables;
using AzureTableContext.Attributes;
using System.Reflection;

namespace AzureTableContext;

public partial class TableContext
{
    private readonly Dictionary<Type, TableClient> _tableClients = new();
    private string? _connectionString = null;
    private string? _endpoint = null;
    private TokenCredential? _tokenCredential = null;
    private TableClientOptions? _tableOptions = null;


    public TableContext RegisterTable<TTableModel>() where TTableModel : TableModel
    {
        var nameAttribute = typeof(TTableModel).GetCustomAttribute<TableNameAttribute>();
        var tableName = nameAttribute != null ? nameAttribute.Name : typeof(TTableModel).Name;
        var client = CreateClient(tableName);
        client.CreateIfNotExists();
        if (_tableClients.TryAdd(typeof(TTableModel), client))
        {
            return this;
        }

        throw new InvalidOperationException("Unable to register the table because the type is already registered");
    }

    private TableClient CreateClient(string tableName)
    {
        if (_tokenCredential != null && !string.IsNullOrWhiteSpace(_endpoint))
        {
            return new TableClient(new Uri(_endpoint), tableName, _tokenCredential, _tableOptions);
        }

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            return new TableClient(_connectionString, tableName);
        }

        throw new InvalidOperationException("Invalid Configuration, make sure to call ConfigureConnectionString or ConfigureTokenCredential before registering tables");
    }

    public TableContext ConfigureLocal()
    {
        return ConfigureConnectionString(Helper.LocalConnectionString);
    }

    public TableContext ConfigureConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        _tokenCredential = null;
        _endpoint = null;
        return this;
    }

    public TableContext ConfigureTokenCredential(TokenCredential tokenCredential, string endpoint)
    {
        _connectionString = null;
        _endpoint = endpoint;
        _tokenCredential = tokenCredential;
        return this;
    }
}
