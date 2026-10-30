using System.Security.Claims;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Security;
using Xunit;

namespace GeoPolitics_Game.Tests
{
    public class SecurityTests
    {
        private static ClaimsPrincipal LoggedInAs(string userId) =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"));

        private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());   // not logged in

        [Fact]
        public void Owner_may_play() =>
            Assert.True(LoggedInAs("u1").IsOwnerOf(new Game { OwnerUserId = "u1" }));

        [Fact]
        public void Another_user_may_not_play() =>
            Assert.False(LoggedInAs("u2").IsOwnerOf(new Game { OwnerUserId = "u1" }));

        [Fact]
        public void Visitor_may_not_play() =>
            Assert.False(Visitor.IsOwnerOf(new Game { OwnerUserId = "u1" }));
    }
}