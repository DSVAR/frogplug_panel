using FrogPlug.Server.Data;
using FrogPlug.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed record LoginResult(
    bool Success,
    Admin? Admin,
    string Error,
    DateTime? LockedUntil = null,
    bool RequiresTwoFactor = false);

public sealed class AdminAuthService
{
    private readonly AppDbContext db;
    private readonly TimeProvider clock;
    private readonly AuditService? audit;
    private readonly PasswordHasher<Admin> passwordHasher = new();

    public AdminAuthService(AppDbContext db, TimeProvider? timeProvider = null, AuditService? audit = null)
    {
        this.db = db;
        clock = timeProvider ?? TimeProvider.System;
        this.audit = audit;
    }

    public async Task<Admin> CreateFirstAdmin(string login, string password)
    {
        ValidateLogin(login);
        ValidatePassword(password);
        if (await db.Admins.AnyAsync())
        {
            throw new InvalidOperationException("Администратор уже существует");
        }

        var admin = new Admin
        {
            Login = login.Trim(),
            PasswordHash = string.Empty,
            CreatedAt = UtcNow,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        db.Admins.Add(admin);
        await WriteAudit(admin.Login, AuditActions.AdminCreate, "Создан первый администратор");
        await db.SaveChangesAsync();
        return admin;
    }

    public async Task<Admin> CreateAdmin(string login, string password, string actorLogin)
    {
        ValidateLogin(login);
        ValidatePassword(password);
        var normalizedLogin = login.Trim();
        if (await db.Admins.AnyAsync(admin => admin.Login == normalizedLogin))
        {
            throw new InvalidOperationException("Администратор с таким логином уже существует");
        }

        var admin = new Admin
        {
            Login = normalizedLogin,
            PasswordHash = string.Empty,
            CreatedAt = UtcNow,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        db.Admins.Add(admin);
        await WriteAudit(actorLogin, AuditActions.AdminCreate, $"Создан администратор {normalizedLogin}");
        await db.SaveChangesAsync();
        return admin;
    }

    public async Task<LoginResult> ValidateLogin(string login, string password)
    {
        var admin = await db.Admins.SingleOrDefaultAsync(item => item.Login == login);
        if (admin is null || !admin.IsActive)
        {
            await WriteAudit(login, AuditActions.LoginFail, null);
            await db.SaveChangesAsync();
            return new LoginResult(false, null, "Неверный логин или пароль");
        }

        if (admin.LockedUntil is { } lockedUntil && lockedUntil > UtcNow)
        {
            await WriteAudit(login, AuditActions.LoginFail, "Попытка входа во время блокировки");
            await db.SaveChangesAsync();
            return new LoginResult(false, null, string.Empty, lockedUntil);
        }

        var verification = passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            admin.FailedAttempts++;
            if (admin.FailedAttempts >= 5)
            {
                admin.FailedAttempts = 0;
                admin.LockedUntil = UtcNow.AddMinutes(15);
            }

            await WriteAudit(login, AuditActions.LoginFail, null);
            await db.SaveChangesAsync();
            var error = admin.LockedUntil is null ? "Неверный логин или пароль" : string.Empty;
            return new LoginResult(false, null, error, admin.LockedUntil);
        }

        if (!string.IsNullOrWhiteSpace(admin.TotpSecret))
        {
            return new LoginResult(true, admin, string.Empty, RequiresTwoFactor: true);
        }

        await CompleteLogin(admin);
        return new LoginResult(true, admin, string.Empty);
    }

    public async Task<LoginResult> ValidateTwoFactor(int adminId, string code)
    {
        var admin = await db.Admins.FindAsync(adminId);
        if (admin is null || !admin.IsActive || string.IsNullOrWhiteSpace(admin.TotpSecret))
        {
            return new LoginResult(false, null, "Недействительная попытка входа");
        }

        if (admin.LockedUntil is { } lockedUntil && lockedUntil > UtcNow)
        {
            return new LoginResult(false, null, string.Empty, lockedUntil);
        }

        if (!Totp.TryVerify(admin.TotpSecret, code, clock.GetUtcNow(), 1, out var step)
            || admin.LastTotpStep is { } lastStep && step <= lastStep)
        {
            await RegisterTwoFactorFailure(admin);
            return new LoginResult(false, null, "Неверный код", admin.LockedUntil);
        }

        admin.LastTotpStep = step;
        await CompleteLogin(admin);
        return new LoginResult(true, admin, string.Empty);
    }

    public async Task<bool> EnableTwoFactor(Admin admin, string secret, string code)
    {
        if (!Totp.TryVerify(secret, code, clock.GetUtcNow(), 1, out var step))
        {
            await WriteAudit(admin.Login, AuditActions.TotpFail, null);
            await db.SaveChangesAsync();
            return false;
        }

        admin.TotpSecret = secret;
        admin.LastTotpStep = step;
        await WriteAudit(admin.Login, AuditActions.TotpEnable, null);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DisableTwoFactor(Admin admin, string password, string code)
    {
        if (passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed
            || string.IsNullOrWhiteSpace(admin.TotpSecret)
            || !Totp.TryVerify(admin.TotpSecret, code, clock.GetUtcNow(), 1, out _))
        {
            await WriteAudit(admin.Login, AuditActions.TotpFail, null);
            await db.SaveChangesAsync();
            return false;
        }

        admin.TotpSecret = null;
        admin.LastTotpStep = null;
        await WriteAudit(admin.Login, AuditActions.TotpDisable, null);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePassword(Admin admin, string currentPassword, string newPassword)
    {
        ValidatePassword(newPassword);
        if (passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        admin.PasswordHash = passwordHasher.HashPassword(admin, newPassword);
        await WriteAudit(admin.Login, AuditActions.PasswordChange, null);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAdmin(int id, string actorLogin)
    {
        var admin = await db.Admins.FindAsync(id);
        if (admin is null || admin.Login == actorLogin)
        {
            return false;
        }

        admin.IsActive = false;
        await WriteAudit(actorLogin, AuditActions.AdminDeactivate, $"Деактивирован администратор {admin.Login}");
        await db.SaveChangesAsync();
        return true;
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new ArgumentException("Пароль должен быть не короче 12 символов");
        }
    }

    private static void ValidateLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new ArgumentException("Логин обязателен");
        }
    }

    private async Task WriteAudit(string login, string action, string? details)
    {
        if (audit is not null)
        {
            await audit.Write(action, details, login);
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            At = UtcNow,
            AdminLogin = login,
            Action = action,
            Details = details
        });
    }

    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    private async Task CompleteLogin(Admin admin)
    {
        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        admin.LastLoginAt = UtcNow;
        await WriteAudit(admin.Login, AuditActions.LoginOk, null);
        await db.SaveChangesAsync();
    }

    private async Task RegisterTwoFactorFailure(Admin admin)
    {
        admin.FailedAttempts++;
        if (admin.FailedAttempts >= 5)
        {
            admin.FailedAttempts = 0;
            admin.LockedUntil = UtcNow.AddMinutes(15);
        }

        await WriteAudit(admin.Login, AuditActions.TotpFail, null);
        await db.SaveChangesAsync();
    }
}


