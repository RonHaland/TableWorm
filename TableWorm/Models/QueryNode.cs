namespace TableWorm.Models;

internal class QueryNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public string? QueryString { get; set; }
    public List<QueryNode> SubQueries { get; set; } = new();
    public Type? TableModelType { get; set; }
    public Guid? SubQueryId { get; set; }

    public QueryNode(string? queryString = null)
    {
        QueryString = queryString;
    }
}