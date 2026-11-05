using System.Security.Claims;
using Infrastructure.Recommendations;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UseCase.Recommendations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PersonalizationInteractionTests : IDisposable
{
    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool IsAvailable => true; public string Id => "test-session"; public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear(); public Task CommitAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken token = default) => Task.CompletedTask; public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
    private sealed class TestSessionFeature : ISessionFeature { public ISession Session { get; set; } = new TestSession(); }
    private sealed class Catalog : IRecommendationCatalog
    {
        public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken token = default) => Task.FromResult(new List<RecommendationProduct> {
            new() { Id=1, Name="Mũ bảo hiểm", Category="Bảo hộ", Brand="GIVI", Tags="mũ bảo hiểm" }
        });
        public Task<List<VehicleOption>> VehiclesAsync(CancellationToken token = default) => Task.FromResult(new List<VehicleOption>());
    }
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ShopContext db;
    private readonly DefaultHttpContext http = new();
    private readonly RecommendationInteractionService service;
    public PersonalizationInteractionTests()
    {
        connection.Open(); db = new ShopContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Roles.Add(new Role {Id=1,Name="User"});
        db.Users.AddRange(new User {Id=1,Username="one",Password="test",DisplayName="one",Email="one@example.invalid",RoleId=1},
                          new User {Id=2,Username="two",Password="test",DisplayName="two",Email="two@example.invalid",RoleId=1});
        db.Categories.Add(new Category {Id=1,Name="Bảo hộ"});
        db.Products.Add(new Product {Id=1,Name="Mũ bảo hiểm",Brand="GIVI",CategoryId=1,Description="",ImageUrl="",Quantity=5,Price=100});
        db.VehicleModels.Add(new VehicleModel {Id=1,Brand="Honda",Model="SH",Variant="125",Market="VN"});
        db.SaveChanges(); http.Features.Set<ISessionFeature>(new TestSessionFeature());
        service = new RecommendationInteractionService(db, new HttpContextAccessor {HttpContext=http}, new Catalog());
    }
    private void SignIn(int id, string role="User") => http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Sid,id.ToString()),new Claim(ClaimTypes.Role,role)],"fixture"));
    [Fact] public async Task GuestDoesNotRecordBeforeConsent() { await service.RecordViewAsync(1,"t1"); Assert.False((await service.GetAsync()).Enabled); Assert.Empty((await service.GetAsync()).Events); }
    [Fact] public async Task GuestConsentRecordsView() { await service.SetEnabledAsync(true); await service.RecordViewAsync(1,"t1"); Assert.Single((await service.GetAsync()).Events); }
    [Fact] public async Task GuestReplayOfSameTicketDoesNotDuplicate() { await service.SetEnabledAsync(true); await service.RecordViewAsync(1,"same"); await service.RecordViewAsync(1,"same"); Assert.Single((await service.GetAsync()).Events); }
    [Fact] public async Task GuestViewIsCappedAtThree() { await service.SetEnabledAsync(true); foreach(var i in Enumerable.Range(0,6)) await service.RecordViewAsync(1,$"ticket{i}"); Assert.Equal(3,(await service.GetAsync()).Events.Count); }
    [Fact] public async Task GuestDisableStopsCollection() { await service.SetEnabledAsync(true); await service.RecordCartAsync(1); await service.SetEnabledAsync(false); await service.RecordCartAsync(1); Assert.Empty((await service.GetAsync()).Events); await service.SetEnabledAsync(true); Assert.Single((await service.GetAsync()).Events); }
    [Fact] public async Task GuestClearKeepsConsentButDeletesHistoryAndVehicle() { await service.SetEnabledAsync(true); await service.SetVehicleAsync(1,2022); await service.RecordCartAsync(1); await service.ClearAsync(); var p=await service.GetAsync(); Assert.True(p.Enabled); Assert.Null(p.VehicleModelId); Assert.Empty(p.Events); }
    [Fact] public async Task GuestClearOnIdentityTransitionDoesNotMerge() { await service.SetEnabledAsync(true); await service.RecordCartAsync(1); service.ClearGuest(); SignIn(1); var p=await service.GetAsync(); Assert.False(p.Enabled); Assert.Empty(p.Events); }
    [Fact] public async Task AccountDoesNotRecordBeforeConsent() { SignIn(1); await service.RecordCartAsync(1); Assert.Empty(await db.RecommendationEvents.ToListAsync()); }
    [Fact] public async Task AccountEventsAreIsolated() { SignIn(1); await service.SetEnabledAsync(true); await service.RecordCartAsync(1); SignIn(2); await service.SetEnabledAsync(true); Assert.Empty((await service.GetAsync()).Events); SignIn(1); Assert.Single((await service.GetAsync()).Events); }
    [Fact] public async Task AccountClearDeletesEventsButKeepsOptIn() { SignIn(1); await service.SetEnabledAsync(true); await service.SetVehicleAsync(1,2022); await service.RecordCartAsync(1); await service.ClearAsync(); var p=await service.GetAsync(); Assert.True(p.Enabled); Assert.Null(p.VehicleModelId); Assert.Empty(p.Events); }
    [Fact] public async Task AccountDisableAndEnablePreservesEvents() { SignIn(1); await service.SetEnabledAsync(true); await service.RecordCartAsync(1); await service.SetEnabledAsync(false); await service.RecordCartAsync(1); Assert.Empty((await service.GetAsync()).Events); await service.SetEnabledAsync(true); Assert.Single((await service.GetAsync()).Events); }
    [Fact] public async Task AccountViewReplayDoesNotDuplicate() { SignIn(1); await service.SetEnabledAsync(true); await service.RecordViewAsync(1,"same"); await service.RecordViewAsync(1,"same"); Assert.Single(await db.RecommendationEvents.ToListAsync()); }
    [Fact] public async Task AccountCartIsCappedAtThree() { SignIn(1); await service.SetEnabledAsync(true); for(var i=0;i<6;i++) await service.RecordCartAsync(1); Assert.Equal(3,await db.RecommendationEvents.CountAsync()); }
    [Fact] public async Task ExpiredAccountEventsAreNotRead() { SignIn(1); await service.SetEnabledAsync(true); db.RecommendationEvents.Add(new RecommendationEvent {UserId=1,Kind="view",ProductId=1,At=DateTimeOffset.UtcNow.AddDays(-91),DedupeKey="old"}); await db.SaveChangesAsync(); Assert.Empty((await service.GetAsync()).Events); }
    [Fact] public async Task SearchStoresCatalogTermsOnlyAndDeduplicates() { SignIn(1); await service.SetEnabledAsync(true); await service.RecordSearchAsync("mũ bảo hiểm secret@example.invalid"); await service.RecordSearchAsync("mũ bảo hiểm secret@example.invalid"); var item=Assert.Single(await db.RecommendationEvents.ToListAsync()); Assert.DoesNotContain("secret",item.Terms!); Assert.Equal("search",item.Kind); }
    [Fact] public async Task UnknownProductsAreNotRecorded() { SignIn(1); await service.SetEnabledAsync(true); await service.RecordViewAsync(999,"ticket"); Assert.Empty(await db.RecommendationEvents.ToListAsync()); }
    [Fact] public async Task VehicleNeedsConsentAndBothValues() { await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetVehicleAsync(1,2022)); await service.SetEnabledAsync(true); await Assert.ThrowsAsync<ArgumentException>(()=>service.SetVehicleAsync(1,null)); await Assert.ThrowsAsync<ArgumentException>(()=>service.SetVehicleAsync(999,2022)); }
    [Fact] public async Task AdminCannotEnableOrRecord() { SignIn(1,"Admin"); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetEnabledAsync(true)); await service.RecordCartAsync(1); Assert.Empty(await db.RecommendationEvents.ToListAsync()); }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
