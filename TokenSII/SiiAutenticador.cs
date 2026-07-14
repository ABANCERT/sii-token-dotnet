// =============================================================================
//  TokenSII — Autenticación con el Servicio de Impuestos Internos (SII) de Chile
//  Secuencia de acceso: solicitar semilla -> firmar con certificado -> obtener token.
//
//  (c) ABANCERT  ·  https://www.abancert.cl
//  ABANCERT emite certificados digitales de Firma Electrónica Simple compatibles con
//  el SII para la facturación electrónica en Chile, con solicitud y emisión en línea:
//  https://www.abancert.cl/solicitud/certificado-digital-simple
// =============================================================================

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace TokenSII;

/// <summary>Ambiente del SII contra el que se opera.</summary>
public enum SiiAmbiente
{
    /// <summary>Servidor de certificación / pruebas (maullin.sii.cl).</summary>
    Certificacion,
    /// <summary>Servidor de producción (palena.sii.cl).</summary>
    Produccion
}

/// <summary>Resultado de la autenticación con el SII.</summary>
public sealed record ResultadoAutenticacion(bool Exito, string? Semilla, string? Token, string? Error);

/// <summary>
/// Cliente de los webservices de autenticación del SII (CrSeed y GetTokenFromSeed),
/// implementado de forma nativa con <see cref="HttpClient"/> y SOAP (estilo RPC/encoded),
/// sin dependencias externas. Expone la secuencia completa: semilla -> firma -> token.
/// </summary>
public sealed class SiiAutenticador
{
    private const string NsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string NsDefault = "http://DefaultNamespace";
    private const string NsEnc = "http://schemas.xmlsoap.org/soap/encoding/";
    private const string NsXsi = "http://www.w3.org/2001/XMLSchema-instance";
    private const string NsXsd = "http://www.w3.org/2001/XMLSchema";

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(30)
    })
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly SiiAmbiente _ambiente;

    public SiiAutenticador(SiiAmbiente ambiente) => _ambiente = ambiente;

    private string UrlSemilla => _ambiente == SiiAmbiente.Certificacion
        ? "https://maullin.sii.cl/DTEWS/CrSeed.jws"
        : "https://palena.sii.cl/DTEWS/CrSeed.jws";

    private string UrlToken => _ambiente == SiiAmbiente.Certificacion
        ? "https://maullin.sii.cl/DTEWS/GetTokenFromSeed.jws"
        : "https://palena.sii.cl/DTEWS/GetTokenFromSeed.jws";

    /// <summary>Ejecuta la secuencia completa de autenticación: semilla -> firma -> token.</summary>
    public async Task<ResultadoAutenticacion> AutenticarAsync(X509Certificate2 certificado, CancellationToken ct = default)
    {
        var (okSemilla, semilla, errorSemilla) = await ObtenerSemillaAsync(ct);
        if (!okSemilla)
            return new ResultadoAutenticacion(false, null, null, $"Al obtener la semilla: {errorSemilla}");

        string semillaFirmada;
        try
        {
            semillaFirmada = FirmadorSemilla.Firmar(semilla!, certificado);
        }
        catch (Exception ex)
        {
            return new ResultadoAutenticacion(false, semilla, null, $"Al firmar la semilla: {ex.Message}");
        }

        var (okToken, token, errorToken) = await ObtenerTokenAsync(semillaFirmada, ct);
        if (!okToken)
            return new ResultadoAutenticacion(false, semilla, null, $"Al obtener el token: {errorToken}");

        return new ResultadoAutenticacion(true, semilla, token, null);
    }

    /// <summary>Solicita una semilla al SII (webservice CrSeed / getSeed).</summary>
    public async Task<(bool ok, string? valor, string? error)> ObtenerSemillaAsync(CancellationToken ct = default)
    {
        try
        {
            var operacion = new XElement(XName.Get("getSeed", NsDefault),
                new XAttribute(XName.Get("encodingStyle", NsSoap), NsEnc));

            var respuesta = await InvocarSoapAsync(UrlSemilla, operacion, ct);
            return LeerRespuestaSii(respuesta, "getSeedReturn", "SEMILLA");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>Solicita el token al SII enviando la semilla firmada (GetTokenFromSeed / getToken).</summary>
    public async Task<(bool ok, string? valor, string? error)> ObtenerTokenAsync(string semillaFirmada, CancellationToken ct = default)
    {
        try
        {
            XNamespace xsi = NsXsi;
            var operacion = new XElement(XName.Get("getToken", NsDefault),
                new XAttribute(XName.Get("encodingStyle", NsSoap), NsEnc),
                new XElement("pszXml",
                    new XAttribute(xsi + "type", "xsd:string"),
                    semillaFirmada));

            var respuesta = await InvocarSoapAsync(UrlToken, operacion, ct);
            return LeerRespuestaSii(respuesta, "getTokenReturn", "TOKEN");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>Envuelve la operación en un sobre SOAP, hace POST y devuelve el XML de respuesta.</summary>
    private static async Task<XDocument> InvocarSoapAsync(string url, XElement operacion, CancellationToken ct)
    {
        XNamespace soap = NsSoap;
        var sobre = new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", NsSoap),
                new XAttribute(XNamespace.Xmlns + "xsi", NsXsi),
                new XAttribute(XNamespace.Xmlns + "xsd", NsXsd),
                new XElement(soap + "Body", operacion)));

        using var contenido = new StringContent(sobre.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        contenido.Headers.Add("SOAPAction", "\"\"");

        using var http = await Http.PostAsync(url, contenido, ct);
        var texto = await http.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(texto))
            throw new HttpRequestException($"El SII respondió HTTP {(int)http.StatusCode} sin cuerpo desde {url}");

        return XDocument.Parse(texto);
    }

    /// <summary>
    /// De la respuesta SOAP extrae el string de retorno (getSeedReturn / getTokenReturn), que a
    /// su vez es el XML RESPUESTA del SII, valida ESTADO=00 y devuelve el valor pedido (SEMILLA/TOKEN).
    /// </summary>
    private static (bool ok, string? valor, string? error) LeerRespuestaSii(XDocument sobreSoap, string nodoRetorno, string nodoValor)
    {
        var fault = sobreSoap.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
            return (false, null, $"SOAP Fault: {faultString ?? fault.Value}");
        }

        var retorno = sobreSoap.Descendants().FirstOrDefault(e => e.Name.LocalName == nodoRetorno)?.Value;
        if (string.IsNullOrWhiteSpace(retorno))
            return (false, null, "La respuesta del SII no contiene el nodo de retorno esperado.");

        XDocument respuestaSii;
        try
        {
            respuestaSii = XDocument.Parse(retorno);
        }
        catch (Exception ex)
        {
            return (false, null, $"La respuesta del SII no es XML válido: {ex.Message}");
        }

        string? Valor(string local) =>
            respuestaSii.Descendants().FirstOrDefault(e => e.Name.LocalName == local)?.Value;

        var estado = Valor("ESTADO");
        if (estado != "00")
            return (false, null, $"El SII devolvió ESTADO='{estado}': {Valor("GLOSA")}");

        var valor = Valor(nodoValor);
        if (string.IsNullOrWhiteSpace(valor))
            return (false, null, $"El SII no devolvió el valor '{nodoValor}'.");

        return (true, valor, null);
    }
}
