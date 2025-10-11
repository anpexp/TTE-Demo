using Data;
using Data.Entities.Enums;
using Logica.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TechTrendEmporium.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionsController : BaseController
    {
        private readonly IAuthService _authService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(IAuthService authService, ILogger<SessionsController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener información de la sesión actual del usuario autenticado
        /// </summary>
        [HttpGet("current")]
        [Authorize]
        public async Task<ActionResult<object>> GetCurrentSession()
        {
            try
            {
                var jtiClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
                if (jtiClaim == null)
                {
                    return BadRequest(new { message = "Token inválido - no se encontró identificador de sesión" });
                }

                var session = await _authService.GetActiveSessionAsync(jtiClaim.Value);
                if (session == null)
                {
                    return NotFound(new { message = "No se encontró una sesión activa" });
                }

                return Ok(new {
                    sessionId = session.Id,
                    userId = session.UserId,
                    status = session.Status.ToString(),
                    createdAt = session.CreatedAt,
                    ipAddress = session.Ip,
                    userAgent = session.UserAgent,
                    message = "Sesión activa encontrada"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo la sesión actual");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener historial de sesiones del usuario actual
        /// </summary>
        [HttpGet("my-history")]
        [Authorize]
        public async Task<ActionResult<object>> GetMySessionHistory()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return BadRequest(new { message = "Usuario no identificado" });
                }

                // Aquí necesitarías implementar un método en el servicio para obtener historial
                // Por ahora retornamos un placeholder
                return Ok(new
                {
                    userId = userId,
                    message = "Historial de sesiones - implementar según necesidades específicas"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo el historial de sesiones del usuario");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }
    }
}