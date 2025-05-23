using Microsoft.EntityFrameworkCore;
using ManufacturingManagementSystem.Models;

namespace ManufacturingManagementSystem.Data
{
    public class ProductionContext : DbContext
    {
        public ProductionContext(DbContextOptions<ProductionContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductionLine> ProductionLines { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<ProductMaterial> ProductMaterials { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Преобразование имен таблиц и столбцов в нижний регистр
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName().ToLower());
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.GetColumnName().ToLower());
                }
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(key.GetName().ToLower());
                }
                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(index.GetDatabaseName().ToLower());
                }
                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    foreignKey.SetConstraintName(foreignKey.GetConstraintName().ToLower());
                }
            }

            // Настройка связи многие-ко-многим для ProductMaterial
            modelBuilder.Entity<ProductMaterial>()
                .HasKey(pm => new { pm.ProductId, pm.MaterialId });

            modelBuilder.Entity<ProductMaterial>()
                .HasOne(pm => pm.Product)
                .WithMany(p => p.ProductMaterials)
                .HasForeignKey(pm => pm.ProductId);

            modelBuilder.Entity<ProductMaterial>()
                .HasOne(pm => pm.Material)
                .WithMany()
                .HasForeignKey(pm => pm.MaterialId);

            // Настройка связи между ProductionLine и WorkOrder (один-ко-многим)
            modelBuilder.Entity<WorkOrder>()
                .HasOne(wo => wo.ProductionLine)
                .WithMany(pl => pl.WorkOrders)
                .HasForeignKey(wo => wo.ProductionLineId)
                .IsRequired(false);

            // Настройка связи ProductionLine -> CurrentWorkOrder (один-к-одному, опционально)
            modelBuilder.Entity<ProductionLine>()
                .HasOne(pl => pl.CurrentWorkOrder)
                .WithOne()
                .HasForeignKey<ProductionLine>(pl => pl.CurrentWorkOrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Установка значений по умолчанию для Status
            modelBuilder.Entity<ProductionLine>()
                .Property(pl => pl.Status)
                .HasDefaultValue("Stopped");

            modelBuilder.Entity<WorkOrder>()
                .HasKey(wo => wo.Id);

            modelBuilder.Entity<WorkOrder>()
                .Property(wo => wo.Id)
                .ValueGeneratedOnAdd(); // Убедимся, что Id генерируется автоматически

            modelBuilder.Entity<WorkOrder>()
                .HasOne(wo => wo.Product)
                .WithMany()
                .HasForeignKey(wo => wo.ProductId);

            modelBuilder.Entity<WorkOrder>()
                .HasOne(wo => wo.ProductionLine)
                .WithMany(pl => pl.WorkOrders)
                .HasForeignKey(wo => wo.ProductionLineId)
                .IsRequired(false); // Указываем, что ProductionLineId опционален

            modelBuilder.Entity<ProductionLine>()
                .HasOne(pl => pl.CurrentWorkOrder)
                .WithMany()
                .HasForeignKey(pl => pl.CurrentWorkOrderId)
                .IsRequired(false);
        }
    }
}