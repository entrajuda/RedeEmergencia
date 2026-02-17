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
    public DbSet<PedidoEstadoLog> PedidoEstadoLogs => Set<PedidoEstadoLog>();
    public DbSet<Distrito> Distritos => Set<Distrito>();
    public DbSet<Concelho> Concelhos => Set<Concelho>();
    public DbSet<Zinf> Zinfs => Set<Zinf>();
    public DbSet<UserZinf> UserZinfs => Set<UserZinf>();
    public DbSet<CodigoPostal> CodigosPostais => Set<CodigoPostal>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();

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

            entity.Property(e => e.PublicId)
                .IsRequired()
                .HasDefaultValueSql("NEWID()");

            entity.HasIndex(e => e.PublicId)
                .IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.State)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(e => e.TipoPedido)
                .WithMany(e => e.Pedidos)
                .HasForeignKey(e => e.TipoPedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Zinf)
                .WithMany()
                .HasForeignKey(e => e.ZinfId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PedidoEstadoLog>(entity =>
        {
            entity.ToTable("PedidoEstadoLogs");

            entity.Property(e => e.ChangedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.FromState)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ToState)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ChangedBy)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasOne(e => e.Pedido)
                .WithMany(e => e.EstadoLogs)
                .HasForeignKey(e => e.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
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

            entity.Property(e => e.ZinfId)
                .HasColumnName("ZINFId");

            entity.HasOne(e => e.Zinf)
                .WithMany(e => e.Concelhos)
                .HasForeignKey(e => e.ZinfId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Distrito)
                .WithMany(e => e.Concelhos)
                .HasForeignKey(e => e.DistritoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Zinf>(entity =>
        {
            entity.ToTable("Zinfs");

            entity.Property(e => e.Nome)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(e => e.Nome)
                .IsUnique();
        });

        modelBuilder.Entity<UserZinf>(entity =>
        {
            entity.ToTable("UserZinfs");

            entity.HasKey(e => new { e.UserPrincipalName, e.ZinfId });

            entity.Property(e => e.UserPrincipalName)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasOne(e => e.Zinf)
                .WithMany(e => e.UserZinfs)
                .HasForeignKey(e => e.ZinfId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSettings");

            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Value)
                .IsRequired();

            entity.HasIndex(e => e.Key)
                .IsUnique();
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("EmailLogs");

            entity.Property(e => e.SentAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.Recipients)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(300);
        });

        modelBuilder.Entity<Instituicao>(entity =>
        {
            entity.ToTable("Instituicoes");

            entity.Property(e => e.CodigoEA)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => e.CodigoEA)
                .IsUnique();

            entity.Property(e => e.Nome)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.PessoaContacto)
                .HasMaxLength(200);

            entity.Property(e => e.Telefone)
                .HasMaxLength(50);

            entity.Property(e => e.Telemovel)
                .HasMaxLength(50);

            entity.Property(e => e.Email1)
                .HasMaxLength(200);

            entity.Property(e => e.Localidade)
                .HasMaxLength(200);

            entity.HasOne(e => e.Concelho)
                .WithMany()
                .HasForeignKey(e => e.ConcelhoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Distrito)
                .WithMany()
                .HasForeignKey(e => e.DistritoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Zinf)
                .WithMany()
                .HasForeignKey(e => e.ZinfId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CodigoPostal)
                .WithMany()
                .HasForeignKey(e => e.CodigoPostalNumero)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
