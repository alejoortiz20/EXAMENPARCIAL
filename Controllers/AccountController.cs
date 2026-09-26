using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EXAMENPARCIAL.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string usuario, string clave, string? returnUrl = null)
    {
        var user = await _userManager.FindByNameAsync(usuario);

        var resultado = user is null
            ? null
            : await _signInManager.PasswordSignInAsync(user, clave, isPersistent: false, lockoutOnFailure: false);

        if (resultado is { Succeeded: true })
        {
            _logger.LogInformation("Inicio de sesion del supervisor {Usuario}", usuario);
            return LocalRedirect(returnUrl ?? "/Operaciones/Incidencias");
        }

        ModelState.AddModelError(string.Empty, "Usuario o clave incorrectos.");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
