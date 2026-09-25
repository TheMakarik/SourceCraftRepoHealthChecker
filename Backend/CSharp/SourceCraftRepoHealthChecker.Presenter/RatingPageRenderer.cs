using System.Net;
using System.Text;
using SourceCraftRepoHealthChecker.Application.Rating.Models;

namespace SourceCraftRepoHealthChecker.Presenter;

public static class RatingPageRenderer
{
    public static string Render(RepositoryLeaderboardPage page, string? language, string sort)
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
        builder.Append("<title>Рейтинг репозиториев SourceCraft</title>");
        builder.Append("<style>body{font-family:sans-serif;margin:2rem}table{border-collapse:collapse;width:100%}");
        builder.Append("th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left}th{background:#f4f4f4}</style></head><body>");
        builder.Append("<h1>Рейтинг здоровья репозиториев SourceCraft</h1>");
        builder.Append("<form method=\"get\" action=\"/rating\">");
        builder.Append($"<input type=\"text\" name=\"language\" placeholder=\"Язык\" value=\"{WebUtility.HtmlEncode(language)}\"> ");
        builder.Append("<select name=\"sort\">");
        builder.Append($"<option value=\"score\"{(sort == "score" ? " selected" : string.Empty)}>По Score</option>");
        builder.Append($"<option value=\"likes\"{(sort == "likes" ? " selected" : string.Empty)}>По лайкам</option>");
        builder.Append($"<option value=\"activity\"{(sort == "activity" ? " selected" : string.Empty)}>По активности</option>");
        builder.Append("</select> <button type=\"submit\">Показать</button></form>");
        builder.Append("<table><thead><tr><th>Место</th><th>Проект</th><th>Score</th><th>Лайки</th><th>Язык</th><th>Последняя активность</th></tr></thead><tbody>");

        foreach (var item in page.Items)
        {
            builder.Append("<tr>");
            builder.Append($"<td>{item.Place}</td>");
            builder.Append($"<td><a href=\"/repositories/{WebUtility.UrlEncode(item.SourceCraftId)}\">{WebUtility.HtmlEncode(item.FullName)}</a></td>");
            builder.Append($"<td>{item.Score?.ToString() ?? "Нет данных"}</td>");
            builder.Append($"<td>{item.LikesCount}</td>");
            builder.Append($"<td>{WebUtility.HtmlEncode(item.Language)}</td>");
            builder.Append($"<td>{item.LastActivityAt:yyyy-MM-dd}</td>");
            builder.Append("</tr>");
        }

        builder.Append("</tbody></table>");
        builder.Append($"<p>Всего: {page.TotalCount}. Страница {page.Page}.</p>");
        builder.Append("</body></html>");

        return builder.ToString();
    }
}
