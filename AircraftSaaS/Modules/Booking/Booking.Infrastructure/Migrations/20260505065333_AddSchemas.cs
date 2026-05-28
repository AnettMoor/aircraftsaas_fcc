using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "booking");

            migrationBuilder.RenameTable(
                name: "Reviews",
                newName: "Reviews",
                newSchema: "booking");

            migrationBuilder.RenameTable(
                name: "Payments",
                newName: "Payments",
                newSchema: "booking");

            migrationBuilder.RenameTable(
                name: "Bookings",
                newName: "Bookings",
                newSchema: "booking");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Reviews",
                schema: "booking",
                newName: "Reviews");

            migrationBuilder.RenameTable(
                name: "Payments",
                schema: "booking",
                newName: "Payments");

            migrationBuilder.RenameTable(
                name: "Bookings",
                schema: "booking",
                newName: "Bookings");
        }
    }
}
