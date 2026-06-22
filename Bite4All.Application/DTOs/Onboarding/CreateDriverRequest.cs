namespace Bite4All.Application.DTOs.Onboarding;

public class CreateDriverRequest
{
    public int CharityOrganizationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
