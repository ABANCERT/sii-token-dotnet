// =============================================================================
//  TokenSII — Autenticación con el Servicio de Impuestos Internos (SII) de Chile
//  Secuencia de acceso: solicitar semilla -> firmar con certificado -> obtener token.
//
//  (c) ABANCERT  ·  https://www.abancert.cl
//  ABANCERT emite certificados digitales de Firma Electrónica Simple compatibles con
//  el SII para la facturación electrónica en Chile, con solicitud y emisión en línea:
//  https://www.abancert.cl/solicitud/certificado-digital-simple
// =============================================================================

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace TokenSII;

/// <summary>
/// Firma la semilla del SII con una firma XML "enveloped" RSA-SHA1, tal como espera el
/// webservice GetTokenFromSeed.
///
/// La firma se calcula con la clave privada RSA que entrega
/// <see cref="X509Certificate2.GetRSAPrivateKey"/>, por lo que funciona de forma transparente
/// tanto con certificados cargados desde un archivo PFX como desde el almacén de Windows
/// (claves CNG o CSP).
/// </summary>
public static class FirmadorSemilla
{
    public static string Firmar(string semilla, X509Certificate2 certificado)
    {
        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.LoadXml($"<?xml version=\"1.0\"?><getToken><item><Semilla>{semilla}</Semilla></item></getToken>");

        using var rsa = certificado.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no expone una clave privada RSA utilizable.");

        var firma = new SignedXml(documento) { SigningKey = rsa };

        // Algoritmos SHA-1 fijados de forma explícita para coincidir con lo que valida el SII.
        firma.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
        firma.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        var referencia = new Reference
        {
            Uri = "#xpointer(/)",
            DigestMethod = SignedXml.XmlDsigSHA1Url
        };
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        firma.AddReference(referencia);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new RSAKeyValue(rsa));
        keyInfo.AddClause(new KeyInfoX509Data(certificado));
        firma.KeyInfo = keyInfo;

        firma.ComputeSignature();

        var nodoFirma = firma.GetXml();
        documento.DocumentElement!.AppendChild(documento.ImportNode(nodoFirma, deep: true));

        return documento.OuterXml;
    }
}
