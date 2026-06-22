using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for a user, keyed by the stable sign-in subject (`sub`).
    /// Populated lazily (upserted on project create) from the caller's token claims.
    /// </summary>
    [Table("users")]
    public class UserEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(256)]
        [IndexedField("ix_users_sub")]
        public string Sub { get; set; } = "";

        [MaxLengthField(256)]
        public string? DisplayName { get; set; }

        [MaxLengthField(256)]
        public string? Email { get; set; }

        public DateTime? UserCreatedAt { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as UserEntity ?? new UserEntity();
            base.CopyTo(target);
            target.Sub = Sub;
            target.DisplayName = DisplayName;
            target.Email = Email;
            target.UserCreatedAt = UserCreatedAt;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
