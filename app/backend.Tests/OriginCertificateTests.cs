using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;
using Oregon.Backend.Infrastructure;

namespace Oregon.Backend.Tests;

/// <summary>Only requests carrying our Cloudflare origin-pull certificate get through when a pin is configured.</summary>
public sealed class OriginCertificateTests : IDisposable
{
    private readonly X509Certificate2 _ours = NewCertificate("CN=oregon origin pull");
    private readonly X509Certificate2 _theirs = NewCertificate("CN=Cloudflare shared origin pull");

    private string Pin => _ours.GetCertHashString(HashAlgorithmName.SHA256);

    public void Dispose()
    {
        _ours.Dispose();
        _theirs.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OurCertificate_PassesInEitherHeaderEncoding(bool urlEncoded)
    {
        (int status, bool reachedApp) = await SendAsync(Pin, Xfcc(_ours, urlEncoded));

        Assert.True(reachedApp);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task AnotherCertificate_MissingHeader_OrSeveralHops_AreForbidden()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, (await SendAsync(Pin, Xfcc(_theirs, urlEncoded: true))).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await SendAsync(Pin, xfcc: null)).Status);

        // A client-supplied value followed by the real hop must not smuggle our certificate in.
        string smuggled = $"{Xfcc(_ours, urlEncoded: true)},{Xfcc(_theirs, urlEncoded: true)}";
        Assert.Equal(StatusCodes.Status403Forbidden, (await SendAsync(Pin, smuggled)).Status);
    }

    [Fact]
    public async Task HashOnlyHeader_IsHonored_AndPinNotationDoesNotMatter()
    {
        string colonUpper = string.Join(':', Enumerable.Range(0, 32).Select(i => Pin.Substring(i * 2, 2)));

        (int status, _) = await SendAsync(colonUpper, $"Hash={Pin.ToLowerInvariant()}");

        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task HealthProbes_AndUnpinnedDeployments_AreNotAffected()
    {
        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(Pin, xfcc: null, path: "/healthz/ready")).Status);
        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(pin: null, xfcc: null)).Status);
    }

    private static async Task<(int Status, bool ReachedApp)> SendAsync(string? pin, string? xfcc, string path = "/api/games")
    {
        bool reached = false;
        OriginCertificateMiddleware middleware = new(
            _ =>
            {
                reached = true;
                return Task.CompletedTask;
            },
            Options.Create(new ProxySettings { OriginClientCertificateSha256 = pin }),
            NullLogger<OriginCertificateMiddleware>.Instance);
        DefaultHttpContext context = new();
        context.Request.Path = path;
        if (xfcc is not null)
        {
            context.Request.Headers[OriginCertificateMiddleware.HeaderName] = xfcc;
        }

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reached);
    }

    private static string Xfcc(X509Certificate2 certificate, bool urlEncoded)
    {
        string pem = certificate.ExportCertificatePem();
        string cert = urlEncoded ? Uri.EscapeDataString(pem) : pem;
        return $"Hash={certificate.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant()};Cert=\"{cert}\";Subject=\"{certificate.Subject}\"";
    }

    private static X509Certificate2 NewCertificate(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
