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
    }
}
