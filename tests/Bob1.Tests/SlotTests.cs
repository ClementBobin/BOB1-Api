using Bob1.Modules.Participation.Domain;
using Bob1.Shared.Contracts.Participation;

namespace Bob1.Tests;

public class SlotTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);

    private readonly Guid _matchId = Guid.NewGuid();

    private Slot NewSlot(SlotKind kind = SlotKind.Volunteer, string role = "Buvette", int capacity = 2) =>
        Slot.Create(_matchId, kind, role, capacity, startsAtUtc: null, endsAtUtc: null);

    [Fact]
    public void Create_starts_empty()
    {
        var slot = NewSlot(SlotKind.Staff, "Arbitre 1", 1);

        Assert.Equal(_matchId, slot.MatchId);
        Assert.Equal(SlotKind.Staff, slot.Kind);
        Assert.Equal("Arbitre 1", slot.Role);
        Assert.Equal(1, slot.Capacity);
        Assert.Equal(0, slot.Filled);
        Assert.Empty(slot.Signups);
    }

    [Fact]
    public void Join_fills_the_slot_and_raises_SlotJoined()
    {
        var slot = NewSlot();
        var user = Guid.NewGuid();
        var family = Guid.NewGuid();

        var result = slot.Join(user, family, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, slot.Filled);
        var signup = Assert.Single(slot.Signups);
        Assert.Equal(user, signup.UserId);
        Assert.Equal(family, signup.FamilyId);

        var raised = Assert.IsType<SlotJoined>(Assert.Single(slot.DomainEvents));
        Assert.Equal(slot.Id, raised.SlotId);
        Assert.Equal(signup.Id, raised.SignupId);
        Assert.Equal(_matchId, raised.MatchId);
        Assert.Equal("Buvette", raised.Role);
        Assert.False(raised.IsStaff);
        Assert.Equal(user, raised.UserId);
        Assert.Equal(family, raised.FamilyId);
    }

    [Fact]
    public void Joining_a_staff_slot_is_flagged_as_staff()
    {
        var slot = NewSlot(SlotKind.Staff, "MAR");

        slot.Join(Guid.NewGuid(), familyId: null, Now);

        var raised = Assert.IsType<SlotJoined>(Assert.Single(slot.DomainEvents));
        Assert.True(raised.IsStaff);
        Assert.Null(raised.FamilyId);
    }

    [Fact]
    public void The_same_person_cannot_join_twice()
    {
        var slot = NewSlot();
        var user = Guid.NewGuid();
        slot.Join(user, null, Now);

        var again = slot.Join(user, null, Now);

        Assert.Equal("slot.already_joined", again.Error.Code);
        Assert.Equal(1, slot.Filled);
    }

    [Fact]
    public void A_full_slot_refuses_new_people()
    {
        var slot = NewSlot(capacity: 1);
        slot.Join(Guid.NewGuid(), null, Now);

        var result = slot.Join(Guid.NewGuid(), null, Now);

        Assert.Equal("slot.full", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(1, slot.Filled);
    }

    [Fact]
    public void Leave_frees_the_place_and_raises_SlotLeft()
    {
        var slot = NewSlot(capacity: 1);
        var user = Guid.NewGuid();
        var family = Guid.NewGuid();
        slot.Join(user, family, Now);
        var signupId = slot.Signups.Single().Id;
        slot.ClearDomainEvents();

        var result = slot.Leave(user);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, slot.Filled);
        Assert.Empty(slot.Signups);
        var raised = Assert.IsType<SlotLeft>(Assert.Single(slot.DomainEvents));
        Assert.Equal(signupId, raised.SignupId);
        Assert.Equal(user, raised.UserId);
        Assert.Equal(family, raised.FamilyId);
        Assert.True(slot.Join(Guid.NewGuid(), null, Now).IsSuccess);   // the place can be taken again
    }

    [Fact]
    public void Leave_without_a_signup_is_not_found()
    {
        var slot = NewSlot();

        var result = slot.Leave(Guid.NewGuid());

        Assert.Equal("slot.not_joined", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Empty(slot.DomainEvents);
    }

    [Fact]
    public void Update_cannot_drop_capacity_below_the_people_already_signed_up()
    {
        var slot = NewSlot(capacity: 3);
        slot.Join(Guid.NewGuid(), null, Now);
        slot.Join(Guid.NewGuid(), null, Now);

        var result = slot.Update("Buvette", 1, null, null);

        Assert.Equal("slot.capacity_below_filled", result.Error.Code);
        Assert.Equal(3, slot.Capacity);
    }

    [Fact]
    public void Update_changes_role_capacity_and_window()
    {
        var slot = NewSlot();
        var starts = Now.AddDays(1);
        var ends = starts.AddHours(2);

        var result = slot.Update("Chrono", 4, starts, ends);

        Assert.True(result.IsSuccess);
        Assert.Equal("Chrono", slot.Role);
        Assert.Equal(4, slot.Capacity);
        Assert.Equal(starts, slot.StartsAtUtc);
        Assert.Equal(ends, slot.EndsAtUtc);
    }

    [Fact]
    public void Confirming_presence_raises_PresenceConfirmed()
    {
        var family = Guid.NewGuid();
        var user = Guid.NewGuid();

        var confirmation = PresenceConfirmation.Create(_matchId, family, user, Now);

        var raised = Assert.IsType<PresenceConfirmed>(Assert.Single(confirmation.DomainEvents));
        Assert.Equal(_matchId, raised.MatchId);
        Assert.Equal(user, raised.UserId);
        Assert.Equal(family, raised.FamilyId);
        Assert.Equal(Now, confirmation.ConfirmedAtUtc);
    }
}
