using System.Text;
using System.Text.Json;
using Optica.Api.Arca;
using QRCoder;

namespace Optica.Api.Facturacion;

/// <summary>
/// Código QR que ARCA exige en los comprobantes electrónicos (RF-30): una URL con los
/// datos del comprobante en JSON codificado en Base64, según la especificación de ARCA
/// (https://www.afip.gob.ar/fe/qr/especificaciones.asp).
/// </summary>
public static class QrArca
{
    public const string UrlBase = "https://www.afip.gob.ar/fe/qr/?p=";

    public static string Url(Factura f, long cuitEmisor)
    {
        var datos = new
        {
            ver = 1,
            fecha = f.Fecha.ToString("yyyy-MM-dd"),
            cuit = cuitEmisor,
            ptoVta = f.PuntoVenta,
            tipoCmp = (int)f.Tipo,
            nroCmp = f.Numero,
            importe = f.ImporteTotal,
            moneda = "PES",
            ctz = 1,
            tipoDocRec = (int)f.TipoDocumentoReceptor,
            nroDocRec = f.NumeroDocumentoReceptor,
            tipoCodAut = "E", // E = CAE
            codAut = long.Parse(f.Cae),
        };
        return UrlBase + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(datos)));
    }

    public static byte[] Png(string url)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }
}
