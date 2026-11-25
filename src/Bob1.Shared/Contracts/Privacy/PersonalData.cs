namespace Bob1.Shared.Contracts.Privacy;

/// <summary>
/// Whose data is exported / erased. <paramref name="FamilyId"/> is the family the person belongs to (if any);
/// <paramref name="FamilyEmptied"/> is true when erasing this person leaves that family without any member, so the data that only
/// exists for the family (presence confirmations...) goes too.
/// </summary>
public sealed record PersonalDataSubject(Guid UserId, Guid? FamilyId, bool FamilyEmptied);

/// <summary>
/// Implemented by every module that stores data about a person (GDPR access / portability and erasure).
/// Identity collects every contributor and builds the export file / runs the erasure; a module only knows its own tables.
/// Erasure must be idempotent: it is retried as a whole when a later step fails.
/// </summary>
public interface IPersonalDataContributor
{
    /// <summary>Key of this module's block in the export file, e.g. "participation".</summary>
    string Section { get; }

    /// <summary>The person's data held by this module, ready to be serialised to JSON.</summary>
    Task<object> ExportAsync(PersonalDataSubject subject, CancellationToken ct);

    /// <summary>
    /// Deletes the person's data, or anonymises it where a row must outlive the person (it then points to <see cref="Guid.Empty"/> / null).
    /// Rows other people rely on are kept; nothing here notifies anybody (the deactivation event has already done that).
    /// </summary>
    Task EraseAsync(PersonalDataSubject subject, CancellationToken ct);
}
