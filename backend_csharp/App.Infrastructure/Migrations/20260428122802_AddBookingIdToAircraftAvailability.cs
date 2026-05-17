using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingIdToAircraftAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BookingId",
                table: "AircraftAvailabilities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AircraftAvailabilities_BookingId",
                table: "AircraftAvailabilities",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_AircraftAvailabilities_Bookings_BookingId",
                table: "AircraftAvailabilities",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AircraftAvailabilities_Bookings_BookingId",
                table: "AircraftAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_AircraftAvailabilities_BookingId",
                table: "AircraftAvailabilities");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "AircraftAvailabilities");
        }
    }
}
