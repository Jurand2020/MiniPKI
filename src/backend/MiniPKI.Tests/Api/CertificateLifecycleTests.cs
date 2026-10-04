using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using MiniPKI.Api.Models;
using MiniPKI.Core.Domain;

namespace MiniPKI.Tests.Api;

public class CertificateLifecycleTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CertificateLifecycleTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FullCertificateLifecycle()
    {
        // Login first
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        // Issue certificate
        var issueResponse = await _client.PostAsJsonAsync("/api/certificates",
            new IssueCertificateRequest
            {
                CommonName = "test.example.com",
                SanEntries = new List<string> { "test.example.com", "www.example.com" },
                KeyAlgorithm = "RSA",
                KeySize = 2048,
                ValidityDays = 365
            });
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var issued = await issueResponse.Content.ReadFromJsonAsync<CertificateResponse>(JsonOptions);
        Assert.NotNull(issued);
        Assert.Equal("test.example.com", issued!.CommonName);
        Assert.Equal("RSA", issued.KeyAlgorithm);

        // List certificates
        var listResponse = await _client.GetAsync("/api/certificates");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<CertificateResponse>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list!);

        // Get certificate details
        var getResponse = await _client.GetAsync($"/api/certificates/{issued.SerialNumber}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var details = await getResponse.Content.ReadFromJsonAsync<CertificateResponse>(JsonOptions);
        Assert.NotNull(details);
        Assert.Equal(issued.SerialNumber, details!.SerialNumber);

        // Download certificate
        var downloadResponse = await _client.GetAsync($"/api/certificates/{issued.SerialNumber}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var certPem = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Contains("-----BEGIN CERTIFICATE-----", certPem);

        // Download chain
        var chainResponse = await _client.GetAsync($"/api/certificates/{issued.SerialNumber}/chain");
        Assert.Equal(HttpStatusCode.OK, chainResponse.StatusCode);
        var chainPem = await chainResponse.Content.ReadAsStringAsync();
        Assert.Contains("-----BEGIN CERTIFICATE-----", chainPem);

        // Download key
        var keyResponse = await _client.GetAsync($"/api/certificates/{issued.SerialNumber}/key");
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyPem = await keyResponse.Content.ReadAsStringAsync();
        Assert.Contains("-----BEGIN PRIVATE KEY-----", keyPem);

        // Revoke certificate
        var revokeResponse = await _client.PostAsJsonAsync(
            $"/api/certificates/{issued.SerialNumber}/revoke",
            new RevokeCertificateRequest { Reason = "keyCompromise" });
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Verify certificate is revoked
        var revokedDetails = await _client.GetAsync($"/api/certificates/{issued.SerialNumber}");
        var revokedCert = await revokedDetails.Content.ReadFromJsonAsync<CertificateResponse>(JsonOptions);
        Assert.NotNull(revokedCert);
        Assert.Equal(CertificateStatus.Revoked, revokedCert!.Status);
    }

    [Fact]
    public async Task GetNonExistentCertificate_ReturnsNotFound()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.GetAsync("/api/certificates/NONEXISTENT123");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeNonExistentCertificate_ReturnsNotFound()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var response = await _client.PostAsJsonAsync(
            "/api/certificates/NONEXISTENT123/revoke",
            new RevokeCertificateRequest { Reason = "test" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IssueCertificate_WithEcdsa_CreatesCertificate()
    {
        await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Password = "test123" });

        var issueResponse = await _client.PostAsJsonAsync("/api/certificates",
            new IssueCertificateRequest
            {
                CommonName = "ecdsa.example.com",
                SanEntries = new List<string> { "ecdsa.example.com" },
                KeyAlgorithm = "ECDSA",
                KeySize = 256,
                ValidityDays = 365
            });
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var issued = await issueResponse.Content.ReadFromJsonAsync<CertificateResponse>(JsonOptions);
        Assert.NotNull(issued);
        Assert.Equal("ECDSA", issued!.KeyAlgorithm);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_factory.TempDataPath))
            Directory.Delete(_factory.TempDataPath, true);
    }
}
