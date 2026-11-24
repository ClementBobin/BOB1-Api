using Bob1.Modules.Carpool.Domain;
using Bob1.Shared.Contracts.Carpool;

namespace Bob1.Tests;

public class RideTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Departure = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

    private readonly Guid _matchId = Guid.NewGuid();
    private readonly Guid _driver = Guid.NewGuid();

    private Ride NewRide(int seats = 3) => Ride.Offer(_matchId, _driver, seats, Departure, "Meet at the gym", Now);

    [Fact]
    public void Offer_creates_a_ride_with_all_its_seats_free()
    {
        var ride = NewRide(seats: 4);

        Assert.Equal(_matchId, ride.MatchId);
        Assert.Equal(_driver, ride.DriverUserId);
        Assert.Equal(4, ride.Seats);
        Assert.Equal(4, ride.SeatsLeft);
        Assert.Equal(Departure, ride.DepartureUtc);
        Assert.Equal("Meet at the gym", ride.Notes);
        Assert.Equal(Now, ride.CreatedAtUtc);
        Assert.Empty(ride.Bookings);
    }

    [Fact]
    public void Offer_raises_RideOffered()
    {
        var ride = NewRide();

        var raised = Assert.IsType<RideOffered>(Assert.Single(ride.DomainEvents));
        Assert.Equal(ride.Id, raised.RideId);
        Assert.Equal(_matchId, raised.MatchId);
        Assert.Equal(_driver, raised.DriverUserId);
    }

    [Fact]
    public void Book_takes_one_seat_per_passenger()
    {
        var ride = NewRide(seats: 3);
        var passenger = Guid.NewGuid();

        var result = ride.Book(passenger, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, ride.SeatsLeft);
        var booking = Assert.Single(ride.Bookings);
        Assert.Equal(passenger, booking.UserId);
        Assert.Equal(ride.Id, booking.RideId);
    }

    [Fact]
    public void The_driver_cannot_book_their_own_ride()
    {
        var ride = NewRide();

        var result = ride.Book(_driver, Now);

        Assert.Equal("ride.own_ride", result.Error.Code);
        Assert.Equal(3, ride.SeatsLeft);
    }

    [Fact]
    public void A_passenger_cannot_book_twice()
    {
        var ride = NewRide();
        var passenger = Guid.NewGuid();
        ride.Book(passenger, Now);

        var again = ride.Book(passenger, Now);

        Assert.Equal("ride.already_booked", again.Error.Code);
        Assert.Equal(2, ride.SeatsLeft);
    }

    [Fact]
    public void A_full_ride_refuses_new_bookings()
    {
        var ride = NewRide(seats: 1);
        ride.Book(Guid.NewGuid(), Now);

        var result = ride.Book(Guid.NewGuid(), Now);

        Assert.Equal("ride.full", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, ride.SeatsLeft);
    }

    [Fact]
    public void CancelBooking_frees_the_seat()
    {
        var ride = NewRide(seats: 1);
        var passenger = Guid.NewGuid();
        ride.Book(passenger, Now);

        var result = ride.CancelBooking(passenger);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, ride.SeatsLeft);
        Assert.Empty(ride.Bookings);
        Assert.True(ride.Book(Guid.NewGuid(), Now).IsSuccess);   // the freed seat can be taken again
    }

    [Fact]
    public void CancelBooking_without_a_booking_is_not_found()
    {
        var ride = NewRide();

        var result = ride.CancelBooking(Guid.NewGuid());

        Assert.Equal("booking.not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Update_changes_seats_departure_and_notes()
    {
        var ride = NewRide(seats: 3);
        var later = Departure.AddHours(1);

        var result = ride.Update(5, later, "Parking behind the school");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, ride.Seats);
        Assert.Equal(later, ride.DepartureUtc);
        Assert.Equal("Parking behind the school", ride.Notes);
    }

    [Fact]
    public void Update_cannot_drop_below_the_seats_already_booked()
    {
        var ride = NewRide(seats: 3);
        ride.Book(Guid.NewGuid(), Now);
        ride.Book(Guid.NewGuid(), Now);

        var result = ride.Update(1, Departure, null);

        Assert.Equal("ride.seats_below_booked", result.Error.Code);
        Assert.Equal(3, ride.Seats);   // unchanged
    }

    [Fact]
    public void Update_may_shrink_to_exactly_the_booked_seats()
    {
        var ride = NewRide(seats: 3);
        ride.Book(Guid.NewGuid(), Now);
        ride.Book(Guid.NewGuid(), Now);

        Assert.True(ride.Update(2, Departure, null).IsSuccess);
        Assert.Equal(0, ride.SeatsLeft);
    }

    [Fact]
    public void Cancel_raises_RideCancelled()
    {
        var ride = NewRide();
        ride.ClearDomainEvents();

        var result = ride.Cancel();

        Assert.True(result.IsSuccess);
        var raised = Assert.IsType<RideCancelled>(Assert.Single(ride.DomainEvents));
        Assert.Equal(ride.Id, raised.RideId);
        Assert.Equal(_matchId, raised.MatchId);
        Assert.Equal(_driver, raised.DriverUserId);
    }
}
