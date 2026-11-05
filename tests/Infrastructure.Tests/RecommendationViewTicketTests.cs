using System.Security.Claims;
using Infrastructure.Recommendations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Tests;

public sealed class RecommendationViewTicketTests
{
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private static DefaultHttpContext User(int id)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(Protection).BuildServiceProvider() };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Sid,id.ToString())],"test"));
        return context;
    }
    private static string Ticket(int id, long issued, int user) => Protection.CreateProtector("MotoShop.RecommendationView.v1").Protect($"{id}|{issued}|u:{user}");
    [Fact] public void TicketCannotBeUsedImmediately() { var c=User(1); Assert.Null(RecommendationViewTicket.Verify(c,RecommendationViewTicket.Create(c,123))); }
    [Fact] public void TicketIsBoundToUser() { var t=Ticket(12,DateTimeOffset.UtcNow.ToUnixTimeSeconds()-9,1); Assert.Null(RecommendationViewTicket.Verify(User(2),t)); }
    [Fact] public void TicketExpires() { var t=Ticket(12,DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1201,1); Assert.Null(RecommendationViewTicket.Verify(User(1),t)); }
    [Fact] public void EligibleTicketIdentifiesProduct() { var t=Ticket(12,DateTimeOffset.UtcNow.ToUnixTimeSeconds()-9,1); Assert.Equal(12,RecommendationViewTicket.Verify(User(1),t)); }
    [Fact] public void ForgedTicketIsRejected() { Assert.Null(RecommendationViewTicket.Verify(User(1),"not-a-valid-ticket")); }
}
