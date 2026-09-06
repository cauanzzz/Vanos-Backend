using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vanos.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverSchools",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverSchools", x => new { x.DriverId, x.SchoolId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverSchools");
        }
    }
}
