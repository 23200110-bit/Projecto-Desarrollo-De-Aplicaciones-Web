using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Models;
using EventosPeruIA.Api.Services;

namespace EventosPeruIA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(
            ApplicationDbContext context,
            JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var existe = await _context.Usuarios
                .AnyAsync(u => u.Email == request.Email);

            if (existe)
            {
                return BadRequest("El correo ya está registrado.");
            }

            var usuario = new Usuario
            {
                Nombre = request.Nombre,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Rol = request.Rol
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Usuario registrado correctamente"
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (usuario == null ||
                !BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    usuario.PasswordHash))
            {
                return Unauthorized("Credenciales inválidas.");
            }

            var token = _jwtService.GenerarToken(usuario);

            return Ok(new
            {
                token,
                usuario = new
                {
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Email,
                    usuario.Rol
                }
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordRequest request)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (usuario == null)
            {
                return Ok(new
                {
                    mensaje = "Si el correo está registrado se generará una solicitud de recuperación"
                });
            }

            var token = Guid.NewGuid().ToString();

            usuario.ResetToken = token;
            usuario.ResetTokenExpira = DateTime.UtcNow.AddMinutes(30);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Token de recuperación generado",
                token
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordRequest request)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u =>
                    u.ResetToken == request.Token &&
                    u.ResetTokenExpira > DateTime.UtcNow);

            if (usuario == null)
            {
                return BadRequest(new
                {
                    mensaje = "El token es inválido o ha expirado"
                });
            }

            usuario.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);

            usuario.ResetToken = null;
            usuario.ResetTokenExpira = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Contraseña actualizada correctamente"
            });
        }
    }
}