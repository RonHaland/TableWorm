using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using TableWorm.Attributes;
using TableWorm.Tests.Entities;

namespace TableWorm.Tests;


public class IntegrationTests
{
    private static string LocalConnectionString =>
        "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";

    private TableStorage Configure()
    {
        var ctx = new TableStorage()
            .ConfigureLocal()
            .RegisterTable<Root>()
            .RegisterTable<Base>()
            .RegisterTable<Branch>()
            .RegisterTable<Leaf>();
        return ctx;
    }

    private TableStorage TestConfigType1()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTableStorage(LocalConnectionString, c =>
            c.AddTable<Root>()
                .AddTable<Base>()
                .AddTable<Branch>()
                .AddTable<Leaf>());

        return (TableStorage)services.First(s => s.ImplementationInstance!.GetType() == typeof(TableStorage))
            .ImplementationInstance!;
    }


    private TableStorage TestConfigType2()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTableStorage(c =>
            c.ConfigureConnectionString(LocalConnectionString)
                .AddTable<Root>()
                .AddTable<Base>()
                .AddTable<Branch>());

        return (TableStorage)services.First(s => s.ImplementationInstance!.GetType() == typeof(TableStorage))
            .ImplementationInstance!;
    }

    private async Task ClearAll()
    {
        var ctx = Configure();

        var allRoot = ctx.Query<Root>("RowKey ge ''");
        await ctx.DeleteAsync(allRoot ?? [], 5);

        var allBase = ctx.Query<Base>("RowKey ge ''");
        await ctx.DeleteAsync(allBase ?? [], 5);

        var allBranch = ctx.Query<Branch>("RowKey ge ''");
        await ctx.DeleteAsync(allBranch ?? [], 5);

        var allLeaf = ctx.Query<Leaf>("RowKey ge ''");
        await ctx.DeleteAsync(allLeaf ?? [], 5);
    }

    [Fact]
    public async Task TestGet_FindsOne()
    {
        await ClearAll();
        var ctx = Configure();

        var root = new Root
        {
            Id = "one",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        await ctx.Save(root);
        var tree = ctx.Get<Root>("one");
        Assert.NotNull(tree);
        Assert.Equal("one", tree.Id);
        Assert.NotNull(tree.Base);
        Assert.NotEmpty(tree.Base.Branches);
        Assert.Equal(2, tree.Base.Branches.Count);
    }

    [Fact]
    public async Task TestConfig1_FindsOne()
    {
        await ClearAll();
        var ctx = TestConfigType1();

        var root = new Root
        {
            Id = "one",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        await ctx.Save(root);
        var tree = ctx.Get<Root>("one");
        Assert.NotNull(tree);
        Assert.Equal("one", tree.Id);
        Assert.NotNull(tree.Base);
        Assert.NotEmpty(tree.Base.Branches);
        Assert.Equal(2, tree.Base.Branches.Count);
    }

    [Fact]
    public async Task TestConfig2_FindsOne()
    {
        await ClearAll();
        var ctx = TestConfigType2();

        var root = new Root
        {
            Id = "one",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        await ctx.Save(root);
        var tree = ctx.Get<Root>("one");
        Assert.NotNull(tree);
        Assert.Equal("one", tree.Id);
        Assert.NotNull(tree.Base);
        Assert.NotEmpty(tree.Base.Branches);
        Assert.Equal(2, tree.Base.Branches.Count);
    }

    [Fact]
    public async Task TestGet_FindsOneOfTwo()
    {
        await ClearAll();
        var ctx = Configure();

        var root = new Root
        {
            Id = "one",
            PartitionKey = "a",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        var root2 = new Root
        {
            Id = "one",
            PartitionKey = "b",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        await ctx.Save(root, root2);
        var tree = ctx.Get<Root>("one", "a");
        Assert.NotNull(tree);
        Assert.Equal("one", tree.Id);
        Assert.NotNull(tree.Base);
        Assert.NotEmpty(tree.Base.Branches);
        Assert.Equal(2, tree.Base.Branches.Count);
    }

    [Fact]
    public async Task TestQuery_AsyncAndSyncReturnsSame()
    {
        await ClearAll();
        var ctx = Configure();

        var root1 = new Root
        {
            Id = "one",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        var root2 = new Root
        {
            Id = "two",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        var root3 = new Root
        {
            Id = "three",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        await ctx.Save(root1, root2, root3);
        var treeAsync = await ctx.QueryAsync<Root>("");
        var tree = ctx.Query<Root>("");

        Assert.NotNull(tree);
        Assert.NotNull(treeAsync);
        var treeArray = tree as Root[] ?? tree.ToArray();
        var asyncTreeArray = treeAsync as Root[] ?? treeAsync.ToArray();
        Assert.Equal(treeArray.First().Id, asyncTreeArray.First().Id);
        Assert.Equal(treeArray.Last().Id, asyncTreeArray.Last().Id);
        Assert.Equal(treeArray.Length, asyncTreeArray.Length);
    }

    [Fact]
    public async Task TestDelete_RemovesOne()
    {
        var root1 = new Root
        {
            Id = "one",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        var root2 = new Root
        {
            Id = "two",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };
        var root3 = new Root
        {
            Id = "three",
            PartitionKey = "",
            Base = new Base
            {
                PartitionKey = "",
                Branches =
                [
                    new Branch { PartitionKey = "" },
                    new Branch { PartitionKey = "" },
                ]
            }
        };

        var ctx = Configure();

        await ctx.Save(root1, root2, root3);

        var allTrees = (await ctx.QueryAsync<Root>("", 0))!.ToList();

        await ctx.DeleteAsync(root1, 5);

        var allTreesAfter = (await ctx.QueryAsync<Root>("", 0))!.ToList();

        Assert.NotEmpty(allTrees);
        Assert.NotEmpty(allTreesAfter);
        Assert.Equal(allTrees.Count - 1, allTreesAfter.Count);
    }

    [Fact]
    public async Task TestLambdaQuery_ShouldFindBoth()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = -1,
            Id = "a",
            PartitionKey = "tree1",
        };
        var tree2 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = 1,
            Id = "b",
            PartitionKey = "tree2",
        };
        await ctx.Save(tree1, tree2);

        var result = ctx.Query<Root>(v => v.PartitionKey == "tree2" && v.Hello > 0 || v.Id == "a")?.ToArray();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.Id == "a");
        Assert.Contains(result, r => r.Id == "b");

        await ctx.DeleteAsync(result, 1);
    }

    [Fact]
    public async Task TestLambdaQueryAsync_ShouldFindBoth()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = -1,
            Id = "a",
            PartitionKey = "tree1",
        };
        var tree2 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = 1,
            Id = "b",
            PartitionKey = "tree2",
        };
        await ctx.Save(tree1, tree2);

        var result = await ctx.QueryAsync<Root>(v => v.PartitionKey == "tree2" && v.Hello > 0 || v.Id == "a");

        Assert.NotNull(result);
        var resultArray = result as Root[] ?? result.ToArray();
        Assert.NotEmpty(resultArray);
        Assert.Contains(resultArray, r => r.Id == "a");
        Assert.Contains(resultArray, r => r.Id == "b");

        await ctx.DeleteAsync(resultArray, 1);
    }

    [Fact]
    public async Task TestLambdaQuery_ShouldFindOne()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = -1,
            Id = "a",
            PartitionKey = "tree1",
            Codes = ["one", "two", "three"],
        };
        var tree2 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = -1,
            Id = "b",
            PartitionKey = "tree2",
            Codes = ["four", "five", "six"],
        };
        await ctx.Save(tree1, tree2);

        var result = ctx.Query<Root>(v => v.PartitionKey == "tree2" && v.Hello > 0 || v.Id == "a");

        Assert.NotNull(result);
        var resultArray = result as Root[] ?? result.ToArray();
        Assert.NotEmpty(resultArray);
        Assert.Contains(resultArray, r => r.Id == "a");
        Assert.Equal(3, resultArray.SelectMany(s => s.Codes).Count());

        await ctx.DeleteAsync(resultArray, 1);
    }

    [Fact]
    public async Task TestLambdaQuery_ShouldFindNone()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = 1,
            Id = "a",
            PartitionKey = "tree1",
        };
        var tree2 = new Root
        {
            Base = new() { PartitionKey = "" },
            Hello = -1,
            Id = "b",
            PartitionKey = "tree2",
        };
        await ctx.Save(tree1, tree2);

        var result = ctx.Query<Root>(v => v.PartitionKey == "tree2" && (v.Hello > 0 || v.Id == "a"));

        Assert.NotNull(result);
        Assert.Empty(result);
    }


    [Fact]
    public async Task TestLambdaQuery_TestCreatedDate()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new Base { PartitionKey = "" },
            Hello = 1,
            Id = "a",
            PartitionKey = "tree1",
        };
        var tree2 = new Root
        {
            Base = new Base { PartitionKey = "" },
            Hello = -1,
            Id = "b",
            PartitionKey = "tree2",
        };
        await ctx.Save(tree1, tree2);

        var result = ctx.Query<Root>(v => v.CreatedAt < DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task TestLambdaQuery_UsingParamValues()
    {
        await ClearAll();
        var ctx = Configure();

        var tree1 = new Root
        {
            Base = new Base { PartitionKey = "" },
            Hello = 1,
            Id = "a",
            PartitionKey = "tree1",
        };
        var tree2 = new Root
        {
            Base = new Base { PartitionKey = "" },
            Hello = -1,
            Id = "b",
            PartitionKey = "tree2",
        };
        await ctx.Save(tree1, tree2);

        var id = "a";
        var pk = "tree1";

        var tree = ctx.Query<Root>(r => r.Id == id && pk == r.PartitionKey)?.First();
        Assert.NotNull(tree);
        Assert.Equal("a", tree.Id);
    }
    
    private class TestParent : TableModel
    {
        public TestChild[] Children { get; set; }
    }

    private class TestChild : TableModel
    {
        [TableParent]
        public TestParent Parent { get; set; }
    };

    [Fact]
    public async Task TestDefaultSetup()
    {
        await ClearAll();
        var ctx = Configure().RegisterTable<TestParent>().RegisterTable<TestChild>();
        var testCase = new TestParent
        {
            PartitionKey = "test",
            Children = [new TestChild()],
        };
        
        await ctx.Save(testCase);
        var result = await ctx.QueryAsync<TestParent>(v => v.PartitionKey == "test");

        await ctx.DeleteAsync(result ?? [], 1);
    }

}
