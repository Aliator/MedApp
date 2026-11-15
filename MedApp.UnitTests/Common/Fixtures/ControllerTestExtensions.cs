using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.UnitTests.Common.Fixtures;

public static class ControllerTestExtensions
{
    /// <summary>Gives a controller a request (signed in as <paramref name="user"/>, or anonymous) to act on.</summary>
    public static TController WithRequest<TController>(this TController controller, ClaimsPrincipal? user = null, Action<HttpContext>? configure = null)
        where TController : ControllerBase
    {
        var context = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        configure?.Invoke(context);
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}
