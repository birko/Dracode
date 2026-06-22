using DraCode.KoboldLair.Models.Users;

namespace DraCode.KoboldLair.Data.Repositories
{
    /// <summary>
    /// Persistence for users, keyed by the stable sign-in subject (`sub`). Populated lazily —
    /// a row is upserted when a caller first creates a project (FEATURE-019 D3/D9).
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>Returns the user for the given `sub`, or null if none exists.</summary>
        Task<User?> GetBySubAsync(string sub);

        /// <summary>
        /// Inserts the user if absent, or refreshes display name/email if already present.
        /// Keyed on <see cref="User.Sub"/>.
        /// </summary>
        Task UpsertAsync(User user);
    }
}
