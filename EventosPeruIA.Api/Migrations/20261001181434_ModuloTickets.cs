using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventosPeruIA.Api.Migrations
{
	/// <inheritdoc />
	public partial class ModuloTickets : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Tickets",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					EventoId = table.Column<int>(type: "int", nullable: false),
					UsuarioId = table.Column<int>(type: "int", nullable: false),
					Cantidad = table.Column<int>(type: "int", nullable: false),
					Estado = table.Column<int>(type: "int", nullable: false),
					FechaCompra = table.Column<DateTime>(type: "datetime2", nullable: false),
					QRPayload = table.Column<string>(type: "nvarchar(max)", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Tickets", x => x.Id);
					table.ForeignKey(
						name: "FK_Tickets_Eventos_EventoId",
						column: x => x.EventoId,
						principalTable: "Eventos",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_Tickets_Usuarios_UsuarioId",
						column: x => x.UsuarioId,
						principalTable: "Usuarios",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict); // Evita ciclos de borrado en cascada
				});

			migrationBuilder.CreateIndex(
				name: "IX_Tickets_EventoId",
				table: "Tickets",
				column: "EventoId");

			migrationBuilder.CreateIndex(
				name: "IX_Tickets_UsuarioId",
				table: "Tickets",
				column: "UsuarioId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "Tickets");
		}
	}
}