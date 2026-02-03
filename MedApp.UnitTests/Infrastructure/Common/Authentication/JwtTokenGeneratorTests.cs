using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.UnitTests.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MedApp.UnitTests.Infrastructure.Common.Authentication;

[TestFixture]
public sealed class JwtTokenGeneratorTests
{
    private IConfiguration _configuration = null!;
    private JwtTokenGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('a', 32),
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings!)
            .Build();

        _generator = new JwtTokenGenerator(_configuration);
    }

    [Test]
    public void GenerateToken_ValidInputs_ReturnsSignedJwtWithExpectedClaims()
    {
        var userId = Guid.NewGuid();
        var username = AuthTestConstants.Usernames[0];
        var roles = AuthTestConstants.Roles;

        var token = _generator.GenerateToken(userId, username, roles);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("issuer");
        jwt.Audiences.Should().Contain("audience");

        jwt.Claims.Should().ContainSingle(c =>
            c.Type == JwtRegisteredClaimNames.Sub &&
            c.Value == userId.ToString());

        jwt.Claims.Should().ContainSingle(c =>
            c.Type == ClaimTypes.Name &&
            c.Value == username);

        foreach (var role in roles)
        {
            jwt.Claims.Should().ContainSingle(c =>
                c.Type == ClaimTypes.Role &&
                c.Value == role);
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "issuer",
            ValidateAudience = true,
            ValidAudience = "audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        handler
            .ValidateToken(token, parameters, out _)
            .Should()
            .NotBeNull();
    }
}
