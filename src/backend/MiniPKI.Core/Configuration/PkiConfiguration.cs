using System.Text.Json.Serialization;
using MiniPKI.Core.Domain;

namespace MiniPKI.Core.Configuration;

/// <summary>
/// Strongly-typed PKI configuration. Bound from appsettings.json
/// and also persisted as a runtime-editable JSON file.
/// </summary>
public class PkiConfiguration
{
    public string DefaultDomain { get; set; } = "company.local";

    /// <summary>
    /// CRL distribution point URL embedded in issued certificates.
    /// Use the {API_CRL} placeholder to reference the MiniPKI CRL endpoint,
    /// e.g. "http://my.host.local:11111{API_CRL}" resolves to
    /// "http://my.host.local:11111/api/crl/current".
    /// </summary>
    public string CrlUrl { get; set; } = "http://pki.company.local{API_CRL}";

    public int DefaultValidityDays { get; set; } = 825;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KeyAlgorithm Algorithm { get; set; } = KeyAlgorithm.RSA;

    public int RsaKeySize { get; set; } = 4096;
    public string Organization { get; set; } = "Company";
    public string OrganizationalUnit { get; set; } = "IT";
    public string Country { get; set; } = "PL";
    public string State { get; set; } = "Greater Poland";
    public string Locality { get; set; } = "Poznan";
}
