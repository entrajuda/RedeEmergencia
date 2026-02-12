using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Domain;

namespace REA.Emergencia.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<PedidoBem> PedidosBens => Set<PedidoBem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PedidoBem>().ToTable("PedidosBens");
    }
}
