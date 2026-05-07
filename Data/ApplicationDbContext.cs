using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Models;
using RazorIdentity.Models.Montecarlo;
using RazorIdentity.Models.PmoFinance;
using RazorIdentity.Models.TallerCostos;

namespace RazorIdentity.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<MontecarloProject> MontecarloProjects { get; set; }

        // Taller de Rango de Costos
        public DbSet<TallerProyecto> TallerProyectos { get; set; }
        public DbSet<TallerFamilia> TallerFamilias { get; set; }
        public DbSet<TallerEmpresa> TallerEmpresas { get; set; }
        public DbSet<TallerContrato> TallerContratos { get; set; }
        public DbSet<TallerItem> TallerItems { get; set; }
        public DbSet<TallerRevisionRiesgos> TallerRevisionesRiesgos { get; set; }
        public DbSet<TallerRiesgo> TallerRiesgos { get; set; }
        public DbSet<TallerAnalisis> TallerAnalisis { get; set; }
        public DbSet<TallerMcResultado> TallerMcResultados { get; set; }

        // PMO — Vectores financieros (PostgreSQL; paridad tablas MySQL del PHP)
        public DbSet<PmoRealParcial> PmoRealParciales { get; set; }
        public DbSet<PmoRealAcumulado> PmoRealAcumulados { get; set; }
        public DbSet<PmoV0Parcial> PmoV0Parciales { get; set; }
        public DbSet<PmoV0Acumulada> PmoV0Acumuladas { get; set; }
        public DbSet<PmoNpcParcial> PmoNpcParciales { get; set; }
        public DbSet<PmoNpcAcumulado> PmoNpcAcumulados { get; set; }
        public DbSet<PmoApiParcial> PmoApiParciales { get; set; }
        public DbSet<PmoApiAcumulada> PmoApiAcumuladas { get; set; }
        public DbSet<PmoFinancieroSap> PmoFinancieroSaps { get; set; }
        public DbSet<PmoVcProject9c> PmoVcProject9cs { get; set; }

        // PMO — Avance físico / líneas base (cinco tablas; creadas por migración SQL cruda)
        public DbSet<PmoAvFisicoReal> PmoAvFisicoReales { get; set; }
        public DbSet<PmoAvFisicoNpc> PmoAvFisicoNpcs { get; set; }
        public DbSet<PmoAvFisicoPoa> PmoAvFisicoPoas { get; set; }
        public DbSet<PmoAvFisicoV0> PmoAvFisicoV0s { get; set; }
        public DbSet<PmoAvFisicoApi> PmoAvFisicoApis { get; set; }
        public DbSet<PmoPredictividad> PmoPredictividades { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<UserProfile>(entity =>
            {
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.UserId).HasMaxLength(450);
            });

            builder.Entity<MontecarloProject>(e => e.HasIndex(p => p.UserId));

            builder.Entity<TallerProyecto>(e =>
            {
                e.HasIndex(p => p.UserId);
                e.Property(p => p.UserId).HasMaxLength(450);
            });

            builder.Entity<TallerFamilia>(e =>
            {
                e.HasOne(f => f.Proyecto)
                 .WithMany(p => p.Familias)
                 .HasForeignKey(f => f.ProyectoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(f => f.ProyectoId);
            });

            builder.Entity<TallerEmpresa>(e =>
            {
                e.HasOne(x => x.Proyecto)
                 .WithMany(p => p.Empresas)
                 .HasForeignKey(x => x.ProyectoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => x.ProyectoId);
                e.HasIndex(x => new { x.ProyectoId, x.Rut }).IsUnique();
            });

            builder.Entity<TallerContrato>(e =>
            {
                e.HasOne(c => c.Proyecto)
                 .WithMany(p => p.Contratos)
                 .HasForeignKey(c => c.ProyectoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Familia)
                 .WithMany(f => f.Contratos)
                 .HasForeignKey(c => c.FamiliaId)
                 .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(c => c.Empresa)
                 .WithMany(em => em.Contratos)
                 .HasForeignKey(c => c.EmpresaId)
                 .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(c => c.ProyectoId);
                e.HasIndex(c => c.FamiliaId);
                e.HasIndex(c => c.EmpresaId);
            });

            builder.Entity<TallerItem>(e =>
            {
                e.HasOne(i => i.Contrato)
                 .WithMany(c => c.Items)
                 .HasForeignKey(i => i.ContratoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(i => i.ContratoId);
            });

            builder.Entity<TallerRevisionRiesgos>(e =>
            {
                e.HasOne(r => r.Proyecto)
                 .WithMany(p => p.Revisiones)
                 .HasForeignKey(r => r.ProyectoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.ProyectoId);
            });

            builder.Entity<TallerRiesgo>(e =>
            {
                e.HasOne(r => r.Revision)
                 .WithMany(rev => rev.Riesgos)
                 .HasForeignKey(r => r.RevisionId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.RevisionId);
            });

            builder.Entity<TallerAnalisis>(e =>
            {
                e.HasOne(a => a.Proyecto)
                 .WithMany(p => p.Analisis)
                 .HasForeignKey(a => a.ProyectoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(a => a.ProyectoId);
            });

            builder.Entity<TallerMcResultado>(e =>
            {
                e.HasOne(r => r.Contrato)
                 .WithMany()
                 .HasForeignKey(r => r.ContratoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.ContratoId);
            });

            // —— PMO financiero (TPC: una tabla física por vector) ——
            builder.Entity<PmoVectorFinancieroRow>(e =>
            {
                e.UseTpcMappingStrategy();
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
                e.Property(x => x.CentroCosto).HasColumnName("centro_costo").HasMaxLength(500);
                e.Property(x => x.Periodo).HasColumnName("periodo");
                e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(200);
                e.Property(x => x.CatVp).HasColumnName("cat_vp").HasMaxLength(200);
                e.Property(x => x.DetalleFactorial).HasColumnName("detalle_factorial").HasMaxLength(500);
                e.Property(x => x.Monto).HasColumnName("monto").HasPrecision(18, 4);
            });
            builder.Entity<PmoRealParcial>().ToTable("real_parcial");
            builder.Entity<PmoRealAcumulado>().ToTable("real_acumulado");
            builder.Entity<PmoV0Parcial>().ToTable("v0_parcial");
            builder.Entity<PmoV0Acumulada>().ToTable("v0_acumulada");
            builder.Entity<PmoNpcParcial>().ToTable("npc_parcial");
            builder.Entity<PmoNpcAcumulado>().ToTable("npc_acumulado");
            builder.Entity<PmoApiParcial>().ToTable("api_parcial");
            builder.Entity<PmoApiAcumulada>().ToTable("api_acumulada");
            foreach (var t in new[]
                     {
                         typeof(PmoRealParcial), typeof(PmoRealAcumulado), typeof(PmoV0Parcial), typeof(PmoV0Acumulada),
                         typeof(PmoNpcParcial), typeof(PmoNpcAcumulado), typeof(PmoApiParcial), typeof(PmoApiAcumulada)
                     })
                builder.Entity(t).HasIndex(nameof(PmoVectorFinancieroRow.ProyectoId));

            builder.Entity<PmoFinancieroSap>(e =>
            {
                e.ToTable("financiero_sap");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.IdSap).HasColumnName("id_sap").HasMaxLength(30);
                e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
                e.Property(x => x.CentroCostoNombre).HasColumnName("centro_costo_nombre").HasMaxLength(100);
                e.Property(x => x.VersionSap).HasColumnName("version_sap").HasMaxLength(50);
                e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
                e.Property(x => x.GrupoVersion).HasColumnName("grupo_version").HasMaxLength(50);
                e.Property(x => x.Periodo).HasColumnName("periodo");
                e.Property(x => x.Mo).HasColumnName("MO").HasPrecision(15, 2);
                e.Property(x => x.Ic).HasColumnName("IC").HasPrecision(15, 2);
                e.Property(x => x.Em).HasColumnName("EM").HasPrecision(15, 2);
                e.Property(x => x.Ie).HasColumnName("IE").HasPrecision(15, 2);
                e.Property(x => x.Sc).HasColumnName("SC").HasPrecision(15, 2);
                e.Property(x => x.Ad).HasColumnName("AD").HasPrecision(15, 2);
                e.Property(x => x.Cl).HasColumnName("CL").HasPrecision(15, 2);
                e.Property(x => x.Ct).HasColumnName("CT").HasPrecision(15, 2);
                e.Property(x => x.FechaCreacion).HasColumnName("fecha_creacion");
                e.Property(x => x.FechaActualizacion).HasColumnName("fecha_actualizacion");
                e.HasIndex(x => new { x.IdSap, x.ProyectoId }).IsUnique();
                e.HasIndex(x => x.ProyectoId);
                e.HasIndex(x => x.Periodo);
            });

            builder.Entity<PmoVcProject9c>(e =>
            {
                e.ToTable("vc_project_9c");
                e.HasKey(x => new { x.ProyectoId, x.IdC9 });
                e.Property(x => x.IdC9).HasColumnName("id_c9").HasMaxLength(20);
                e.Property(x => x.Periodo).HasColumnName("periodo");
                e.Property(x => x.CatVp).HasColumnName("cat_vp").HasMaxLength(30);
                e.Property(x => x.MonedaBase).HasColumnName("moneda_base");
                e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
                e.Property(x => x.Base).HasColumnName("base").HasPrecision(15, 2);
                e.Property(x => x.Cambio).HasColumnName("cambio").HasPrecision(15, 2);
                e.Property(x => x.Control).HasColumnName("control").HasPrecision(15, 2);
                e.Property(x => x.Tendencia).HasColumnName("tendencia").HasPrecision(15, 2);
                e.Property(x => x.Eat).HasColumnName("eat").HasPrecision(15, 2);
                e.Property(x => x.Compromiso).HasColumnName("compromiso").HasPrecision(15, 2);
                e.Property(x => x.Incurrido).HasColumnName("incurrido").HasPrecision(15, 2);
                e.Property(x => x.Financiero).HasColumnName("financiero").HasPrecision(15, 2);
                e.Property(x => x.PorComprometer).HasColumnName("por_comprometer").HasPrecision(15, 2);
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
                e.HasIndex(x => new { x.ProyectoId, x.Periodo });
            });

            // —— PMO avance físico (TPC; tablas ya existentes: no generar DDL en nuevas migraciones) ——
            builder.Entity<PmoAvFisicoFila>(e =>
            {
                e.UseTpcMappingStrategy();
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id").HasMaxLength(20);
                e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
                e.Property(x => x.Periodo).HasColumnName("periodo");
                e.Property(x => x.Vector).HasColumnName("vector").HasMaxLength(10);
                e.Property(x => x.IeParcial).HasColumnName("ie_parcial").HasPrecision(12, 8);
                e.Property(x => x.IeAcumulado).HasColumnName("ie_acumulado").HasPrecision(12, 8);
                e.Property(x => x.EmParcial).HasColumnName("em_parcial").HasPrecision(12, 8);
                e.Property(x => x.EmAcumulado).HasColumnName("em_acumulado").HasPrecision(12, 8);
                e.Property(x => x.MoParcial).HasColumnName("mo_parcial").HasPrecision(12, 8);
                e.Property(x => x.MoAcumulado).HasColumnName("mo_acumulado").HasPrecision(12, 8);
                e.Property(x => x.ApiParcial).HasColumnName("api_parcial").HasPrecision(12, 8);
                e.Property(x => x.ApiAcum).HasColumnName("api_acum").HasPrecision(12, 8);
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            });
            builder.Entity<PmoAvFisicoReal>().ToTable("av_fisico_real", t => t.ExcludeFromMigrations());
            builder.Entity<PmoAvFisicoNpc>().ToTable("av_fisico_npc", t => t.ExcludeFromMigrations());
            builder.Entity<PmoAvFisicoPoa>().ToTable("av_fisico_poa", t => t.ExcludeFromMigrations());
            builder.Entity<PmoAvFisicoV0>().ToTable("av_fisico_v0", t => t.ExcludeFromMigrations());
            builder.Entity<PmoAvFisicoApi>().ToTable("av_fisico_api", t => t.ExcludeFromMigrations());
            foreach (var t in new[]
                     {
                         typeof(PmoAvFisicoReal), typeof(PmoAvFisicoNpc), typeof(PmoAvFisicoPoa), typeof(PmoAvFisicoV0),
                         typeof(PmoAvFisicoApi)
                     })
                builder.Entity(t).HasIndex(nameof(PmoAvFisicoFila.ProyectoId));

            builder.Entity<PmoPredictividad>(e =>
            {
                e.ToTable("predictividad", t => t.ExcludeFromMigrations());
                e.HasKey(x => x.IdPredictivo);
                e.Property(x => x.IdPredictivo).HasColumnName("id_predictivo");
                e.Property(x => x.ProyectoId).HasColumnName("proyecto_id");
                e.Property(x => x.CentroCostoId).HasColumnName("id");
                e.Property(x => x.PeriodoPrediccion).HasColumnName("periodo_prediccion");
                e.Property(x => x.PorcentajePredicido).HasColumnName("porcentaje_predicido").HasPrecision(12, 6);
                e.Property(x => x.PeriodoCierreReal).HasColumnName("periodo_cierre_real");
                e.Property(x => x.ValorRealPorcentaje).HasColumnName("valor_real_porcentaje").HasPrecision(12, 6);
                e.HasIndex(x => x.ProyectoId);
                e.HasIndex(x => x.PeriodoPrediccion);
            });
        }
    }
}
