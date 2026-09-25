using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Infrastructure.Persistence.Configurations;

public sealed class RentalConfiguration : IEntityTypeConfiguration<Rental>
{
    public void Configure(EntityTypeBuilder<Rental> builder)
    {
        builder.ToTable("rentals");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.ReturnedAt).HasColumnType("date");

        builder.Property(r => r.DailyRateSnapshot)
            .HasConversion(money => money.Amount, value => Money.Create(value))
            .HasColumnName("daily_rate_snapshot")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(r => r.TotalAmount)
            .HasConversion(money => money.Amount, value => Money.Create(value))
            .HasColumnName("total_amount")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(r => r.LateFee)
            .HasConversion(money => money.Amount, value => Money.Create(value))
            .HasColumnName("late_fee")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Suporta a checagem de "cliente já tem aluguel ativo" e a listagem por cliente.
        builder.HasIndex(r => new { r.CustomerId, r.Status });
        builder.HasIndex(r => new { r.VehicleId, r.Status });
    }
}
