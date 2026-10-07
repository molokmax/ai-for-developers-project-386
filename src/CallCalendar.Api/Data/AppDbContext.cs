using CallCalendar.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CallCalendar.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EventType> EventTypes => Set<EventType>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite хранит DateTime без информации о Kind; всё в UTC, Kind проставляется при материализации
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.Property(b => b.CustomerName).HasMaxLength(200);
            booking.Property(b => b.CustomerEmail).HasMaxLength(320);
            booking.Property(b => b.IdempotencyKey).HasMaxLength(100);

            booking.Property(b => b.StartUtc).HasConversion(utc);
            booking.Property(b => b.EndUtc).HasConversion(utc);
            booking.Property(b => b.CreatedUtc).HasConversion(utc);

            // Повтор с тем же Idempotency-Key возвращает первичный результат: ключ уникален глобально
            booking.HasIndex(b => b.IdempotencyKey).IsUnique();

            booking.HasIndex(b => b.StartUtc);
        });
    }
}
