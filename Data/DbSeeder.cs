using Data.Entities;
using Data.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; 

namespace Data;

public static class DbSeeder
{
    // This method creates all users needed for testing
    public static async Task SeedUsersAsync(AppDbContext context, ILogger logger)
    {
        // Define specific GUIDs for system users
        var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
        var shopperUserId = new Guid("00000000-0000-0000-0000-000000000002");
        var employeeUserId = new Guid("00000000-0000-0000-0000-000000000003");
        var testShopperUserId = new Guid("00000000-0000-0000-0000-000000000004");

        // 1. Create system user if not exists (SuperAdmin)
        if (!await context.Users.AnyAsync(u => u.Id == systemUserId))
        {
            context.Users.Add(new User
            {
                Id = systemUserId,
                Name = "System Administrator",
                Email = "admin@tte.com",
                Username = "superadmin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = Role.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30) // Created 30 days ago
            });
            logger.LogInformation("SuperAdmin test user created with ID: {UserId}", systemUserId);
        }

        // 2. Create employee user if not exists
        if (!await context.Users.AnyAsync(u => u.Id == employeeUserId))
        {
            context.Users.Add(new User
            {
                Id = employeeUserId,
                Name = "Employee User",
                Email = "employee@tte.com",
                Username = "employee1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!"),
                Role = Role.Employee,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20) // Created 20 days ago
            });
            logger.LogInformation("Employee test user created with ID: {UserId}", employeeUserId);
        }

        // 3. Create shopper user if not exists
        if (!await context.Users.AnyAsync(u => u.Id == shopperUserId))
        {
            context.Users.Add(new User
            {
                Id = shopperUserId,
                Name = "Shopper User",
                Email = "shopper@tte.com",
                Username = "shopper1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Shopper123!"),
                Role = Role.Shopper,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15) // Created 15 days ago
            });
            logger.LogInformation("Shopper user test created with ID: {UserId}", shopperUserId);
        }

        // 4. Create additional test shopper for testing scenarios
        if (!await context.Users.AnyAsync(u => u.Id == testShopperUserId))
        {
            context.Users.Add(new User
            {
                Id = testShopperUserId,
                Name = "Test Shopper",
                Email = "testshopper@tte.com", 
                Username = "testshopper",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = Role.Shopper,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5) // Created 5 days ago
            });
            logger.LogInformation("Test Shopper user created with ID: {UserId}", testShopperUserId);
        }

        // 5. Create inactive user for testing inactive scenarios
        var inactiveUserId = new Guid("00000000-0000-0000-0000-000000000005");
        if (!await context.Users.AnyAsync(u => u.Id == inactiveUserId))
        {
            context.Users.Add(new User
            {
                Id = inactiveUserId,
                Name = "Inactive User",
                Email = "inactive@tte.com",
                Username = "inactiveuser",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Inactive123!"),
                Role = Role.Shopper,
                IsActive = false, // Inactive for testing
                CreatedAt = DateTime.UtcNow.AddDays(-10) // Created 10 days ago
            });
            logger.LogInformation("Inactive test user created with ID: {UserId}", inactiveUserId);
        }

        // Save all users
        await context.SaveChangesAsync();
        logger.LogInformation("Database seeding completed successfully - Created test users");
        
        // Log created users for reference
        logger.LogInformation("=== TEST USERS CREATED ===");
        logger.LogInformation("SuperAdmin: admin@tte.com / superadmin (Password123!)");
        logger.LogInformation("Employee: employee@tte.com / employee1 (Employee123!)");
        logger.LogInformation("Shopper: shopper@tte.com / shopper1 (Shopper123!)");
        logger.LogInformation("Test Shopper: testshopper@tte.com / testshopper (Test123!)");
        logger.LogInformation("Inactive User: inactive@tte.com / inactiveuser (Inactive123!) - INACTIVE");
        logger.LogInformation("========================");
        
        logger.LogInformation("=== SESIONES EN TIEMPO REAL ===");
        logger.LogInformation("✅ Las sesiones se crean AUTOMÁTICAMENTE cuando:");
        logger.LogInformation("   - POST /api/login → Crea sesión con estado 'Active'");
        logger.LogInformation("   - POST /api/logout → Cierra sesión con estado 'Closed'");
        logger.LogInformation("✅ NO hay sesiones de demostración pre-creadas");
        logger.LogInformation("✅ Todas las sesiones son REALES de usuarios reales");
        logger.LogInformation("=================================");
    }
}