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
using System.Text;
using TokenSII;

Console.OutputEncoding = Encoding.UTF8;
Abancert.EscribirEncabezado();

var opciones = Opciones.Parsear(args);
if (opciones.MostrarAyuda)
{
    Opciones.ImprimirAyuda();
    return 0;
}

// 1) Certificado de firma (archivo PFX o almacén de Windows).
X509Certificate2 certificado;
try
{
    certificado = ObtenerCertificado(opciones);
}
catch (Exception ex)
{
    Console.WriteLine($"\nNo se pudo cargar el certificado: {ex.Message}");
    Abancert.EscribirLlamadoCertificado();
    return 2;
}

// 2) Secuencia de autenticación con el SII.
int codigoSalida;
using (certificado)
{
    MostrarCertificado(certificado, opciones.Ambiente);

    Console.WriteLine("Solicitando semilla, firmando y pidiendo token al SII...\n");

    var autenticador = new SiiAutenticador(opciones.Ambiente);
    var resultado = await autenticador.AutenticarAsync(certificado);

    MostrarResultado(resultado);
    codigoSalida = resultado.Exito ? 0 : 1;
}

Abancert.EscribirPie();
return codigoSalida;

// ----------------------------- Controles de ejecución -----------------------------

static X509Certificate2 ObtenerCertificado(Opciones o) => o.Fuente switch
{
    FuenteCertificado.Pfx => CargarPfx(o),
    FuenteCertificado.Almacen => CargarAlmacen(o),
    _ => SeleccionInteractiva(o)
};

static X509Certificate2 CargarPfx(Opciones o)
{
    var ruta = o.RutaPfx;
    if (string.IsNullOrWhiteSpace(ruta))
    {
        Console.Write("Ruta del archivo .pfx/.p12: ");
        ruta = (Console.ReadLine() ?? string.Empty).Trim().Trim('"');
    }

    var clave = o.Clave;
    if (clave is null)
    {
        Console.Write("Clave del certificado    : ");
        clave = LeerClaveOculta();
    }

    return CertificadoSii.DesdeArchivoPfx(ruta, clave);
}

static X509Certificate2 CargarAlmacen(Opciones o)
{
    if (!string.IsNullOrWhiteSpace(o.Huella))
        return CertificadoSii.DesdeAlmacenPorHuella(o.Huella!, o.Ubicacion);

    return SeleccionarDelAlmacen(o.Ubicacion);
}

static X509Certificate2 SeleccionInteractiva(Opciones o)
{
    Console.WriteLine("¿Desde dónde cargar el certificado?");
    Console.WriteLine("  1) Archivo .pfx / .p12");
    Console.WriteLine("  2) Almacén de certificados de Windows");
    var opcion = LeerOpcion("Opción [1]: ", ["1", "2"], "1");

    var certificado = opcion == "2" ? SeleccionarDelAlmacen(o.Ubicacion) : CargarPfx(o);

    if (!o.AmbienteEspecificado)
        o.Ambiente = PreguntarAmbiente();

    return certificado;
}

static X509Certificate2 SeleccionarDelAlmacen(StoreLocation ubicacion)
{
    var lista = CertificadoSii.ListarConClavePrivada(ubicacion);
    if (lista.Count == 0)
        throw new InvalidOperationException($"No hay certificados con clave privada en {ubicacion}\\My.");

    Console.WriteLine($"\nCertificados con clave privada en {ubicacion}\\My:");
    for (var i = 0; i < lista.Count; i++)
    {
        var c = lista[i];
        var nombre = c.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        Console.WriteLine($"  {i + 1}) {nombre}  |  vence {c.NotAfter:dd-MM-yyyy}  |  {c.Thumbprint}");
    }

    var indice = LeerEntero("Seleccione N° [1]: ", 1, lista.Count, 1);
    return lista[indice - 1];
}

static SiiAmbiente PreguntarAmbiente()
{
    Console.WriteLine("\n¿Ambiente del SII?");
    Console.WriteLine("  1) Certificación (maullin.sii.cl) - pruebas");
    Console.WriteLine("  2) Producción (palena.sii.cl)");
    var opcion = LeerOpcion("Opción [1]: ", ["1", "2"], "1");
    return opcion == "2" ? SiiAmbiente.Produccion : SiiAmbiente.Certificacion;
}

// ----------------------------- Salida -----------------------------

static void MostrarCertificado(X509Certificate2 certificado, SiiAmbiente ambiente)
{
    var descripcionAmbiente = ambiente == SiiAmbiente.Certificacion
        ? "CERTIFICACIÓN (maullin.sii.cl)"
        : "PRODUCCIÓN (palena.sii.cl)";

    Console.WriteLine();
    Console.WriteLine($"Certificado : {certificado.Subject}");
    Console.WriteLine($"Emitido por : {certificado.Issuer}");
    Console.WriteLine($"Vigencia    : {certificado.NotBefore:dd-MM-yyyy} a {certificado.NotAfter:dd-MM-yyyy}");
    Console.WriteLine($"Clave priv. : {certificado.HasPrivateKey}");
    Console.WriteLine($"Ambiente    : {descripcionAmbiente}");
    Console.WriteLine();
}

