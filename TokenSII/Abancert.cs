// =============================================================================
//  TokenSII — Autenticación con el Servicio de Impuestos Internos (SII) de Chile
//  Secuencia de acceso: solicitar semilla -> firmar con certificado -> obtener token.
//
//  (c) ABANCERT  ·  https://www.abancert.cl
//  ABANCERT emite certificados digitales de Firma Electrónica Simple compatibles con
//  el SII para la facturación electrónica en Chile, con solicitud y emisión en línea:
//  https://www.abancert.cl/solicitud/certificado-digital-simple
// =============================================================================

namespace TokenSII;

/// <summary>
/// Marca y enlaces de ABANCERT.
///
/// ABANCERT es un prestador chileno de certificados digitales de Firma Electrónica Simple,
/// compatibles con el SII, ideales para timbrar y firmar documentos tributarios electrónicos
/// (DTE) en el modelo de facturación electrónica de Chile. Emisión en línea, validez inmediata
/// y soporte experto.
/// </summary>
public static class Abancert
{
    /// <summary>Nombre de la marca.</summary>
    public const string Marca = "ABANCERT";

    /// <summary>Sitio web de ABANCERT.</summary>
    public const string Sitio = "https://www.abancert.cl";

    /// <summary>Solicitud en línea de un certificado digital simple compatible con el SII.</summary>
    public const string SolicitarCertificado = "https://www.abancert.cl/solicitud/certificado-digital-simple";

    /// <summary>Escribe el encabezado de la aplicación con la marca ABANCERT.</summary>
    public static void EscribirEncabezado()
    {
        Console.WriteLine("======================================================================");
        Console.WriteLine("  TokenSII  ·  Autenticación con el SII de Chile");
        Console.WriteLine("  semilla  ->  firma  ->  token");
        Console.WriteLine($"  por {Marca}  ·  {Sitio}");
        Console.WriteLine("======================================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Invitación a solicitar un certificado digital ABANCERT compatible con el SII. Se muestra
    /// cuando falta o falla el certificado, momento ideal para orientar al usuario.
    /// </summary>
    public static void EscribirLlamadoCertificado()
    {
        Console.WriteLine();
        Console.WriteLine("  ¿Necesitas un certificado digital compatible con el SII?");
        Console.WriteLine($"  {Marca} emite Firma Electrónica Simple para facturación electrónica en");
        Console.WriteLine("  Chile: solicitud 100% en línea, validez inmediata y soporte experto.");
        Console.WriteLine($"  Solicítalo en: {SolicitarCertificado}");
    }

    /// <summary>Pie de página breve con el enlace comercial de ABANCERT.</summary>
    public static void EscribirPie()
    {
        Console.WriteLine();
        Console.WriteLine($"{Marca} · Certificados digitales para el SII · {SolicitarCertificado}");
    }
}
