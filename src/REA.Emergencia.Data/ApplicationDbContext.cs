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
    public DbSet<TipoPedido> TiposPedido => Set<TipoPedido>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Distrito> Distritos => Set<Distrito>();
    public DbSet<Concelho> Concelhos => Set<Concelho>();
    public DbSet<CodigoPostal> CodigosPostais => Set<CodigoPostal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PedidoBem>().ToTable("PedidosBens");

        modelBuilder.Entity<TipoPedido>(entity =>
        {
            entity.ToTable("TiposPedido");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.Workflow)
                .IsRequired();

            entity.Property(e => e.TableName)
                .IsRequired()
                .HasMaxLength(200);
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("Pedidos");

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.State)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(e => e.TipoPedido)
                .WithMany(e => e.Pedidos)
                .HasForeignKey(e => e.TipoPedidoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Distrito>(entity =>
        {
            entity.ToTable("Distritos");

            entity.Property(e => e.Nome)
                .HasColumnName("Distrito")
                .IsRequired()
                .HasMaxLength(200);
        });

        modelBuilder.Entity<Concelho>(entity =>
        {
            entity.ToTable("Concelhos");

            entity.Property(e => e.Nome)
                .HasColumnName("Concelho")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ZINF)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(e => e.Distrito)
                .WithMany(e => e.Concelhos)
                .HasForeignKey(e => e.DistritoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CodigoPostal>(entity =>
        {
            entity.ToTable("CodigosPostais");

            entity.HasKey(e => e.Numero);
            entity.Property(e => e.Numero).ValueGeneratedNever();
            entity.ToTable(t => t.HasCheckConstraint("CK_CodigosPostais_Numero_Range", "[Numero] >= 1000000 AND [Numero] <= 9999999"));

            entity.Property(e => e.Freguesia)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasOne(e => e.Concelho)
                .WithMany(e => e.CodigosPostais)
                .HasForeignKey(e => e.ConcelhoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
