using Bite4All.Domain.Common;

namespace Bite4All.Domain.Entities;

public class BlockRelation : Entity
{
    public int HospitalityPartnerId { get; set; }
    public HospitalityPartner? HospitalityPartner { get; set; }
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public bool BlockedByHospitalityPartner { get; set; }
    public bool BlockedByOrganization { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
