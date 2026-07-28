namespace MotoCare.Api.Infrastructure;

public static class SecurityRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Receptionist = "Receptionist";
    public const string Technician = "Technician";
    public const string Cashier = "Cashier";

    public const string Management = Administrator + "," + Manager;
    public const string Operations = Administrator + "," + Manager + "," + Receptionist;
    public const string Finance = Administrator + "," + Manager + "," + Cashier;
}
