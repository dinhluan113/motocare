namespace MotoCare.Api.Infrastructure;

public static class SecurityRoles
{
    public const string Admin = "Admin";
    public const string LegacyAdministrator = "Administrator";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public const string Administrators = Admin + "," + LegacyAdministrator;
    public const string Management = Admin + "," + LegacyAdministrator + "," + Manager;
    public const string Operations = Admin + "," + LegacyAdministrator + "," + Manager + "," + Employee;
    public const string Finance = Admin + "," + LegacyAdministrator + "," + Manager;
}
