using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overflow.EstimationService.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksToRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskName",
                table: "RoundHistory",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TasksJson",
                table: "Rooms",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskName",
                table: "RoundHistory");

            migrationBuilder.DropColumn(
                name: "TasksJson",
                table: "Rooms");
        }
    }
}
