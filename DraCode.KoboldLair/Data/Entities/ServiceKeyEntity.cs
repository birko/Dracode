using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Who created a service-account key (TASK-162). A key is an OAuth client (its secret, scopes and enabled flag live
    /// in the OAuth client store), but that store cannot be queried, so the owner and name are kept here to list a
    /// person's keys.
    /// </summary>
    [Table("service_keys")]
    public class ServiceKeyEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(255)]
        public string ClientId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(255)]
        public string OwnerId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(255)]
        public string Name { get; set; } = "";

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as ServiceKeyEntity ?? new ServiceKeyEntity();
            base.CopyTo(target);
            target.ClientId = ClientId;
            target.OwnerId = OwnerId;
            target.Name = Name;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
