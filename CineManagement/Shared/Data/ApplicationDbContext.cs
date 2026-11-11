using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CineManagement.Models;

namespace CineManagement.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── CHUẨN KIẾN TRÚC RẠP CHIẾU PHIM DOANH NGHIỆP (Cinemas, Outbox, 10 Core Tables) ──
    // 0. cinemas (Chuỗi cụm rạp thuộc hãng)
    public DbSet<Cinema> Cinemas { get; set; }
    // 1. movies
    public DbSet<Movie> Movies { get; set; }
    // 2. auditoriums (screening_rooms)
    public DbSet<ScreeningRoom> ScreeningRooms { get; set; }
    // 3. seat_types
    public DbSet<SeatType> SeatTypes { get; set; }
    // 4. seats
    public DbSet<Seat> Seats { get; set; }
    // 5. showtimes
    public DbSet<Showtime> Showtimes { get; set; }
    // 6. showtime_seats
    public DbSet<ShowtimeSeat> ShowtimeSeats { get; set; }
    // 7. bookings
    public DbSet<Booking> Bookings { get; set; }
    // 8. tickets
    public DbSet<Ticket> Tickets { get; set; }
    // 9. concessions & booking_concessions
    public DbSet<FoodItem> FoodItems { get; set; }
    public DbSet<BookingConcession> BookingConcessions { get; set; }
    // 10. payments
    public DbSet<Payment> Payments { get; set; }
    // 11. outbox_messages (Transactional Outbox Pattern)
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    // ── CÁC BẢNG BỔ TRỢ NGHIỆP VỤ ───────────────────────────────────────────
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Cinema (cinemas) ────────────────────────────────────────────────
        builder.Entity<Cinema>()
            .HasKey(c => c.CinemaId);
        builder.Entity<Cinema>()
            .HasIndex(c => c.Code)
            .IsUnique();

        // ── OutboxMessage (outboxmessages) ──────────────────────────────────
        builder.Entity<OutboxMessage>()
            .HasKey(o => o.Id);
        builder.Entity<OutboxMessage>()
            .HasIndex(o => new { o.ProcessedAt, o.RetryCount });

        // ── Staff -> User (1-1) ─────────────────────────────────────────────
        builder.Entity<Staff>()
            .HasOne(s => s.User)
            .WithOne(u => u.Staff)
            .HasForeignKey<Staff>(s => s.UserId);

        // ── Movie -> Genre (N-1) ────────────────────────────────────────────
        builder.Entity<Movie>()
            .HasOne(m => m.Genre)
            .WithMany(g => g.Movies)
            .HasForeignKey(m => m.GenreId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── ScreeningRoom (auditoriums) & Cinema (N-1) ──────────────────────
        builder.Entity<ScreeningRoom>()
            .HasKey(r => r.RoomId);
        builder.Entity<ScreeningRoom>()
            .HasOne(r => r.Cinema)
            .WithMany(c => c.ScreeningRooms)
            .HasForeignKey(r => r.CinemaId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<ScreeningRoom>().Ignore(r => r.Capacity);

        // ── SeatType (seat_types) ───────────────────────────────────────────
        builder.Entity<SeatType>()
            .HasKey(st => st.SeatTypeId);
        builder.Entity<SeatType>()
            .HasIndex(st => st.Code)
            .IsUnique();
        builder.Entity<SeatType>()
            .Property(st => st.Surcharge)
            .HasPrecision(18, 2);

        // ── Seat (seats) ────────────────────────────────────────────────────
        builder.Entity<Seat>()
            .HasOne(s => s.Room)
            .WithMany(r => r.Seats)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Seat>()
            .HasOne(s => s.SeatType)
            .WithMany(st => st.Seats)
            .HasForeignKey(s => s.SeatTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique index: Mỗi phòng chiếu chỉ có 1 ghế tại tọa độ lưới (GridRow, GridCol)
        builder.Entity<Seat>()
            .HasIndex(s => new { s.RoomId, s.GridRow, s.GridCol })
            .IsUnique();

        builder.Entity<Seat>().Ignore(s => s.SeatCode);

        // ── Showtime (showtimes) ────────────────────────────────────────────
        builder.Entity<Showtime>()
            .HasOne(st => st.Movie)
            .WithMany(m => m.Showtimes)
            .HasForeignKey(st => st.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Showtime>()
            .HasOne(st => st.Room)
            .WithMany(r => r.Showtimes)
            .HasForeignKey(st => st.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Showtime>()
            .Property(s => s.TicketPrice)
            .HasPrecision(18, 2);

        // ── ShowtimeSeat (showtime_seats) ───────────────────────────────────
        builder.Entity<ShowtimeSeat>()
            .HasKey(ss => ss.ShowtimeSeatId);

        builder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.Showtime)
            .WithMany(st => st.ShowtimeSeats)
            .HasForeignKey(ss => ss.ShowtimeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.Seat)
            .WithMany(s => s.ShowtimeSeats)
            .HasForeignKey(ss => ss.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.SeatType)
            .WithMany(st => st.ShowtimeSeats)
            .HasForeignKey(ss => ss.SeatTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.HeldByUser)
            .WithMany()
            .HasForeignKey(ss => ss.HeldByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique index: Mỗi ghế chỉ xuất hiện 1 lần trong 1 suất chiếu
        builder.Entity<ShowtimeSeat>()
            .HasIndex(ss => new { ss.ShowtimeId, ss.SeatId })
            .IsUnique();

        builder.Entity<ShowtimeSeat>()
            .Property(ss => ss.Price)
            .HasPrecision(18, 2);

        // ── Booking (bookings) ──────────────────────────────────────────────
        builder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.Showtime)
            .WithMany(st => st.Bookings)
            .HasForeignKey(b => b.ShowtimeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasIndex(b => b.BookingCode)
            .IsUnique();

        builder.Entity<Booking>()
            .HasOne(b => b.Voucher)
            .WithMany(v => v.Bookings)
            .HasForeignKey(b => b.VoucherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Booking>().Property(b => b.TicketAmount).HasPrecision(18, 2);
        builder.Entity<Booking>().Property(b => b.FnBAmount).HasPrecision(18, 2);
        builder.Entity<Booking>().Property(b => b.TotalAmount).HasPrecision(18, 2);
        builder.Entity<Booking>().Property(b => b.DiscountAmount).HasPrecision(18, 2);
        builder.Entity<Booking>().Property(b => b.FinalAmount).HasPrecision(18, 2);

        // ── Ticket (tickets) ────────────────────────────────────────────────
        builder.Entity<Ticket>()
            .HasOne(t => t.Booking)
            .WithMany(b => b.Tickets)
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Ticket>()
            .HasOne(t => t.Showtime)
            .WithMany(st => st.Tickets)
            .HasForeignKey(t => t.ShowtimeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Ticket>()
            .HasOne(t => t.Seat)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Ticket>()
            .HasOne(t => t.ShowtimeSeat)
            .WithOne(ss => ss.Ticket)
            .HasForeignKey<Ticket>(t => t.ShowtimeSeatId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Ticket>()
            .HasOne(t => t.CheckedInBy)
            .WithMany()
            .HasForeignKey(t => t.CheckedInByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Ticket>()
            .HasOne(t => t.PrintedBy)
            .WithMany()
            .HasForeignKey(t => t.PrintedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Ticket>()
            .HasIndex(t => t.TicketCode)
            .IsUnique();

        builder.Entity<Ticket>()
            .HasIndex(t => new { t.BookingId, t.SeatId })
            .IsUnique();

        builder.Entity<Ticket>()
            .HasIndex(t => new { t.ShowtimeId, t.SeatId });

        builder.Entity<Ticket>().Property(t => t.UnitPrice).HasPrecision(18, 2);

        // ── FoodItem & BookingConcession (concessions) ───────────────────────
        builder.Entity<FoodItem>().Property(f => f.Price).HasPrecision(18, 2);

        builder.Entity<BookingConcession>()
            .HasKey(bc => bc.BookingConcessionId);

        builder.Entity<BookingConcession>()
            .HasOne(bc => bc.Booking)
            .WithMany(b => b.BookingConcessions)
            .HasForeignKey(bc => bc.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BookingConcession>()
            .HasOne(bc => bc.FoodItem)
            .WithMany(f => f.BookingConcessions)
            .HasForeignKey(bc => bc.FoodItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BookingConcession>().Property(bc => bc.UnitPrice).HasPrecision(18, 2);
        builder.Entity<BookingConcession>().Ignore(bc => bc.SubTotal);

        // ── Payment (payments) ──────────────────────────────────────────────
        builder.Entity<Payment>()
            .HasKey(p => p.PaymentId);

        builder.Entity<Payment>()
            .HasOne(p => p.Booking)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Entity<Payment>()
            .HasIndex(p => p.TransactionCode);

        // ── Voucher & Review ────────────────────────────────────────────────
        builder.Entity<Voucher>()
            .HasIndex(v => v.Code)
            .IsUnique();

        builder.Entity<Voucher>().Property(v => v.DiscountPercent).HasPrecision(5, 2);
        builder.Entity<Voucher>().Property(v => v.DiscountAmount).HasPrecision(18, 2);
        builder.Entity<Voucher>().Property(v => v.MinOrderAmount).HasPrecision(18, 2);

        builder.Entity<Review>()
            .HasOne(r => r.Movie)
            .WithMany(m => m.Reviews)
            .HasForeignKey(r => r.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.MovieId })
            .IsUnique();

        builder.Entity<Movie>().Property(m => m.Rating).HasPrecision(3, 1);
        builder.Entity<Staff>().Property(s => s.Salary).HasPrecision(18, 2);
    }
}
