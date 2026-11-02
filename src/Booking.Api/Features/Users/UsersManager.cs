using System.Net.Mail;
using Booking.Api.Domain;
using Booking.Api.Errors;
using Booking.Api.Features.Shared;
using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Api.Features.Users;

public class UsersManager
{
    private readonly AppDbContext _db;

    public UsersManager(AppDbContext db)
    {
        _db = db;
    }

    public Task<PagedResponse<UserResponse>> ListAsync(int page, int pageSize)
    {
        var ordered = _db.Users.AsNoTracking().OrderBy(u => u.Name).ThenBy(u => u.Id);
        return Paging.ToPageAsync(ordered, page, pageSize, UserResponse.From);
    }

    public async Task<UserResponse> GetAsync(Guid id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return UserResponse.From(user ?? throw UserNotFound());
    }

    public async Task<UserResponse> CreateAsync(UserRequest request)
    {
        var (name, email, role) = Validate(request);
        await EnsureEmailIsFree(email, exceptUserId: null);

        var user = new User { Id = Guid.NewGuid(), Name = name, Email = email, Role = role };
        _db.Users.Add(user);
        await SaveAsync();
        return UserResponse.From(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UserRequest request)
    {
        var (name, email, role) = Validate(request);

        // Lock every admin row until this transaction ends, so two requests that each demote an admin
        // run one after the other. The second waits here; once the first commits, Postgres checks the
        // WHERE clause again on the locked rows, so a just-demoted admin no longer counts as an admin.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var admins = await _db.Users
            .FromSqlRaw("SELECT * FROM \"Users\" WHERE \"Role\" = 'Admin' ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw UserNotFound();
        await EnsureEmailIsFree(email, exceptUserId: id);

        var wasAdmin = admins.Any(a => a.Id == id);
        var otherAdmins = admins.Count(a => a.Id != id);
        if (wasAdmin && role != Role.Admin && otherAdmins == 0)
        {
            throw ApiException.Conflict("LAST_ADMIN",
                "This is the only admin. Make another user an admin first, then change this role.");
        }

        user.Name = name;
        user.Email = email;
        user.Role = role;
        await SaveAsync();
        await transaction.CommitAsync();
        return UserResponse.From(user);
    }

    private static ApiException UserNotFound() =>
        ApiException.NotFound("There is no user with that id. Check the id and try again.");

    // Returns the cleaned values, or throws one 400 listing every bad field.
    private static (string Name, string Email, Role Role) Validate(UserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            errors["name"] = ["Enter a name."];
        }
        else if (name.Length > 200)
        {
            errors["name"] = ["Use 200 characters or fewer for the name."];
        }

        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        if (!LooksLikeEmail(email))
        {
            errors["email"] = ["Enter a valid email address, such as name@example.com."];
        }

        // Only the exact names "Admin" and "Member" (case-sensitive); numbers like "1" are not roles.
        var role = request.Role switch { "Admin" => Role.Admin, "Member" => Role.Member, _ => (Role?)null };
        if (role is null)
        {
            errors["role"] = ["Choose a role: Admin or Member."];
        }

        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }
        return (name, email, role!.Value);
    }

    private static bool LooksLikeEmail(string email)
    {
        if (email.Length == 0 || email.Length > 320)
        {
            return false;
        }
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && parsed.Host.Contains('.');
    }

    private async Task EnsureEmailIsFree(string email, Guid? exceptUserId)
    {
        var taken = await _db.Users.AnyAsync(u => u.Email == email && u.Id != exceptUserId);
        if (taken)
        {
            throw EmailTaken();
        }
    }

    private static ApiException EmailTaken() =>
        ApiException.Conflict("EMAIL_TAKEN", "That email is already in use. Use a different email address.");

    // The unique index is the final guard if two requests race past the check above.
    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_Email" })
        {
            throw EmailTaken();
        }
    }
}
