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
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(AppDbContext context, IAuthService authService, ILogger<SessionsController> logger)
        {
            _context = context;
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

                var userInfo = await _context.Users
                    .Where(u => u.Id == session.UserId)
                    .Select(u => new { u.Username, u.Email, u.Role })
                    .FirstOrDefaultAsync();

                return Ok(new {
                    sessionId = session.Id,
                    userId = session.UserId,
                    username = userInfo?.Username,
                    email = userInfo?.Email,
                    role = userInfo?.Role.ToString(),
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

                var sessions = await _context.Sessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        sessionId = s.Id,
                        status = s.Status.ToString(),
                        createdAt = s.CreatedAt,
                        closedAt = s.ClosedAt,
                        ipAddress = s.Ip,
                        userAgent = s.UserAgent,
                        durationMinutes = s.ClosedAt != null 
                            ? EF.Functions.DateDiffMinute(s.CreatedAt, s.ClosedAt.Value)
                            : EF.Functions.DateDiffMinute(s.CreatedAt, DateTime.UtcNow)
                    })
                    .Take(20) // Últimas 20 sesiones
                    .ToListAsync();

                return Ok(new
                {
                    userId = userId,
                    totalSessions = sessions.Count,
                    sessions = sessions,
                    message = "Historial de sesiones obtenido exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo el historial de sesiones del usuario");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener todas las sesiones activas (Solo SuperAdmin)
        /// </summary>
        [HttpGet("active")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetActiveSessions()
        {
            try
            {
                var activeSessions = await _context.Sessions
                    .Include(s => s.User)
                    .Where(s => s.Status == SessionStatus.Active)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        sessionId = s.Id,
                        userId = s.UserId,
                        username = s.User.Username,
                        email = s.User.Email,
                        role = s.User.Role.ToString(),
                        createdAt = s.CreatedAt,
                        ipAddress = s.Ip,
                        userAgent = s.UserAgent,
                        minutesActive = EF.Functions.DateDiffMinute(s.CreatedAt, DateTime.UtcNow)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalActiveSessions = activeSessions.Count,
                    sessions = activeSessions,
                    timestamp = DateTime.UtcNow,
                    message = "Sesiones activas obtenidas exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo las sesiones activas");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener historial completo de sesiones (Solo SuperAdmin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetAllSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var totalSessions = await _context.Sessions.CountAsync();
                
                var sessions = await _context.Sessions
                    .Include(s => s.User)
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        sessionId = s.Id,
                        userId = s.UserId,
                        username = s.User.Username,
                        email = s.User.Email,
                        role = s.User.Role.ToString(),
                        status = s.Status.ToString(),
                        createdAt = s.CreatedAt,
                        closedAt = s.ClosedAt,
                        ipAddress = s.Ip,
                        userAgent = s.UserAgent,
                        durationMinutes = s.ClosedAt != null 
                            ? EF.Functions.DateDiffMinute(s.CreatedAt, s.ClosedAt.Value)
                            : (s.Status == SessionStatus.Active 
                                ? (int?)EF.Functions.DateDiffMinute(s.CreatedAt, DateTime.UtcNow) 
                                : (int?)null)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalSessions = totalSessions,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalSessions / pageSize),
                    sessions = sessions,
                    message = "Historial de sesiones obtenido exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo el historial completo de sesiones");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener estadísticas de sesiones (Solo SuperAdmin)
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> GetSessionStatistics()
        {
            try
            {
                var totalSessions = await _context.Sessions.CountAsync();
                var activeSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Active);
                var closedSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Closed);
                var expiredSessions = await _context.Sessions.CountAsync(s => s.Status == SessionStatus.Expired);

                var sessionsByRole = await _context.Sessions
                    .Include(s => s.User)
                    .GroupBy(s => s.User.Role)
                    .Select(g => new
                    {
                        role = g.Key.ToString(),
                        totalSessions = g.Count(),
                        activeSessions = g.Count(s => s.Status == SessionStatus.Active)
                    })
                    .ToListAsync();

                var recentLogins = await _context.Sessions
                    .Include(s => s.User)
                    .Where(s => s.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .GroupBy(s => s.CreatedAt.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        loginCount = g.Count()
                    })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                return Ok(new
                {
                    totalSessions = totalSessions,
                    sessionsByStatus = new
                    {
                        active = activeSessions,
                        closed = closedSessions,
                        expired = expiredSessions
                    },
                    sessionsByRole = sessionsByRole,
                    recentLoginsLast7Days = recentLogins,
                    timestamp = DateTime.UtcNow,
                    message = "Estadísticas de sesiones obtenidas exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo las estadísticas de sesiones");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Cerrar una sesión específica (Solo SuperAdmin)
        /// </summary>
        [HttpPost("{sessionId:guid}/close")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<object>> CloseSession(Guid sessionId)
        {
            try
            {
                var session = await _context.Sessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    return NotFound(new { message = "Sesión no encontrada" });
                }

                if (session.Status != SessionStatus.Active)
                {
                    return BadRequest(new { message = $"La sesión ya está en estado: {session.Status}" });
                }

                session.Status = SessionStatus.Expired;
                session.ClosedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sesión {SessionId} cerrada por administrador para el usuario {Username}", 
                    sessionId, session.User.Username);

                return Ok(new
                {
                    sessionId = sessionId,
                    userId = session.UserId,
                    username = session.User.Username,
                    previousStatus = "Active",
                    newStatus = "Expired",
                    closedAt = session.ClosedAt,
                    message = "Sesión cerrada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cerrando la sesión {SessionId}", sessionId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }
    }
}