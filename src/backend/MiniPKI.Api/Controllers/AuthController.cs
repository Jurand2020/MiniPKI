using Microsoft.AspNetCore.Mvc;
using MiniPKI.Api.Auth;
using MiniPKI.Api.Models;
using MiniPKI.Core.Services;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SessionStore _sessions;
    private readonly IAuditService _audit;
    private readonly PasswordService _passwordService;

    public AuthController(
        SessionStore sessions,
        IAuditService audit,
        PasswordService passwordService)
    {
        _sessions = sessions;
        _audit = audit;
        _passwordService = passwordService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new LoginResponse { Success = false, Error = "Password required" });
        }

        var storedHash = _passwordService.GetPasswordHash();
        if (!PasswordHasher.Verify(request.Password, storedHash))
        {
            await _audit.LogAsync("LOGIN_FAILED", "Failed login attempt");
            return Unauthorized(new LoginResponse { Success = false, Error = "Invalid password" });
        }

        var token = _sessions.CreateSession();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(60)
        };
        Response.Cookies.Append("minipki_session", token, cookieOptions);

        await _audit.LogAsync("LOGIN", "Administrator logged in");

        return Ok(new LoginResponse { Success = true });
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var token = Request.Cookies["minipki_session"];
        _sessions.RemoveSession(token);
        Response.Cookies.Delete("minipki_session");

        await _audit.LogAsync("LOGOUT", "Administrator logged out");

        return Ok();
    }

    [HttpGet("status")]
    public ActionResult<AuthStatusResponse> Status()
    {
        var token = Request.Cookies["minipki_session"];
        return Ok(new AuthStatusResponse
        {
            Authenticated = _sessions.IsValid(token),
            FirstRun = _passwordService.IsFirstRun()
        });
    }

    [HttpPost("set-password")]
    public async Task<ActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters" });
        }

        if (!_passwordService.IsFirstRun())
        {
            return BadRequest(new { error = "Password has already been set" });
        }

        _passwordService.SetInitialPassword(request.Password);
        await _audit.LogAsync("PASSWORD_SET", "Initial password set on first run");

        return Ok(new { success = true });
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { error = "New password must be at least 8 characters" });
        }

        if (!_passwordService.ChangePassword(request.CurrentPassword, request.NewPassword))
        {
            return BadRequest(new { error = "Current password is incorrect" });
        }

        await _audit.LogAsync("PASSWORD_CHANGE", "Admin password changed");

        return Ok(new { success = true });
    }
}
