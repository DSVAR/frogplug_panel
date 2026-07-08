using Net.Codecrete.QrCodeGenerator;

namespace FrogPlug.Core;

/// <summary>
/// QR-код в виде встроенного SVG (для ссылки подписки).
/// </summary>
public static class QrSvg
{
    public static string Generate(string value)
    {
        var qr = QrCode.EncodeText(value, QrCode.Ecc.Medium);
        var svg = qr.ToSvgString(border: 2);

        // Для встраивания в HTML убираем XML-пролог и DOCTYPE.
        return svg[svg.IndexOf("<svg", StringComparison.Ordinal)..];
    }

    public static int Size(string value)
    {
        return QrCode.EncodeText(value, QrCode.Ecc.Medium).Size;
    }
}
