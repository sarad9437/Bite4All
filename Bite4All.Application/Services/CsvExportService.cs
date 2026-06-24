using System.Globalization;
using System.Text;
using Bite4All.Application.DTOs.Reports;

namespace Bite4All.Application.Services;

public class CsvExportService : ICsvExportService
{
    public byte[] ExportAdminImpact(AdminImpactReportDto report)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "Metric", "Value");
        AppendLine(sb, "TotalRescuedKg", report.TotalRescuedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "EstimatedCo2SavedKg", report.EstimatedCo2SavedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "MealsProvided", report.MealsProvided.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "UnusedOffersCount", report.UnusedOffersCount.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "UnusedOffersKg", report.UnusedOffersKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "UniqueRecipientsHelped", report.UniqueRecipientsHelped.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "ActiveHospitalityPartners", report.ActiveHospitalityPartners.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "ActiveOrganizations", report.ActiveOrganizations.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        AppendLine(sb, "City", "RescuedKg");
        foreach (var c in report.ByCity)
        {
            AppendLine(sb, c.CityName, c.RescuedKg.ToString(CultureInfo.InvariantCulture));
        }
        sb.AppendLine();

        AppendLine(sb, "TopHospitalityPartnerId", "Name", "QuantityKg", "CompletedPickups");
        foreach (var p in report.TopHospitalityPartners)
        {
            AppendLine(sb, p.ActorId.ToString(CultureInfo.InvariantCulture), p.Name, p.QuantityKg.ToString(CultureInfo.InvariantCulture), p.CompletedPickups.ToString(CultureInfo.InvariantCulture));
        }
        sb.AppendLine();

        AppendLine(sb, "TopOrganizationId", "Name", "QuantityKg", "CompletedPickups");
        foreach (var o in report.TopOrganizations)
        {
            AppendLine(sb, o.ActorId.ToString(CultureInfo.InvariantCulture), o.Name, o.QuantityKg.ToString(CultureInfo.InvariantCulture), o.CompletedPickups.ToString(CultureInfo.InvariantCulture));
        }
        sb.AppendLine();

        AppendLine(sb, "Year", "Month", "RescuedKg", "CompletedPickups");
        foreach (var m in report.MonthlyTrends)
        {
            AppendLine(sb, m.Year.ToString(CultureInfo.InvariantCulture), m.Month.ToString(CultureInfo.InvariantCulture), m.RescuedKg.ToString(CultureInfo.InvariantCulture), m.CompletedPickups.ToString(CultureInfo.InvariantCulture));
        }

        return ToUtf8WithBom(sb);
    }

    public byte[] ExportPartnerImpact(PartnerImpactReportDto report)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "Metric", "Value");
        AppendLine(sb, "HospitalityPartnerId", report.HospitalityPartnerId.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "TotalDonatedKg", report.TotalDonatedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "SuccessfulDonations", report.SuccessfulDonations.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "UnusedOffersCount", report.UnusedOffersCount.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "UnusedOffersKg", report.UnusedOffersKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "ReputationScore", report.ReputationScore.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "EstimatedCo2SavedKg", report.EstimatedCo2SavedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "MealsProvided", report.MealsProvided.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "NextBadgeHint", report.NextBadgeHint);
        return ToUtf8WithBom(sb);
    }

    public byte[] ExportOrganizationImpact(OrganizationImpactReportDto report)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "Metric", "Value");
        AppendLine(sb, "CharityOrganizationId", report.CharityOrganizationId.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "TotalReceivedKg", report.TotalReceivedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "CompletedPickups", report.CompletedPickups.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "MealsDistributedEstimate", report.MealsDistributedEstimate?.ToString(CultureInfo.InvariantCulture) ?? "");
        AppendLine(sb, "RecordedMealsDistributed", report.RecordedMealsDistributed?.ToString(CultureInfo.InvariantCulture) ?? "");
        AppendLine(sb, "UniqueRecipientsHelped", report.UniqueRecipientsHelped.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "ReputationScore", report.ReputationScore.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "NextBadgeHint", report.NextBadgeHint);
        sb.AppendLine();

        AppendLine(sb, "Category", "QuantityKg");
        foreach (var c in report.ByCategory)
        {
            AppendLine(sb, c.Category, c.QuantityKg.ToString(CultureInfo.InvariantCulture));
        }
        sb.AppendLine();

        AppendLine(sb, "DriverId", "DriverName", "CompletedPickups", "TransportedKg");
        foreach (var d in report.DriverActivity)
        {
            AppendLine(sb, d.DriverId?.ToString(CultureInfo.InvariantCulture) ?? "", d.DriverName, d.CompletedPickups.ToString(CultureInfo.InvariantCulture), d.TransportedKg.ToString(CultureInfo.InvariantCulture));
        }

        return ToUtf8WithBom(sb);
    }

    public byte[] ExportDriverImpact(DriverImpactReportDto report)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "Metric", "Value");
        AppendLine(sb, "DriverId", report.DriverId.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "DriverName", report.DriverName);
        AppendLine(sb, "CompletedPickups", report.CompletedPickups.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "TransportedKg", report.TransportedKg.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "CancellationCount", report.CancellationCount.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "ReputationScore", report.ReputationScore.ToString(CultureInfo.InvariantCulture));
        AppendLine(sb, "NextBadgeHint", report.NextBadgeHint);
        sb.AppendLine();

        AppendLine(sb, "RecordedAtUtc", "Score", "Source");
        foreach (var h in report.ReputationHistory)
        {
            AppendLine(sb, h.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture), h.Score.ToString(CultureInfo.InvariantCulture), h.Source);
        }

        return ToUtf8WithBom(sb);
    }

    private static void AppendLine(StringBuilder sb, params string[] fields)
    {
        sb.AppendLine(string.Join(",", fields.Select(Escape)));
    }

    private static string Escape(string field)
    {
        field ??= string.Empty;
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private static byte[] ToUtf8WithBom(StringBuilder sb)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }
}
