using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSODemo.Models;

namespace SSODemo.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Authorize]
    public IActionResult Index()
    {
        var user = User;
        var viewModel = new UserInfoViewModel
        {
            Name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value,
            Email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value,
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value,
            Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
                       .Concat(user.FindAll("roles").SelectMany(c => c.Value.Split(',')))
                       .Distinct(),
            Claims = user.Claims.Select(c => new { c.Type, c.Value })
                .ToDictionary(c => c.Type, c => c.Value)
        };

        return View(viewModel);
    }

    public IActionResult Login()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Challenge(new AuthenticationProperties { RedirectUri = Url.Action("Index", "Home") }, 
                CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Logout()
    {
        // 构建 Keycloak 登出 URL
        var authority = _configuration.GetSection("Keycloak")["Authority"];
        var logoutUrl = $"{authority}/protocol/openid-connect/logout";
        
        // 清除本地 Cookie
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // 重定向到 Keycloak 登出端点，然后返回应用首页
        var redirectUri = Url.Action("Index", "Home", null, Request.Scheme, Request.Host.ToString());
        var keycloakLogoutUrl = $"{logoutUrl}?post_logout_redirect_uri={Uri.EscapeDataString(redirectUri)}";
        
        return Redirect(keycloakLogoutUrl);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var message = Request.Query["message"].FirstOrDefault() ?? "An error occurred.";
        ViewBag.ErrorMessage = message;
        return View();
    }

    [Authorize]
    public IActionResult Profile()
    {
        return View();
    }
}
