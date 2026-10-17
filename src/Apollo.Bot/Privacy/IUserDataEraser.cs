using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Privacy;

/// <summary>
/// Deletes everything recorded about one member of the guild and removes their tracking role (scope §5.4, D15). The
/// confirm button of <c>/apollo privacy delete</c> calls it; a platform without buttons (Fluxer, M6) can call it from a
/// text confirm instead.
/// </summary>
public interface IUserDataEraser
{
    /// <summary>
    /// Deletes the member's rows, clears references to them in other users' rows and removes their tracking role.
    /// Never throws for a database or platform failure: the outcome says what happened.
    /// </summary>
    Task<UserDataErasureOutcome> EraseAsync(PlatformRef guild, PlatformRef member, CancellationToken cancellationToken);
}
