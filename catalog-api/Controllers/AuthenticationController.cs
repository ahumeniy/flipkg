using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flipkg.Web.Api.Controllers;

public class AuthenticationController : Controller
{
    [HttpGet("~/signin")]
    public IActionResult SignIn()
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/"}, "GitHub");
    }

    [HttpGet("~/signout")]
    [HttpPost("~/signout")]
    public IActionResult SignOutCurrentUser()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("~/")]
    public IActionResult Index()
    {
        return new ObjectResult(new { Resultado = "OK", Usuario= User.Identity?.Name, Claims = User.Claims.Select(x => new{ x.Type, x.Value }).ToArray()});
    }
}