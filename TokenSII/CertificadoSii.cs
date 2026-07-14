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

namespace TokenSII;

/// <summary>
/// Obtención del certificado digital de firma, encapsulada de modo que su origen (archivo
/// PFX o almacén de certificados de Windows) no afecte al proceso de firma: en ambos casos
/// se entrega un <see cref="X509Certificate2"/> con clave privada RSA lista para firmar.
///
/// Pensado para operar con certificados de Firma Electrónica Simple emitidos por ABANCERT
/// (https://www.abancert.cl/solicitud/certificado-digital-simple), válidos ante el SII.
/// </summary>
public static class CertificadoSii
{
    /// <summary>Carga un certificado desde un archivo .pfx / .p12.</summary>
    public static X509Certificate2 DesdeArchivoPfx(string ruta, string? clave)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo del certificado: {ruta}");

        // Exportable | PersistKeySet deja la clave privada plenamente utilizable para firmar,
        // sin importar el proveedor criptográfico con que venga empaquetado el archivo.
        const X509KeyStorageFlags flags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;
        var certificado = X509CertificateLoader.LoadPkcs12FromFile(ruta, clave, flags);

        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException("El certificado no contiene clave privada; no se puede firmar.");

        return certificado;
    }

    /// <summary>Carga un certificado del almacén de Windows por su huella digital (thumbprint).</summary>
    public static X509Certificate2 DesdeAlmacenPorHuella(string huella, StoreLocation ubicacion = StoreLocation.CurrentUser)
    {
        huella = huella.Replace(" ", string.Empty).Replace(":", string.Empty).ToUpperInvariant();

        using var store = new X509Store(StoreName.My, ubicacion);
        store.Open(OpenFlags.ReadOnly);

        var encontrados = store.Certificates.Find(X509FindType.FindByThumbprint, huella, validOnly: false);
        if (encontrados.Count == 0)
            throw new InvalidOperationException($"No se encontró un certificado con huella {huella} en {ubicacion}\\My.");

        var certificado = encontrados[0];
        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException("El certificado encontrado no tiene clave privada asociada.");

        return certificado;
    }

    /// <summary>Lista los certificados con clave privada del almacén personal (para selección interactiva).</summary>
    public static IReadOnlyList<X509Certificate2> ListarConClavePrivada(StoreLocation ubicacion = StoreLocation.CurrentUser)
    {
        using var store = new X509Store(StoreName.My, ubicacion);
        store.Open(OpenFlags.ReadOnly);

        return store.Certificates
            .Where(c => c.HasPrivateKey)
            .OrderBy(c => c.GetNameInfo(X509NameType.SimpleName, forIssuer: false))
            .ToList();
    }
}
