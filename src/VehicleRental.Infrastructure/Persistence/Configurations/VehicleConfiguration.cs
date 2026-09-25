using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);

        // Os value objects viram colunas simples: a validação continua no domínio,
        // o banco guarda apenas a forma já normalizada.
        builder.Property(v => v.Plate)
            .HasConversion(plate => plate.Value, value => Plate.Create(value))
            .HasColumnName("plate")
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(v => v.Plate).IsUnique();

        builder.Property(v => v.Brand)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(v => v.Model)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(v => v.Year).IsRequired();

        builder.Property(v => v.Category)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.DailyRate)
            .HasConversion(money => money.Amount, value => Money.Create(value))
            .HasColumnName("daily_rate")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.CreatedAt).IsRequired();
    }
}
