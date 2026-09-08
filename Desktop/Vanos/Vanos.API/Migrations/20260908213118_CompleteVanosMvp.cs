using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vanos.API.Migrations
{
    /// <inheritdoc />
    public partial class CompleteVanosMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "IsSimulated",
                table: "MonthlyFees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DriverServiceAreas",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverServiceAreas", x => new { x.DriverId, x.City, x.Neighborhood });
                    table.ForeignKey(
                        name: "FK_DriverServiceAreas_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DriverId",
                table: "Users",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_DriverId",
                table: "Students",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ParentId_DriverId",
                table: "Students",
                columns: new[] { "ParentId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFees_DriverId",
                table: "MonthlyFees",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFees_StudentId_DriverId_DueDate",
                table: "MonthlyFees",
                columns: new[] { "StudentId", "DriverId", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFees_StudentId_IsPaid_DueDate",
                table: "MonthlyFees",
                columns: new[] { "StudentId", "IsPaid", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverSchools_SchoolId_DriverId",
                table: "DriverSchools",
                columns: new[] { "SchoolId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverServiceAreas_City_Neighborhood",
                table: "DriverServiceAreas",
                columns: new[] { "City", "Neighborhood" });

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyFees_Drivers_DriverId",
                table: "MonthlyFees",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyFees_Students_StudentId",
                table: "MonthlyFees",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Drivers_DriverId",
                table: "Users",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyFees_Drivers_DriverId",
                table: "MonthlyFees");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyFees_Students_StudentId",
                table: "MonthlyFees");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Drivers_DriverId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "DriverServiceAreas");

            migrationBuilder.DropIndex(
                name: "IX_Users_DriverId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Students_DriverId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ParentId_DriverId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyFees_DriverId",
                table: "MonthlyFees");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyFees_StudentId_DriverId_DueDate",
                table: "MonthlyFees");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyFees_StudentId_IsPaid_DueDate",
                table: "MonthlyFees");

            migrationBuilder.DropIndex(
                name: "IX_DriverSchools_SchoolId_DriverId",
                table: "DriverSchools");

            migrationBuilder.DropColumn(
                name: "IsSimulated",
                table: "MonthlyFees");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);
        }
    }
}
