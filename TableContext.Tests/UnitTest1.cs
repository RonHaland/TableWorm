using AzureTableContext.Tests.Entities;

namespace AzureTableContext.Tests;

public class UnitTest1
{
    [Fact]
    public async Task FoozTest()
    {
        var localTableContext = new TableContext();
        localTableContext
            .ConfigureLocal()
            .RegisterTable<Teams>()
            .RegisterTable<Participants>()
            .RegisterTable<Tournaments>()
            .RegisterTable<Rounds>()
            .RegisterTable<Matches>()
            .RegisterTable<Users>();

        var tournaments = localTableContext.Query<Tournaments>("RowKey eq '145767d9-f608-4679-90c1-7b6d7be9b60d'");
        var tournament = tournaments?.FirstOrDefault()!;
        tournament.Id = Guid.NewGuid().ToString();
        tournament.Name = "Custom Tournament with new Library";

        var users = localTableContext.Query<Users>("");

        var foozAppTableContext = new TableContext()
            .ConfigureConnectionString("")
            .RegisterTable<Teams>()
            .RegisterTable<Participants>()
            .RegisterTable<Tournaments>()
            .RegisterTable<Rounds>()
            .RegisterTable<Matches>()
            .RegisterTable<Users>();

        await foozAppTableContext.Save(tournament);
        await foozAppTableContext.Save(users?.ToArray() ?? []);

        var newTournament = foozAppTableContext.Query<Tournaments>($"RowKey eq '{tournament.Id}'");
    }

    [Fact]
    public void TestQuery()
    {
        var testTableContext = new TableContext();
        testTableContext
            .ConfigureLocal()
            .RegisterTable<Root>()
            .RegisterTable<Base>()
            .RegisterTable<Branch>()
            .RegisterTable<Leaf>();

        var tree = testTableContext.Query<Root>("");
    }

    [Fact]
    public async Task TestQueryAsync()
    {
        var testTableContext = new TableContext();
        testTableContext
            .ConfigureLocal()
            .RegisterTable<Root>()
            .RegisterTable<Base>()
            .RegisterTable<Branch>()
            .RegisterTable<Leaf>();

        var tree = await testTableContext.QueryAsync<Root>("");
    }

    [Fact]
    public async Task ModelConversion()
    {
        var tree = new Root
        {
            Id = "myTree",
            PartitionKey = "tree",
            Branches = [
                new Branch() { Id = "firstBranch", PartitionKey = "myTree" },
                new Branch() { Id = "secondBranch", PartitionKey = "myTree" },
            ],
            Base = new()
            {
                Id = "Base",
                PartitionKey = "TreeBase",
                Branches = [
                        new Branch() { Id = "thirdBranch", PartitionKey = "Trunk" },
                    new Branch()
                    {
                        Id = "fourthBranch",
                        PartitionKey = "Trunk",
                        Leafs = [
                            new Leaf() { Id = "firstLeaf", PartitionKey = "fourthBranch" },
                            new Leaf() { Id = "secondLeaf", PartitionKey = "fourthBranch" }
                            ]
                    },
                ]
            }
        };

        var tableContext = new TableContext();
        tableContext.RegisterTable<Root>()
            .RegisterTable<Base>()
            .RegisterTable<Branch>()
            .RegisterTable<Leaf>();
        await tableContext.Save(tree, tree, tree);
    }


    [Fact]
    public async Task DeleteModel()
    {
        var tree = new Root
        {
            Id = "forDelete",
            PartitionKey = "tree",
            Codes = ["aaa", "bbb", "\\\\"],
            Branches = [
                new Branch() { PartitionKey = "test" },
                new Branch() { PartitionKey = "test" },
            ],
            Base = new()
            {
                PartitionKey = "test",
                Branches = [
                        new Branch() { PartitionKey = "test" },
                    new Branch()
                    {
                        PartitionKey = "test",
                        Leafs = [
                                new Leaf() { PartitionKey = "test" },
                            new Leaf() { PartitionKey = "test" }
                            ]
                    },
                ]
            }
        };

        var tableContext = new TableContext();
        tableContext
            .ConfigureLocal()
            .RegisterTable<Root>()
            .RegisterTable<Base>()
            .RegisterTable<Branch>()
            .RegisterTable<Leaf>();

        await tableContext.Save(tree);

        var loadedTree = await tableContext.QueryAsync<Root>("RowKey eq 'forDelete'", 5);

        await tableContext.Delete(loadedTree ?? [], 5);

        var emptyTree = await tableContext.QueryAsync<Root>("RowKey eq 'forDelete'", 5);

        Assert.Empty(emptyTree ?? []);
    }

    [Fact]
    public async Task TeamsTest()
    {
        var names = Enumerable.Range(1, 3).Select(n => n.ToString());
        var counters = names.OrderBy(n => Random.Shared.Next(100)).ToDictionary(k => k, v => 0);

        var matches = new List<List<string>>();

        while (counters.Max(c => c.Value) < 4 || counters.Any(c => c.Value != counters.First().Value))
        {
            if (counters.Any(c => c.Value > 100)) break;
            var selected = counters.OrderBy(c => c.Value).Take(4);
            matches.Add(selected.Select(s => s.Key).ToList());
            foreach (var item in selected)
            {
                counters[item.Key]++;
            }
        }

        var options = Enumerable.Range(4, 25).Select(n => new { n, Options = getOptions(n) }).ToDictionary(k => k.n, v => v.Options);
    }

    private List<int> getOptions(int count, int max = 100) => Enumerable.Range(4, max).Where(n => count * n % 4 == 0).Select(n => n * count / 4).ToList();
}