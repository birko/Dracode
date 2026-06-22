namespace DraCode.KoboldLair.Models.Users
{
    /// <summary>
    /// A DraCode user, keyed by the stable sign-in subject (`sub`) claim carried by every
    /// validated token (interactive humans, service accounts, and the local-dev loopback
    /// principal). Project ownership references this <see cref="Sub"/> directly — see
    /// FEATURE-019 decision D9.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Stable subject identifier from the token's `sub` / NameIdentifier claim.
        /// This is the owner key stored on projects (<c>Project.OwnerId</c>).
        /// </summary>
        public string Sub { get; set; } = "";

        /// <summary>
        /// Best-effort display name from the token's name claim, if present.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Best-effort email from the token's email claim, if present.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// When this user record was first seen / created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
