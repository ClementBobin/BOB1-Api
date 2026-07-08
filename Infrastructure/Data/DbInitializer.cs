using Bogus;
using NLog;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Data;

using Domain.Entities;
using Domain.Enums;

/// <summary>
/// Seeds the database with realistic fake data using the Bogus library.
/// Only runs when the DB is empty — safe to call on every startup in Development.
/// </summary>
public static class DbInitializer
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            Logger.Info("Database already seeded — skipping.");
            return;
        }

        Logger.Info("Seeding database…");

        Randomizer.Seed = new Random(42);
        var faker = new Faker("fr");

        // ── 1. Divisions ──────────────────────────────────────────────────
        var divisions = new[]
        {
            new Division { Id = Guid.NewGuid(), Name = "U7" },
            new Division { Id = Guid.NewGuid(), Name = "U9" },
            new Division { Id = Guid.NewGuid(), Name = "U11" },
            new Division { Id = Guid.NewGuid(), Name = "U13" },
            new Division { Id = Guid.NewGuid(), Name = "U15" },
            new Division { Id = Guid.NewGuid(), Name = "U17" },
        };
        db.Divisions.AddRange(divisions);

        // ── 2. Locations ──────────────────────────────────────────────────
        var locationFaker = new Faker<Location>("fr")
            .RuleFor(l => l.Id, _ => Guid.NewGuid())
            .RuleFor(l => l.Name, f => $"Gymnase {f.Address.City()}")
            .RuleFor(l => l.Address, f => f.Address.FullAddress());

        var locations = locationFaker.Generate(5);
        db.Locations.AddRange(locations);

        // ── 3. Teams (2 per division) ─────────────────────────────────────
        var teams = divisions.SelectMany(div =>
        {
            return new[]
            {
                new Team { Id = Guid.NewGuid(), Name = $"{faker.Address.City()} {div.Name} A", DivisionId = div.Id },
                new Team { Id = Guid.NewGuid(), Name = $"{faker.Address.City()} {div.Name} B", DivisionId = div.Id },
            };
        }).ToList();
        db.Teams.AddRange(teams);

        // ── 4. Users ──────────────────────────────────────────────────────
        var userFaker = new Faker<User>("fr")
            .RuleFor(u => u.Id, _ => Guid.NewGuid())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName).ToLower())
            .RuleFor(u => u.PasswordHash, _ => BCrypt.Net.BCrypt.HashPassword("Password123!"))
            .RuleFor(u => u.Role, _ => UserRole.Official);

        var officials = userFaker.Generate(10);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Admin",
            LastName = "Bob1",
            Email = "admin@bob1.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Role = UserRole.Admin,
        };

        db.Users.Add(admin);
        db.Users.AddRange(officials);

        // ── 5. PointRules (one per OfficialRole) ──────────────────────────
        var pointRules = Enum.GetValues<OfficialRole>().Select(role => new PointRule
        {
            Id = Guid.NewGuid(),
            Role = role,
            PointsOnJ15 = role is OfficialRole.Arbitre1 ? 3 : 2,
            PointsOnJ4 = role is OfficialRole.Arbitre1 ? 5 : 4,
            PointsEmergency = role is OfficialRole.Arbitre1 ? 8 : 6,
        }).ToList();
        db.PointRules.AddRange(pointRules);

        // ── 6. Matches (2 per division, spread over next 3 months) ────────
        var allRoles = Enum.GetValues<OfficialRole>().ToList();
        var matches = new List<Match>();

        foreach (var div in divisions)
        {
            var divTeams = teams.Where(t => t.DivisionId == div.Id).ToList();

            for (int i = 0; i < 2; i++)
            {
                var matchDate = DateTime.UtcNow.AddDays(faker.Random.Int(7, 90));
                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    DivisionId = div.Id,
                    HomeTeamId = divTeams[0].Id,
                    AwayTeamId = divTeams[1].Id,
                    LocationId = faker.PickRandom(locations).Id,
                    DateUtc = matchDate,
                    EmergencyDateUtc = matchDate.AddDays(-1),
                    EmergencyPoints = 10,
                };

                // One slot per OfficialRole
                match.Slots = allRoles.Select(role => new RoleSlot
                {
                    Role = role,
                    MatchId = match.Id,
                    // Randomly pre-assign ~half the slots
                    AssignedUserId = faker.Random.Bool(0.4f) ? faker.PickRandom(officials).Id : null,
                }).ToList();

                matches.Add(match);
            }
        }

        db.Matches.AddRange(matches);

        // ── 7. Subscriptions (a few officials subscribed to matches) ──────
        var subscriptions = new List<Subscription>();
        foreach (var match in matches.Take(4))
        {
            var randomOfficials = faker.PickRandom(officials, 2);
            foreach (var official in randomOfficials)
            {
                if (subscriptions.Any(s => s.UserId == official.Id && s.MatchId == match.Id))
                    continue;

                subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    UserId = official.Id,
                    MatchId = match.Id,
                    Role = faker.PickRandom(allRoles),
                    Status = faker.PickRandom(
                        MatchSubscriptionStatus.Subscribed,
                        MatchSubscriptionStatus.ConfirmedJ15),
                    CreatedAt = DateTime.UtcNow.AddDays(-faker.Random.Int(1, 10)),
                });
            }
        }
        db.Subscriptions.AddRange(subscriptions);

        // ── 8. Penalties (a couple per official) ──────────────────────────
        var penalties = officials.Take(3).Select(u => new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = u.Id,
            MatchId = faker.PickRandom(matches).Id,
            Reason = faker.PickRandom("Absence non justifiée", "Retard", "Comportement"),
            Points = faker.Random.Int(1, 5),
            CreatedAt = DateTime.UtcNow.AddDays(-faker.Random.Int(1, 30)),
        }).ToList();
        db.Penalties.AddRange(penalties);

        // ── 9. Notifications ──────────────────────────────────────────────
        var notifications = officials.SelectMany(u =>
            Enumerable.Range(0, faker.Random.Int(1, 3)).Select(_ => new AppNotification
            {
                Id = Guid.NewGuid(),
                UserId = u.Id,
                MatchId = faker.PickRandom(matches).Id,
                Type = faker.PickRandom<NotificationType>(),
                Title = "Rappel de match",
                Body = faker.Lorem.Sentence(),
                IsRead = faker.Random.Bool(0.3f),
                CreatedAt = DateTime.UtcNow.AddHours(-faker.Random.Int(1, 72)),
            })
        ).ToList();
        db.Notifications.AddRange(notifications);

        await db.SaveChangesAsync();

        Logger.Info(
            "Seed complete — {U} users, {D} divisions, {T} teams, {M} matches.",
            officials.Count + 1, divisions.Length, teams.Count, matches.Count);
    }
}
