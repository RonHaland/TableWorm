namespace TableContext;

internal static class Helper
{
    internal static Type[] AllowedTypes =>
    [
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(uint),
        typeof(ulong),
        typeof(ushort),
        typeof(bool),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
    ];
    internal static string LocalConnectionString => "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
}
