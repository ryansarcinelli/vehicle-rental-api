using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleRental.Domain.Entities;

namespace VehicleRental.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();

        builder.Property(c => c.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.DriverLicense)
            .HasMaxLength(11)
            .IsRequired();

        builder.HasIndex(c => c.DriverLicense).IsUnique();

        builder.Property(c => c.Phone)
            .HasMaxLength(20)
            .IsRequired();
    }
}