static void MostrarResultado(ResultadoAutenticacion r)
{
    Console.WriteLine("----------------------------------------------------------------------");
    if (r.Exito)
    {
        Console.WriteLine(" RESULTADO: OK");
        Console.WriteLine("----------------------------------------------------------------------");
        Console.WriteLine($" Semilla : {r.Semilla}");
        Console.WriteLine($" TOKEN   : {r.Token}");
    }
    else
    {
        Console.WriteLine(" RESULTADO: ERROR");
        Console.WriteLine("----------------------------------------------------------------------");
        Console.WriteLine($" {r.Error}");
    }
    Console.WriteLine("----------------------------------------------------------------------");
}

// ----------------------------- Entrada de consola -----------------------------

static string LeerClaveOculta()
{
    if (Console.IsInputRedirected)
        return Console.ReadLine() ?? string.Empty;

    var sb = new StringBuilder();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
            {
                sb.Length--;
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(tecla.KeyChar))
        {
            sb.Append(tecla.KeyChar);
            Console.Write('*');
        }
    }
    return sb.ToString();
}

static string LeerOpcion(string prompt, string[] validas, string porDefecto)
{
    while (true)
    {
        Console.Write(prompt);
        var entrada = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(entrada))
            return porDefecto;
        entrada = entrada.Trim();
        if (validas.Contains(entrada))
            return entrada;
        Console.WriteLine("  Opción no válida.");
    }
}

static int LeerEntero(string prompt, int min, int max, int porDefecto)
{
    while (true)
    {
        Console.Write(prompt);
        var entrada = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(entrada))
            return porDefecto;
        if (int.TryParse(entrada.Trim(), out var valor) && valor >= min && valor <= max)
            return valor;
        Console.WriteLine($"  Ingrese un número entre {min} y {max}.");
    }
}

// ----------------------------- Opciones de línea de comandos -----------------------------

enum FuenteCertificado { Interactivo, Pfx, Almacen }

sealed class Opciones
{
    public FuenteCertificado Fuente { get; private set; } = FuenteCertificado.Interactivo;
    public string? RutaPfx { get; private set; }
    public string? Clave { get; private set; }
    public string? Huella { get; private set; }
    public StoreLocation Ubicacion { get; private set; } = StoreLocation.CurrentUser;
    public SiiAmbiente Ambiente { get; set; } = SiiAmbiente.Certificacion;
    public bool AmbienteEspecificado { get; private set; }
    public bool MostrarAyuda { get; private set; }

    public static Opciones Parsear(string[] args)
    {
        var o = new Opciones();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--pfx":
                    o.Fuente = FuenteCertificado.Pfx;
                    o.RutaPfx = Siguiente(args, ref i);
                    break;
                case "--clave":
                case "--pass":
                    o.Clave = Siguiente(args, ref i);
                    break;
                case "--almacen":
                case "--store":
                    o.Fuente = FuenteCertificado.Almacen;
                    break;
                case "--huella":
                case "--thumbprint":
                    o.Fuente = FuenteCertificado.Almacen;
                    o.Huella = Siguiente(args, ref i);
                    break;
                case "--maquina":
                    o.Ubicacion = StoreLocation.LocalMachine;
                    break;
                case "--prod":
                    o.Ambiente = SiiAmbiente.Produccion;
                    o.AmbienteEspecificado = true;
                    break;
                case "--cert":
                case "--certificacion":
                    o.Ambiente = SiiAmbiente.Certificacion;
                    o.AmbienteEspecificado = true;
                    break;
                case "-h":
                case "--help":
                case "/?":
                    o.MostrarAyuda = true;
                    break;
            }
        }
        return o;
    }

    private static string? Siguiente(string[] args, ref int i) => (i + 1 < args.Length) ? args[++i] : null;

    public static void ImprimirAyuda()
    {
        Console.WriteLine($"""
        TokenSII — Autenticación con el SII de Chile (por {Abancert.Marca}).

        Uso: TokenSII [opciones]

        Fuente del certificado:
          --pfx <ruta>         Certificado desde archivo .pfx/.p12
          --clave <clave>      Clave del .pfx (alias: --pass). Si se omite, se pide por consola.
          --almacen            Certificado desde el almacén de Windows (selección interactiva)
          --huella <thumb>     Certificado del almacén por huella (thumbprint)
          --maquina            Buscar en LocalMachine\My en vez de CurrentUser\My

        Ambiente:
          --cert               Certificación / maullin (por defecto)
          --prod               Producción / palena

          -h, --help           Muestra esta ayuda

        Sin argumentos: modo interactivo (elige fuente y ambiente).

        Ejemplos:
          TokenSII --pfx "C:\certs\mi.pfx" --clave miClave
          TokenSII --almacen --prod
          TokenSII --huella 6F1A...C9 --maquina

        ¿Necesitas un certificado digital compatible con el SII?
        {Abancert.SolicitarCertificado}
        """);
    }
}
