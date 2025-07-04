using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Entities;
using Core.RequestModels;
using FluentValidation;
using Core.DTOs;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Infrastructure.Validators;

namespace Infrastructure.Controllers;

/// <summary>
/// Controller for managing user-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IConfiguration _config;

    public UserController(UserService userService, IConfiguration config)
    {
        _userService = userService;
        _config = config;
    }

    [HttpGet("{email}")]
    public async Task<ActionResult<User>> GetUserByEmail(string email)
    {
        try
        {
            var user = await _userService.GetByEmailAsync(email);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUserById(Guid id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Core.RequestModels.RegisterRequest request)
    {
        try
        {
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                BirthDate = request.BirthDate
            };

            var result = await _userService.RegisterAsync(user, request.Password1, request.Password2);

            return result
                ? CreatedAtAction(nameof(GetUserByEmail), new { email = user.Email }, user)
                : BadRequest("Failed to register user.");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Errors);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (EmailExistsException ex)
        {
            return BadRequest($"{ex.Message}. Tried email: {ex.TriedEmail}");
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out Guid userId))
            return Unauthorized("Invalid token");

        var user = await _userService.GetByIdAsync(userId); // Or use DbContext directly

        if (user == null)
            return NotFound("User not found");

        // Optional: map to DTO to avoid sending password hashes etc.
        var userDto = new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.BirthDate
        };

        return Ok(userDto);
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Core.RequestModels.LoginRequest request)
    {
        try
        {
            var user = await _userService.LoginAsync(request.Email, request.Password);
            if (user == null)
                return Unauthorized("Invalid credentials");

            var token = GenerateJwt(user);
            return Ok(new AuthResponseDto { Token = token });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private string GenerateJwt(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    
    [Authorize]
    [HttpPut("update")]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest dto)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized("Invalid token");

            if (dto.Id != userId)
                return Forbid("You can only update your own profile.");

            // Validate request DTO
            var validator = new UpdateUserRequestValidator();
            var validationResult = await validator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors);

            // Get the current user to compare emails
            var existingUser = await _userService.GetByIdAsync(userId);
            if (existingUser == null)
                return NotFound("User not found.");

            var isEmailChanged = !string.Equals(existingUser.Email, dto.Email, StringComparison.OrdinalIgnoreCase);
            if (isEmailChanged && await _userService.UserExistsAsync(dto.Email))
                throw new EmailExistsException("Email is already used by another user.") { TriedEmail = dto.Email };

            // Create updated user entity
            var user = new User
            {
                Id = dto.Id,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                BirthDate = dto.BirthDate
            };

            var success = await _userService.UpdateUserAsync(user);
            return success ? Ok("User updated successfully.") : NotFound("User not found.");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Errors);
        }
        catch (EmailExistsException ex)
        {
            return BadRequest($"{ex.Message}. Tried email: {ex.TriedEmail}");
        }
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var result = await _userService.ChangePasswordAsync(
                request.Email,
                request.CurrentPassword,
                request.NewPassword,
                request.ConfirmNewPassword
            );

            return result ? Ok("Password changed successfully.") : BadRequest("Failed to change password.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount()
    {
        var count = await _userService.CountAsync();

        if (count > 0) return Ok(count);
        return NotFound("No users registered");
    }
}