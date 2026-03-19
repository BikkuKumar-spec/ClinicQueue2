using Microsoft.AspNetCore.Identity;

namespace ClinicQueue.Infrastructure.Persistence;

public class ClinicIdentityUser : IdentityUser
{
    public string RoleName { get; set; } = "Staff";
}
