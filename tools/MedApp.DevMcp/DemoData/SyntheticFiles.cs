using System.Globalization;
using System.Text;
using SkiaSharp;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Small, clearly synthetic files for the demo's attachments, made here with no new dependency: one-page PDFs written
/// by hand, abstract PNG images drawn with SkiaSharp (which the app already uses for thumbnails), and plain text. Each
/// says it's synthetic demo data. Nothing in them is a real person, place or record.
/// </summary>
public static class SyntheticFiles
{
    public const string Banner = "SYNTHETIC DEMO DOCUMENT - not a real patient record";

    /// <summary>
    /// A minimal valid one-page PDF (1.4) with a title and lines of text in Helvetica: a catalog, a page tree, a page,
    /// its content stream and a font, with a correct cross-reference table. No scripts, actions or embedded files.
    /// </summary>
    public static byte[] Pdf(string title, IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.Append("BT /F1 16 Tf 56 780 Td (").Append(Escape(title)).Append(") Tj ET\n");
        content.Append("BT /F1 9 Tf 56 760 Td (").Append(Escape(Banner)).Append(") Tj ET\n");
        var y = 730;
        foreach (var line in lines)
        {
            content.Append("BT /F1 11 Tf 56 ").Append(y.ToString(CultureInfo.InvariantCulture)).Append(" Td (").Append(Escape(line)).Append(") Tj ET\n");
            y -= 18;
        }

        var stream = content.ToString();
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());

        static string Escape(string text) =>
            new string(text.Select(c => c < 128 ? c : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    /// <summary>An ECG-like trace on a grid: abstract, labelled synthetic, with a heart rate drawn from the seed.</summary>
    public static byte[] TracePng(int heartRate, Random random)
    {
        const int width = 640, height = 240;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(255, 248, 248));

        using var grid = new SKPaint { Color = new SKColor(240, 190, 190), StrokeWidth = 1, IsAntialias = false };
        for (var x = 0; x < width; x += 16)
            canvas.DrawLine(x, 0, x, height, grid);
        for (var y = 0; y < height; y += 16)
            canvas.DrawLine(0, y, width, y, grid);

        using var trace = new SKPaint { Color = new SKColor(30, 30, 60), StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var path = new SKPath();
        var beat = Math.Max(60, 60 * 160 / Math.Max(40, heartRate));
        var baseline = height * 0.6f;
        path.MoveTo(0, baseline);
        for (var start = 10; start < width; start += beat)
        {
            var jitter = (float)(random.NextDouble() * 4 - 2);
            path.LineTo(start, baseline);
            path.LineTo(start + 8, baseline - 10 + jitter);
            path.LineTo(start + 16, baseline);
            path.LineTo(start + 24, baseline + 12);
            path.LineTo(start + 30, baseline - 90 + jitter * 3);
            path.LineTo(start + 36, baseline + 25);
            path.LineTo(start + 44, baseline);
            path.LineTo(start + 70, baseline - 18 + jitter);
            path.LineTo(start + 90, baseline);
        }

        path.LineTo(width, baseline);
        canvas.DrawPath(path, trace);
        Label(canvas, $"Synthetic trace (demo) - rate {heartRate}", 12, 20);
        return Encode(surface);
    }

    /// <summary>A simple diagram of an exercise progression: circles joined by arrows, labelled synthetic.</summary>
    public static byte[] DiagramPng(IReadOnlyList<string> steps)
    {
        const int width = 640, height = 220;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(246, 250, 255));

        using var fill = new SKPaint { Color = new SKColor(70, 120, 200), IsAntialias = true };
        using var line = new SKPaint { Color = new SKColor(90, 90, 90), StrokeWidth = 3, IsAntialias = true };
        var gap = width / (steps.Count + 1);
        for (var i = 0; i < steps.Count; i++)
        {
            var x = gap * (i + 1);
            if (i > 0)
                canvas.DrawLine(x - gap + 30, 110, x - 30, 110, line);
            canvas.DrawCircle(x, 110, 26, fill);
            Label(canvas, steps[i], x - 40, 165);
        }

        Label(canvas, "Exercise progression (synthetic demo)", 12, 24);
        return Encode(surface);
    }

    /// <summary>A plain text note, UTF-8.</summary>
    public static byte[] Text(string title, IEnumerable<string> lines) =>
        Encoding.UTF8.GetBytes($"{title}\n{Banner}\n\n{string.Join("\n", lines)}\n");

    private static void Label(SKCanvas canvas, string text, float x, float y)
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        using var paint = new SKPaint { Color = new SKColor(40, 40, 40), IsAntialias = true };
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static byte[] Encode(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
