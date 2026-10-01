using Microsoft.EntityFrameworkCore;
using EventosPeruIA.Api.Models;

namespace EventosPeruIA.Api.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
			: base(options)
		{
		}

		public DbSet<Usuario> Usuarios => Set<Usuario>();
		public DbSet<Categoria> Categorias => Set<Categoria>();
		public DbSet<Evento> Eventos => Set<Evento>();
		public DbSet<Ticket> Tickets => Set<Ticket>();

		// Agregamos este método para resolver el ciclo de cascadas en SQL Server:
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// 1. Evitar la cascada múltiple entre Usuario y Ticket
			modelBuilder.Entity<Ticket>()
				.HasOne(t => t.Usuario)
				.WithMany()
				.HasForeignKey(t => t.UsuarioId)
				.OnDelete(DeleteBehavior.Restrict);

			// 2. Si se elimina un Evento, sí se pueden eliminar sus Tickets en cascada
			modelBuilder.Entity<Ticket>()
				.HasOne(t => t.Evento)
				.WithMany()
				.HasForeignKey(t => t.EventoId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}