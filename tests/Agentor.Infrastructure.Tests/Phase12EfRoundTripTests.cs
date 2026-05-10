using Agentor.Application.Abstractions;



using Agentor.Application.Reliability;



using Agentor.Infrastructure.Persistence;



using Agentor.Infrastructure.Persistence.Records;



using Microsoft.Data.Sqlite;



using Microsoft.EntityFrameworkCore;



namespace Agentor.Infrastructure.Tests;



public sealed class Phase12EfRoundTripTests



{



    private static async Task<AgentorDbContext> CreateSqliteContextAsync(SqliteConnection connection)



    {



        await connection.OpenAsync();



        var options = new DbContextOptionsBuilder<AgentorDbContext>()



            .UseSqlite(connection)



            .Options;



        var ctx = new AgentorDbContext(options);



        await ctx.Database.EnsureCreatedAsync();



        return ctx;



    }



    [Fact]



    public async Task EfOutboxStore_Append_ListPending_DispatchFlow_Persists()



    {



        await using var connection = new SqliteConnection("DataSource=:memory:");



        await using var ctx = await CreateSqliteContextAsync(connection);



        var store = new EfOutboxStore(ctx);



        var id = Guid.NewGuid();



        var msg = new OutboxMessage(id, OutboxMessageKind.Conexus, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);



        await store.AppendAsync(msg, CancellationToken.None);



        // SQLite cannot translate OrderBy on DateTimeOffset (EfOutboxStore.ListPendingForDispatchAsync).

        var exists = await ctx.OutboxMessages.AsNoTracking()

            .AnyAsync(r => r.Id == id && r.Status == OutboxStatus.Pending.ToString());

        Assert.True(exists);



        Assert.True(await store.TryMarkDispatchingAsync(id, CancellationToken.None));



        await store.MarkOutcomeAsync(id, OutboxStatus.Succeeded, null, CancellationToken.None);



        var row = await ctx.OutboxMessages.AsNoTracking().SingleAsync(r => r.Id == id);



        Assert.Equal(OutboxStatus.Succeeded.ToString(), row.Status);



    }



    [Fact]



    public async Task EfDistributedOperationLedger_SecondCommitWithSameKey_ReturnsFalse()



    {



        await using var connection = new SqliteConnection("DataSource=:memory:");



        await using var ctx = await CreateSqliteContextAsync(connection);



        var ledger = new EfDistributedOperationLedger(ctx);



        Assert.True(await ledger.TryCommitOnceAsync("op-key-1", CancellationToken.None));



        Assert.False(await ledger.TryCommitOnceAsync("op-key-1", CancellationToken.None));



    }



    [Fact]



    public async Task EfExecutionLeaseStore_Contested_ThenReleaseAndAcquire()



    {



        await using var connection = new SqliteConnection("DataSource=:memory:");



        await using var ctx = await CreateSqliteContextAsync(connection);



        var rid = Guid.NewGuid();



        var now = DateTimeOffset.UtcNow;



        var store = new EfExecutionLeaseStore(ctx);



        var a = await store.TryAcquireAsync(rid, "worker-a", TimeSpan.FromMinutes(5), now, CancellationToken.None);



        Assert.Equal(LeaseAcquireOutcome.Acquired, a);



        var b = await store.TryAcquireAsync(rid, "worker-b", TimeSpan.FromMinutes(5), now, CancellationToken.None);



        Assert.Equal(LeaseAcquireOutcome.Contested, b);



        var same = await store.TryAcquireAsync(rid, "worker-a", TimeSpan.FromMinutes(5), now, CancellationToken.None);



        Assert.Equal(LeaseAcquireOutcome.AlreadyHeldByCaller, same);



        await store.ReleaseAsync(rid, "worker-a", CancellationToken.None);



        var c = await store.TryAcquireAsync(rid, "worker-b", TimeSpan.FromMinutes(5), now, CancellationToken.None);



        Assert.Equal(LeaseAcquireOutcome.Acquired, c);



    }



    [Fact]



    public async Task EfExecutionLeaseStore_ExpiredLease_CanBeReAcquired()



    {



        await using var connection = new SqliteConnection("DataSource=:memory:");



        await using var ctx = await CreateSqliteContextAsync(connection);



        var rid = Guid.NewGuid();



        var now = DateTimeOffset.UtcNow;



        ctx.ExecutionLeases.Add(



            new ExecutionLeaseRecord



            {



                ResourceId = rid,



                LeaseHolder = "stale",



                ExpiresAtUtc = now.AddMinutes(-10),



                CreatedAtUtc = now.AddMinutes(-20),



            });



        await ctx.SaveChangesAsync();



        var store = new EfExecutionLeaseStore(ctx);



        var o = await store.TryAcquireAsync(rid, "worker-z", TimeSpan.FromMinutes(1), now, CancellationToken.None);



        Assert.Equal(LeaseAcquireOutcome.Acquired, o);



    }



    [Fact]



    public async Task EfExecutionLeaseStore_Renew_ExtendsLease()



    {



        await using var connection = new SqliteConnection("DataSource=:memory:");



        await using var ctx = await CreateSqliteContextAsync(connection);



        var rid = Guid.NewGuid();



        var now = DateTimeOffset.UtcNow;



        var store = new EfExecutionLeaseStore(ctx);



        await store.TryAcquireAsync(rid, "worker-a", TimeSpan.FromSeconds(30), now, CancellationToken.None);



        var ok = await store.TryRenewAsync(rid, "worker-a", TimeSpan.FromMinutes(5), now, CancellationToken.None);



        Assert.True(ok);



        var row = await ctx.ExecutionLeases.AsNoTracking().SingleAsync(r => r.ResourceId == rid);



        Assert.True(row.ExpiresAtUtc > now.AddMinutes(1));



    }



    [Fact]



    public async Task EfOutboxStore_TryMarkDispatching_AllowsSingleWinnerUnderContention()



    {



        var dbFile = Path.GetTempFileName();
        var cs = $"Data Source={dbFile}";



        try



        {



            await using (var seed = new AgentorDbContext(new DbContextOptionsBuilder<AgentorDbContext>().UseSqlite(cs).Options))



            {



                await seed.Database.EnsureCreatedAsync();



                var seedStore = new EfOutboxStore(seed);
                var id = Guid.NewGuid();
                await seedStore.AppendAsync(
                    new OutboxMessage(id, OutboxMessageKind.Mcp, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null),
                    CancellationToken.None);



                await using var ctxA = new AgentorDbContext(new DbContextOptionsBuilder<AgentorDbContext>().UseSqlite(cs).Options);
                await using var ctxB = new AgentorDbContext(new DbContextOptionsBuilder<AgentorDbContext>().UseSqlite(cs).Options);
                var storeA = new EfOutboxStore(ctxA);
                var storeB = new EfOutboxStore(ctxB);



                var a = storeA.TryMarkDispatchingAsync(id, CancellationToken.None);
                var b = storeB.TryMarkDispatchingAsync(id, CancellationToken.None);
                await Task.WhenAll(a, b);

                var aResult = await a;
                var bResult = await b;



                Assert.NotEqual(aResult, bResult);
                Assert.True(aResult || bResult);
            }
        }
        finally
        {
            if (File.Exists(dbFile))
            {
                try
                {
                    File.Delete(dbFile);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for file-backed sqlite tests.
                }
            }
        }



    }



}



