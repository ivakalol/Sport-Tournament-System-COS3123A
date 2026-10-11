using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportTournaments.Registration.Domain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TournamentName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParticipantName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_ParticipantId",
                table: "Registrations",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_TournamentId_ParticipantId",
                table: "Registrations",
                columns: new[] { "TournamentId", "ParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Registrations");
        }
    }
}
