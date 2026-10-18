using System.Security.Authentication;
using System.Net.Sockets;
using System.Net.Http;

namespace VideoPiper.Services;

/// <summary>
/// Swedish, user-facing messages for exceptions. The UI must never show raw .NET
/// (or tool) exception text — it is English and full of implementation detail. Each
/// mapped message says what went wrong and (where it can be acted on) what to do
/// next; anything unmapped falls back to a Swedish <paramref name="fallback"/>.
///
/// Notes on what is matchable:
/// - File failures arrive as the typed .NET exceptions (UnauthorizedAccess /
///   FileNotFound / DirectoryNotFound / IOException), not as bare Win32 codes.
/// - Network/TLS failures arrive as HttpRequestException / SocketException /
///   AuthenticationException (possibly nested). Note: on .NET 10 the public
///   Win32Exception constructor squashes every code to E_FAIL, so codes can't be
///   read back — the typed wrappers are the reliable signal.
/// - A few raw tool strings (yt-dlp is Python) get targeted mappings by exact
///   text, because they are the ones actually seen in the field.
/// </summary>
public static class UiErrors
{
    public static string FromException(Exception ex, string fallback)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        if (ex is OperationCanceledException || current is OperationCanceledException)
        {
            return "Avbrutet.";
        }
        if (current is UnauthorizedAccessException)
        {
            return "Appen har inte tillgång till filen eller mappen. Kontrollera behörigheterna och försök igen.";
        }
        if (current is FileNotFoundException || current is DirectoryNotFoundException)
        {
            return "Filen eller mappen kunde inte hittas.";
        }
        if (current is IOException)
        {
            return "Filen kunde inte läsas eller skrivas.";
        }
        if (IsNetworkError(ex))
        {
            return "Nätverksfel: YouTube kunde inte nås. Kontrollera internetanslutningen och försök igen.";
        }

        // Known raw strings from the managed tools (yt-dlp is Python). Matched by
        // exact substring so no implementation detail leaks into the UI.
        var message = ex.Message;
        if (current.Message != ex.Message)
        {
            message += " " + current.Message;
        }
        if (message.Contains("untrusted mount point", StringComparison.OrdinalIgnoreCase))
        {
            return "Sökvägen går genom en Windows-junction som appen inte kan använda. Välj en annan mapp och försök igen.";
        }
        if (message.Contains("No credentials are available in the security package", StringComparison.OrdinalIgnoreCase))
        {
            return "Nätverksfel: YouTube kunde inte nås. Kontrollera internetanslutningen och försök igen.";
        }

        return fallback;
    }

    /// <summary>
    /// Walks the whole exception chain. TLS/SSPI failures surface as an
    /// <see cref="HttpRequestException"/> wrapping a Win32Exception
    /// (e.g. SEC_E_NO_CREDENTIALS), so matching the outer HTTP/socket types is
    /// what actually catches them.
    /// </summary>
    private static bool IsNetworkError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException
                || e is SocketException
                || e is TimeoutException
                || e is AuthenticationException)
            {
                return true;
            }
        }
        return false;
    }
}
