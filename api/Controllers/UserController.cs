using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MonoPayAggregator.Models;
using System.Security.Cryptography;
using System.Text;

namespace MonoPayAggregator.Controllers
{
    [ApiController]
    [Route("v1/users")]
    public class UserController : ControllerBase
    {
        private readonly MonoPayAggregator.Services.IEmailService _emailService;
        private readonly MonoPayAggregator.Data.MonoPayDbContext _db;
        private readonly IConfiguration _configuration;

        public UserController(MonoPayAggregator.Services.IEmailService emailService,
                              MonoPayAggregator.Data.MonoPayDbContext db,
                              IConfiguration configuration)
        {
            _emailService = emailService;
            _db = db;
            _configuration = configuration;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterRequest request)
        {
            try
            {
                var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existing != null)
                {
                    return Conflict(new { message = "Email already in use." });
                }
                var merchantId = Guid.NewGuid().ToString("N").Substring(0, 12);
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Phone = request.Phone,
                    PasswordHash = HashPassword(request.Password),
                    IsVerified = false,
                    MerchantId = merchantId
                };
                var token = Guid.NewGuid().ToString("N");
                _db.Users.Add(user);
                _db.EmailVerifications.Add(new EmailVerification { UserId = user.Id, Token = token });
                await _db.SaveChangesAsync();
                await _emailService.SendVerificationEmailAsync(user.Email, token);
                return Created($"/v1/users/{user.Id}", new { user.Id, user.Email, user.MerchantId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return NotFound(new { message = "User not found." });
                }
                var hash = HashPassword(request.Password);
                if (hash != user.PasswordHash)
                {
                    return Unauthorized(new { message = "Invalid credentials." });
                }
                if (!user.IsVerified)
                {
                    return Unauthorized(new { message = "Account not verified. Please check your email for the verification link." });
                }
                var token = GenerateJwtToken(user);
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Verify a user account using a one‑time token sent via email.
        /// </summary>
        [HttpGet("verify")]
        public async Task<IActionResult> Verify([FromQuery] string token)
        {
            var verification = await _db.EmailVerifications.FirstOrDefaultAsync(v => v.Token == token);
            if (verification == null || verification.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired verification token." });
            }
            var user = await _db.Users.FindAsync(verification.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "User not found." });
            }
            user.IsVerified = true;
            // remove token so it can't be reused
            _db.EmailVerifications.Remove(verification);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Account verified." });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtConfig = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwtConfig["DurationInMinutes"]!));
            var claims = new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Email),
                new System.Security.Claims.Claim("merchant_id", user.MerchantId),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.IsAdmin ? "admin" : "merchant")
            };
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: jwtConfig["Issuer"],
                audience: jwtConfig["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    /// <summary>
    /// Request model for user registration.
    /// </summary>
    public class UserRegisterRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for logging in.
    /// </summary>
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}