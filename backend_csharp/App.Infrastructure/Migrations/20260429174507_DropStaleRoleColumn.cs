using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropStaleRoleColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety: copy Role → AppUserRoleInCompany where they differ,
            // so no data is lost when the stale Role column is dropped.
            migrationBuilder.Sql(
                @"UPDATE ""AppUserCompanies""
                  SET ""AppUserRoleInCompany"" = ""Role""
                  WHERE ""AppUserRoleInCompany"" <> ""Role"";");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppUserCompanies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "AppUserCompanies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
