using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fleet");

            migrationBuilder.RenameTable(
                name: "MaintenanceRecords",
                newName: "MaintenanceRecords",
                newSchema: "fleet");

            migrationBuilder.RenameTable(
                name: "InsurancePolicies",
                newName: "InsurancePolicies",
                newSchema: "fleet");

            migrationBuilder.RenameTable(
                name: "Airports",
                newName: "Airports",
                newSchema: "fleet");

            migrationBuilder.RenameTable(
                name: "Aircrafts",
                newName: "Aircrafts",
                newSchema: "fleet");

            migrationBuilder.RenameTable(
                name: "AircraftPhotos",
                newName: "AircraftPhotos",
                newSchema: "fleet");

            migrationBuilder.RenameTable(
                name: "AircraftAvailabilities",
                newName: "AircraftAvailabilities",
                newSchema: "fleet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "MaintenanceRecords",
                schema: "fleet",
                newName: "MaintenanceRecords");

            migrationBuilder.RenameTable(
                name: "InsurancePolicies",
                schema: "fleet",
                newName: "InsurancePolicies");

            migrationBuilder.RenameTable(
                name: "Airports",
                schema: "fleet",
                newName: "Airports");

            migrationBuilder.RenameTable(
                name: "Aircrafts",
                schema: "fleet",
                newName: "Aircrafts");

            migrationBuilder.RenameTable(
                name: "AircraftPhotos",
                schema: "fleet",
                newName: "AircraftPhotos");

            migrationBuilder.RenameTable(
                name: "AircraftAvailabilities",
                schema: "fleet",
                newName: "AircraftAvailabilities");
        }
    }
}
