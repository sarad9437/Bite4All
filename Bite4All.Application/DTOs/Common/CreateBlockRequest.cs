namespace Bite4All.Application.DTOs.Common;

public class CreateBlockRequest
{
    public int HospitalityPartnerId { get; set; }
    public int CharityOrganizationId { get; set; }
    public bool BlockedByHospitalityPartner { get; set; }
    public bool BlockedByOrganization { get; set; }
    public string Reason { get; set; } = string.Empty;
}
