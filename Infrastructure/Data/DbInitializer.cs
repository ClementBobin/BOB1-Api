using Bogus;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

using Domain.Entities;

using Tools;

/// <summary>
/// Seeds the database with realistic fake data using the Bogus library.
/// Mirrors the JS factories.js structure (makeUser, makeCategory, makeProduct…).
/// Only runs when the DB is empty — safe to call on every startup in Development.
/// </summary>
public static class DbInitializer
{

}