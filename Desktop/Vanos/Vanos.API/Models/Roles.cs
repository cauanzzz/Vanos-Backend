namespace Vanos.API.Models;
public static class Roles
{
    public const string Driver = "Driver";
    public const string Parent = "Parent";
    public const string Student = "Student";
    public const string Consumer = Parent + "," + Student;
}
