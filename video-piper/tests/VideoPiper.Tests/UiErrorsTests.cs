using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPiper.Services;

namespace VideoPiper.Tests;

[TestClass]
public class UiErrorsTests
{
    private const string Network = "Nätverksfel: YouTube kunde inte nås. Kontrollera internetanslutningen och försök igen.";
    private const string AccessDenied = "Appen har inte tillgång till filen eller mappen. Kontrollera behörigheterna och försök igen.";
    private const string NotFound = "Filen eller mappen kunde inte hittas.";

    [TestMethod]
    public void FromException_UnauthorizedAccess_ReturnsSwedishPermissionMessage()
    {
        Assert.AreEqual(AccessDenied, UiErrors.FromException(new UnauthorizedAccessException("Access to the path 'C:\\x' is denied."), "fallback"));
    }

    [TestMethod]
    public void FromException_FileNotFound_ReturnsSwedishNotFoundMessage()
    {
        Assert.AreEqual(NotFound, UiErrors.FromException(new FileNotFoundException("Could not find file."), "fallback"));
    }

    [TestMethod]
    public void FromException_DirectoryNotFound_ReturnsSwedishNotFoundMessage()
    {
        Assert.AreEqual(NotFound, UiErrors.FromException(new DirectoryNotFoundException("No dir"), "fallback"));
    }

    [TestMethod]
    public void FromException_IoException_ReturnsSwedishReadWriteMessage()
    {
        Assert.AreEqual("Filen kunde inte läsas eller skrivas.", UiErrors.FromException(new IOException("io boom"), "fallback"));
    }

    [TestMethod]
    public void FromException_HttpRequest_ReturnsSwedishNetworkMessage()
    {
        Assert.AreEqual(Network, UiErrors.FromException(new HttpRequestException("SSL connection failed"), "fallback"));
    }

    [TestMethod]
    public void FromException_HttpRequestWrappingSslNoCredentials_ReturnsSwedishNetworkMessage()
    {
        // The real-world shape seen in restricted environments: an HttpRequestException
        // wrapping a Win32Exception with an SSPI HRESULT (SEC_E_NO_CREDENTIALS).
        var inner = new Win32Exception(unchecked((int)0x8009030E));
        Assert.AreEqual(Network, UiErrors.FromException(new HttpRequestException("TLS failed", inner), "fallback"));
    }

    [TestMethod]
    public void FromException_SocketError_ReturnsSwedishNetworkMessage()
    {
        Assert.AreEqual(Network, UiErrors.FromException(new SocketException(10061), "fallback"));
    }

    [TestMethod]
    public void FromException_AuthenticationFailure_ReturnsSwedishNetworkMessage()
    {
        Assert.AreEqual(Network, UiErrors.FromException(new AuthenticationException("Authentication failed"), "fallback"));
    }

    [TestMethod]
    public void FromException_NetworkErrorNested_ReturnsSwedishNetworkMessage()
    {
        // The network failure may be buried in an inner exception.
        var outer = new InvalidOperationException("wrap", new SocketException(10053));
        Assert.AreEqual(Network, UiErrors.FromException(outer, "fallback"));
    }

    [TestMethod]
    public void FromException_UntrustedMountPointToolError_ReturnsSwedishJunctionMessage()
    {
        // WinError 448 as surfaced by the Python-based yt-dlp binary.
        var ex = new InvalidOperationException("ERROR: [WinError 448] The path cannot be traversed because it contains an untrusted mount point: 'C:\\nvm4w\\nodejs'");
        Assert.AreEqual(
            "Sökvägen går genom en Windows-junction som appen inte kan använda. Välj en annan mapp och försök igen.",
            UiErrors.FromException(ex, "fallback"));
    }

    [TestMethod]
    public void FromException_SspiRawMessage_ReturnsSwedishNetworkMessage()
    {
        var ex = new Win32Exception(unchecked((int)0x8009030E), "No credentials are available in the security package");
        Assert.AreEqual(Network, UiErrors.FromException(ex, "fallback"));
    }

    [TestMethod]
    public void FromException_Cancellation_ReturnsAvbrutet()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.AreEqual("Avbrutet.", UiErrors.FromException(new OperationCanceledException("cancelled"), "fallback"));
        Assert.AreEqual("Avbrutet.", UiErrors.FromException(new OperationCanceledException(), "fallback"));
    }

    [TestMethod]
    public void FromException_UnknownException_ReturnsFallback()
    {
        Assert.AreEqual("Något gick fel. Försök igen.", UiErrors.FromException(new InvalidOperationException("some internal thing"), "Något gick fel. Försök igen."));
    }

    [TestMethod]
    public void FromException_NeverReturnsRawEnglishMessage()
    {
        // The whole point: no .NET exception text may reach the UI.
        var cases = new Exception[]
        {
            new UnauthorizedAccessException("Access denied"),
            new IOException("The process cannot access the file because it is being used by another process."),
            new HttpRequestException("No credentials are available in the security package"),
            new ArgumentException("Value cannot be null. (Parameter 'path')"),
            new Win32Exception(5),
        };

        foreach (var ex in cases)
        {
            var message = UiErrors.FromException(ex, "Något gick fel. Försök igen.");
            Assert.IsFalse(message.Contains("Access denied"), $"raw message leaked for {ex.GetType().Name}");
            Assert.IsFalse(message.Contains("Value cannot be null"), $"raw message leaked for {ex.GetType().Name}");
        }
    }
}
