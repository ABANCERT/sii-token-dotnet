# ABANCERT-SII · TokenSII

Autenticación con el **Servicio de Impuestos Internos (SII)** de Chile en tres pasos:
**semilla → firma → token**. Proyecto de consola en **C# / .NET 10**, autónomo y sin
dependencias externas más allá de la firma XML.

<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="C#" src="https://img.shields.io/badge/C%23-latest-239120">
  <img alt="Licencia MIT" src="https://img.shields.io/badge/Licencia-MIT-blue">
  <img alt="ABANCERT" src="https://img.shields.io/badge/por-ABANCERT-e30613">
</p>

> ### 🔐 ¿Necesitas un certificado digital para facturar en Chile?
> **ABANCERT** emite certificados de **Firma Electrónica Simple** compatibles con el SII,
> con solicitud 100 % en línea, validez inmediata y soporte experto.
>
> 👉 **Solicítalo aquí:** <https://www.abancert.cl/solicitud/certificado-digital-simple>
> · Sitio: <https://www.abancert.cl>

---

## ¿Qué hace?

`TokenSII` obtiene un **token de sesión** del SII, que es el paso previo para consumir sus
servicios (envío de DTE, consultas de estado, etc.):

1. **Semilla** — solicita una semilla al webservice `CrSeed` (`getSeed`).
2. **Firma** — firma la semilla con tu certificado digital (firma XML *enveloped* RSA‑SHA1).
3. **Token** — envía la semilla firmada a `GetTokenFromSeed` (`getToken`) y obtiene el token.

Los webservices del SII se consumen de forma **nativa con `HttpClient` + SOAP**, sin
generadores de proxies ni librerías de terceros.

## Estructura del proyecto

| Archivo | Responsabilidad |
|---|---|
| `TokenSII/Program.cs` | Controles de ejecución: argumentos de línea de comandos y menú interactivo. |
| `TokenSII/Abancert.cs` | Marca y enlaces de ABANCERT. |
| `TokenSII/CertificadoSii.cs` | Carga del certificado, encapsulada: archivo PFX o almacén de Windows. |
| `TokenSII/FirmadorSemilla.cs` | Firma XML de la semilla (RSA‑SHA1). |
| `TokenSII/SiiAutenticador.cs` | Cliente SOAP nativo (getSeed / getToken) y orquestación. |

## Requisitos

- **.NET 10 SDK**.
- Certificado digital con **clave privada** (archivo `.pfx`/`.p12` o instalado en Windows).
  Consigue uno compatible con el SII en **ABANCERT**:
  <https://www.abancert.cl/solicitud/certificado-digital-simple>
- Conexión a Internet hacia `maullin.sii.cl` (certificación) / `palena.sii.cl` (producción).

## Abrir en Visual Studio Code

Proyecto preparado para **Visual Studio Code**:

1. Instala el [SDK de .NET 10](https://dotnet.microsoft.com/download) y la extensión
   **C# Dev Kit** (VS Code la sugiere al abrir la carpeta).
2. Abre la carpeta o el *workspace* `ABANCERT-SII.code-workspace`.
3. **Compilar:** `Ctrl+Shift+B` (tarea `build`).
4. **Ejecutar / depurar:** `F5` y elige una configuración:
   - *TokenSII (interactivo)* — pregunta la fuente del certificado y el ambiente.
   - *TokenSII (PFX · certificación)* — ajusta la ruta y clave del `.pfx` en `.vscode/launch.json`.
   - *TokenSII (almacén Windows · producción)*.

La configuración compartida vive en `.vscode/` (`tasks.json`, `launch.json`, `extensions.json`,
`settings.json`).

## Compilar y ejecutar (línea de comandos)

```bash
# Compilar
dotnet build

# Ejecutar (certificado desde archivo PFX, ambiente de certificación por defecto)
dotnet run --project TokenSII -- --pfx "C:\ruta\mi.pfx" --clave miClave

# Certificado desde el almacén de Windows (selección interactiva), en producción
dotnet run --project TokenSII -- --almacen --prod

# Modo interactivo (pregunta fuente del certificado y ambiente)
dotnet run --project TokenSII
```

### Opciones

| Opción | Descripción |
|---|---|
| `--pfx <ruta>` | Certificado desde archivo `.pfx`/`.p12`. |
| `--clave <clave>` | Clave del `.pfx` (alias `--pass`). Si se omite, se pide oculta por consola. |
| `--almacen` | Certificado desde el almacén de Windows (selección interactiva). |
| `--huella <thumbprint>` | Certificado del almacén por huella digital. |
| `--maquina` | Buscar en `LocalMachine\My` en lugar de `CurrentUser\My`. |
| `--cert` / `--prod` | Ambiente: certificación (por defecto) / producción. |
| `-h`, `--help` | Ayuda. |

Códigos de salida: `0` OK · `1` error del SII / flujo · `2` error de certificado o excepción.

## Notas técnicas

- **Firma en .NET 10.** `SignedXml` firma con la clave que entrega
  `X509Certificate2.GetRSAPrivateKey()`, por lo que funciona igual con claves de archivo PFX o
  del almacén de Windows (CNG o CSP). Los algoritmos SHA‑1 se fijan de forma explícita para
  coincidir con lo que valida el SII.
- **Carga de certificado desacoplada de la firma.** `CertificadoSii` entrega el certificado y
  `FirmadorSemilla` firma sin conocer su origen.
- **TLS.** .NET 10 negocia TLS 1.2/1.3 con el SII automáticamente.

---

## Acerca de ABANCERT

**ABANCERT** provee **certificados digitales de Firma Electrónica Simple** compatibles con el
SII, pensados para la **facturación electrónica** y la firma de documentos tributarios (DTE)
en Chile. Emisión en línea, validez inmediata y soporte experto.

- 🌐 Sitio web: <https://www.abancert.cl>
- 🧾 Solicitar certificado digital simple: <https://www.abancert.cl/solicitud/certificado-digital-simple>

## Licencia

Distribuido bajo licencia **MIT**. Consulta el archivo [`LICENSE`](LICENSE).

© ABANCERT — <https://www.abancert.cl>
