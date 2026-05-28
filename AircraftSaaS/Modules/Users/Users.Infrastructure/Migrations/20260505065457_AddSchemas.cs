using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "users");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "RefreshTokens",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "PilotLicenseTypes",
                newName: "PilotLicenseTypes",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "Persons",
                newName: "Persons",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "Licenses",
                newName: "Licenses",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "ContactTypes",
                newName: "ContactTypes",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "Contacts",
                newName: "Contacts",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "Companies",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLogs",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "users");

            migrationBuilder.RenameTable(
                name: "AppUserCompanies",
                newName: "AppUserCompanies",
                newSchema: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                schema: "users",
                newName: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "PilotLicenseTypes",
                schema: "users",
                newName: "PilotLicenseTypes");

            migrationBuilder.RenameTable(
                name: "Persons",
                schema: "users",
                newName: "Persons");

            migrationBuilder.RenameTable(
                name: "Licenses",
                schema: "users",
                newName: "Licenses");

            migrationBuilder.RenameTable(
                name: "ContactTypes",
                schema: "users",
                newName: "ContactTypes");

            migrationBuilder.RenameTable(
                name: "Contacts",
                schema: "users",
                newName: "Contacts");

            migrationBuilder.RenameTable(
                name: "Companies",
                schema: "users",
                newName: "Companies");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                schema: "users",
                newName: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "users",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "users",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "users",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "users",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "users",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "users",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "users",
                newName: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "AppUserCompanies",
                schema: "users",
                newName: "AppUserCompanies");
        }
    }
}
