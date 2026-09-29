using System.Globalization;
using System.Net;
using System.Text;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public static class RepositoryBadgeSvgRenderer
{
    private const string Label = "repo health";
    private const int BadgeHeight = 20;
    private const int CharacterWidth = 7;
    private const int HorizontalPadding = 10;

    public static string Render(int? score)
    {
        var value = score is null
            ? "n/a"
            : string.Create(CultureInfo.InvariantCulture, $"{score}/100");
        var labelWidth = Label.Length * CharacterWidth + HorizontalPadding;
        var valueWidth = value.Length * CharacterWidth + HorizontalPadding;
        var totalWidth = labelWidth + valueWidth;
        var labelCenter = labelWidth / 2.0;
        var valueCenter = labelWidth + valueWidth / 2.0;

        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        builder.Append(totalWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" height=\"");
        builder.Append(BadgeHeight.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" role=\"img\" aria-label=\"");
        builder.Append(WebUtility.HtmlEncode($"{Label}: {value}"));
        builder.Append("\">");
        builder.Append("<linearGradient id=\"s\" x2=\"0\" y2=\"100%\"><stop offset=\"0\" stop-color=\"#bbb\" stop-opacity=\".1\"/><stop offset=\"1\" stop-opacity=\".1\"/></linearGradient>");
        builder.Append("<clipPath id=\"r\"><rect width=\"");
        builder.Append(totalWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" height=\"");
        builder.Append(BadgeHeight.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" rx=\"3\" fill=\"#fff\"/></clipPath>");
        builder.Append("<g clip-path=\"url(#r)\">");
        builder.Append("<rect width=\"");
        builder.Append(labelWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" height=\"");
        builder.Append(BadgeHeight.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" fill=\"#555\"/>");
        builder.Append("<rect x=\"");
        builder.Append(labelWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" width=\"");
        builder.Append(valueWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" height=\"");
        builder.Append(BadgeHeight.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" fill=\"");
        builder.Append(Color(score));
        builder.Append("\"/>");
        builder.Append("<rect width=\"");
        builder.Append(totalWidth.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" height=\"");
        builder.Append(BadgeHeight.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" fill=\"url(#s)\"/>");
        builder.Append("</g>");
        builder.Append("<g fill=\"#fff\" text-anchor=\"middle\" font-family=\"Verdana,Geneva,DejaVu Sans,sans-serif\" font-size=\"11\">");
        AppendText(builder, labelCenter, Label);
        AppendText(builder, valueCenter, value);
        builder.Append("</g></svg>");
        return builder.ToString();
    }

    private static void AppendText(StringBuilder builder, double center, string text)
    {
        var horizontalPosition = center.ToString("0.#", CultureInfo.InvariantCulture);
        builder.Append("<text x=\"");
        builder.Append(horizontalPosition);
        builder.Append("\" y=\"15\" fill=\"#010101\" fill-opacity=\".3\">");
        builder.Append(WebUtility.HtmlEncode(text));
        builder.Append("</text><text x=\"");
        builder.Append(horizontalPosition);
        builder.Append("\" y=\"14\">");
        builder.Append(WebUtility.HtmlEncode(text));
        builder.Append("</text>");
    }

    private static string Color(int? score) => score switch
    {
        null => "#9f9f9f",
        >= 90 => "#4c1",
        >= 80 => "#97ca00",
        >= 70 => "#a4a61d",
        >= 60 => "#dfb317",
        >= 50 => "#fe7d37",
        _ => "#e05d44"
    };
}
