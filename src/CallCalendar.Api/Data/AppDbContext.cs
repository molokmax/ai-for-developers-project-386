using CallCalendar.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CallCalendar.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeSlot>()
            .HasIndex(slot => slot.StartUtc);

        modelBuilder.Entity<Booking>()
            .HasOne(booking => booking.TimeSlot)
            .WithMany(slot => slot.Bookings)
            .HasForeignKey(booking => booking.TimeSlotId);
    }
}
