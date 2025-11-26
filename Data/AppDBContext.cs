using Microsoft.EntityFrameworkCore;
using Campus_Virtul_GRLL.Models;

namespace Campus_Virtul_GRLL.Data
{
    public class AppDBContext: DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
        {

        }

        public DbSet<Rol> Rols { get; set; }
        public DbSet<Permisos> Permisos { get; set; }
        public DbSet<Modulo> Modulos { get; set; }
        public DbSet <Solicitud> Solicituds { get; set; }
        public DbSet<SolicitudRevisión> SolicitudsRevision { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Rol>(tabla =>
            {
                tabla.HasKey(columna => columna.IdRol);

                tabla.Property(columna => columna.IdRol)
                .ValueGeneratedOnAdd();

                tabla.Property(columna => columna.NombreRol)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.Descripcion)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.Estado)
                .IsRequired();
            });

            modelBuilder.Entity<Permisos>(tabla =>
            {
                tabla.HasKey(columna => columna.IdPermisos);

                tabla.HasOne(columna => columna.Rol)
                .WithMany()
                .HasForeignKey(columna => columna.IdRol)
                .OnDelete(DeleteBehavior.Restrict);

                tabla.HasOne(columna => columna.Modulos)
                .WithMany()
                .HasForeignKey(columna => columna.IdModulo)
                .OnDelete(DeleteBehavior.Restrict);

                tabla.Property(columna => columna.Crear)
                .HasDefaultValue(false);

                tabla.Property(columna => columna.Editar)
                .HasDefaultValue(false);

                tabla.Property(columna => columna.Revisar)
                .HasDefaultValue(false);

                tabla.Property(columna => columna.Aprobar)
                .HasDefaultValue(false);

                tabla.Property(columna => columna.Visualizar)
                .HasDefaultValue(false);
            });

            modelBuilder.Entity<Modulo>(tabla =>
            {
                tabla.HasKey(columna => columna.IdModulo);

                tabla.Property(columna => columna.Titulo)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.Descripcion)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.Estado)
                .IsRequired();
            });

            modelBuilder.Entity<Solicitud>(tabla =>
            {
                tabla.HasKey(columna => columna.IdSolicitud);

                tabla.Property(columna => columna.IdSolicitud)
                .UseIdentityColumn()
                .ValueGeneratedOnAdd();

                tabla.Property(columna => columna.Nombres)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.Apellidos)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.DNI)
                .HasMaxLength(8)
                .IsRequired();

                tabla.Property(columna => columna.Telefono)
                .HasMaxLength(9)
                .IsRequired();

                tabla.Property(columna => columna.CorreoElectronico)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.Area)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.FechaSolicitud)
                .HasColumnType("date")
                .IsRequired();

                tabla.Property(columna => columna.Estado)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

                tabla.HasOne(columna => columna.Rol)
                .WithMany()
                .HasForeignKey(columna => columna.IdRol)
                .OnDelete(DeleteBehavior.Restrict);

                tabla.Ignore(columna => columna.NombreRol);
            });

            modelBuilder.Entity<SolicitudRevisión>(tabla =>
            {
                tabla.HasKey(columna => columna.IdSolicitudRevision);

                tabla.Property(columna => columna.IdSolicitudRevision)
                .UseIdentityColumn()
                .ValueGeneratedOnAdd();

                tabla.HasOne(columna => columna.Solicitud)
                .WithMany()
                .HasForeignKey(columna => columna.IdSolicitud)
                .OnDelete(DeleteBehavior.Restrict);

                tabla.Property(columna => columna.fechaRevision)
                .HasColumnType("date")
                .IsRequired();

                tabla.Property(columna => columna.observaciones)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.Estado)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            });

            modelBuilder.Entity<Usuario>(tabla =>
            {
                tabla.HasKey(columna => columna.IdUsuario);

                tabla.Property(columna => columna.IdUsuario)
                .UseIdentityColumn()
                .ValueGeneratedOnAdd();

                tabla.HasOne(columna => columna.Rol)
                .WithMany()
                .HasForeignKey(columna => columna.IdRol)
                .OnDelete(DeleteBehavior.Restrict);

                tabla.Property(columna => columna.Nombres)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.Apellidos)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.DNI)
                .HasMaxLength(8)
                .IsRequired();

                tabla.Property(columna => columna.Telefono)
                .HasMaxLength(15)
                .IsRequired();

                tabla.Property(columna => columna.CorreoElectronico)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.Area)
                .HasMaxLength(50)
                .IsRequired();

                tabla.Property(columna => columna.PrimerInicio)
                .HasDefaultValue(false);

                tabla.Property(columna => columna.ClaveTemporal)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.ClavePermanente)
                .HasMaxLength(100)
                .IsRequired();

                tabla.Property(columna => columna.FechaCreacion)
                .HasColumnType("date")
                .IsRequired();

                tabla.Property(columna => columna.FechaActualizacion)
                .HasColumnType("date")
                .IsRequired();

                tabla.Property(columna => columna.TokenRecuperacion)
                .HasMaxLength(100);

                tabla.Property(columna => columna.FechaExpiracionToken)
                .HasColumnType("datetime");

                tabla.Property(columna => columna.Estado)
                .IsRequired();
            });

            modelBuilder.Entity<Solicitud>().ToTable("Solicitud");
            modelBuilder.Entity<SolicitudRevisión>().ToTable("SolicitudRevisión");
            modelBuilder.Entity<Usuario>().ToTable("Usuario");
            modelBuilder.Entity<Rol>().ToTable("Rol");
            modelBuilder.Entity<Permisos>().ToTable("Permisos");
            modelBuilder.Entity<Modulo>().ToTable("Modulo");
        }
    }        
}
