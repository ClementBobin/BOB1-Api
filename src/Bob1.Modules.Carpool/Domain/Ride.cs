using Bob1.Shared.Contracts.Carpool;

namespace Bob1.Modules.Carpool.Domain;

internal sealed class Ride : Entity
{
    private readonly List<RideBooking> _bookings = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MatchId { get; private set; }
    public Guid DriverUserId { get; private set; }
    public int Seats { get; private set; }
    public DateTime DepartureUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<RideBooking> Bookings => _bookings;
    public int SeatsLeft => Seats - _bookings.Count;   // one seat per booking

    private Ride() { }

    public static Ride Offer(Guid matchId, Guid driverUserId, int seats, DateTime departureUtc, string? notes, DateTime nowUtc)
    {
        var ride = new Ride
        {
            MatchId = matchId, DriverUserId = driverUserId, Seats = seats,
            DepartureUtc = departureUtc, Notes = notes, CreatedAtUtc = nowUtc
        };
        ride.Raise(new RideOffered(ride.Id, matchId, driverUserId));
        return ride;
    }

    public Result Update(int seats, DateTime departureUtc, string? notes)
    {
        if (seats < _bookings.Count)
            return Error.Conflict("ride.seats_below_booked", $"{_bookings.Count} seat(s) are already booked.");
        Seats = seats; DepartureUtc = departureUtc; Notes = notes;
        return Result.Success();
    }

    /// <summary>Raises RideCancelled; the caller then deletes the ride (there is no "cancelled" flag any more).</summary>
    public Result Cancel()
    {
        Raise(new RideCancelled(Id, MatchId, DriverUserId));
        return Result.Success();
    }

    public Result Book(Guid userId, DateTime nowUtc)
    {
        if (userId == DriverUserId) return Error.Conflict("ride.own_ride", "You cannot book your own ride.");
        if (_bookings.Any(b => b.UserId == userId)) return Error.Conflict("ride.already_booked", "You already booked this ride.");
        if (SeatsLeft < 1) return Error.Conflict("ride.full", "Not enough seats left.");

        _bookings.Add(RideBooking.Create(Id, userId, nowUtc));
        return Result.Success();
    }

    public Result CancelBooking(Guid userId)
    {
        var booking = _bookings.SingleOrDefault(b => b.UserId == userId);
        if (booking is null) return Error.NotFound("booking.not_found", "You have no booking on this ride.");
        _bookings.Remove(booking);
        return Result.Success();
    }
}

internal sealed class RideBooking
{
    public Guid RideId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RideBooking() { }

    public static RideBooking Create(Guid rideId, Guid userId, DateTime nowUtc) =>
        new() { RideId = rideId, UserId = userId, CreatedAtUtc = nowUtc };
}
