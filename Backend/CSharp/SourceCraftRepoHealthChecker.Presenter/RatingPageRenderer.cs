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
        builder.Append("th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left}th{background:#f4f4f4}");
        builder.Append(".pager{margin-top:1rem;display:flex;gap:.5rem;align-items:center}</style></head><body>");
        builder.Append("<h1>Рейтинг здоровья репозиториев SourceCraft</h1>");
        builder.Append("<form method=\"get\" action=\"/rating\">");
        builder.Append($"<input type=\"text\" name=\"language\" placeholder=\"Язык\" value=\"{WebUtility.HtmlEncode(language)}\"> ");
        builder.Append("<select name=\"sort\">");
        builder.Append($"<option value=\"score\"{(sort == "score" ? " selected" : string.Empty)}>По Score</option>");
        builder.Append($"<option value=\"likes\"{(sort == "likes" ? " selected" : string.Empty)}>По лайкам</option>");
        builder.Append($"<option value=\"activity\"{(sort == "activity" ? " selected" : string.Empty)}>По активности</option>");
        builder.Append("</select> <button type=\"submit\">Показать</button></form>");
        builder.Append("<table><thead><tr><th>Место</th><th>Проект</th><th>Анализ</th><th>Score</th><th>Лайки</th><th>Язык</th><th>Последняя активность</th></tr></thead><tbody>");

        foreach (var item in page.Items)
        {
            builder.Append("<tr>");
            builder.Append($"<td>{item.Place}</td>");
            builder.Append($"<td><a href=\"{WebUtility.HtmlEncode(item.Url)}\" rel=\"noopener noreferrer\">{WebUtility.HtmlEncode(item.FullName)}</a></td>");
            builder.Append($"<td><a href=\"/repositories/{WebUtility.UrlEncode(item.SourceCraftId)}\">Открыть</a></td>");
            builder.Append($"<td>{item.Score?.ToString() ?? "Нет данных"}</td>");
            builder.Append($"<td>{item.LikesCount}</td>");
            builder.Append($"<td>{WebUtility.HtmlEncode(item.Language)}</td>");
            builder.Append($"<td>{item.LastActivityAt:yyyy-MM-dd}</td>");
            builder.Append("</tr>");
        }

        builder.Append("</tbody></table>");
        AppendPager(builder, page, language, sort);
        builder.Append("</body></html>");

        return builder.ToString();
    }

    private static void AppendPager(StringBuilder builder, RepositoryLeaderboardPage page, string? language, string sort)
    {
        var totalPages = page.PageSize <= 0 ? 1 : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);
        if (totalPages < 1)
            totalPages = 1;

        builder.Append("<div class=\"pager\">");
        builder.Append(page.Page > 1
            ? $"<a href=\"{PageLink(page.Page - 1, language, sort)}\">← Назад</a>"
            : "<span>← Назад</span>");
        builder.Append($"<span>Страница {page.Page} из {totalPages} · всего {page.TotalCount}</span>");
        builder.Append(page.Page < totalPages
            ? $"<a href=\"{PageLink(page.Page + 1, language, sort)}\">Вперёд →</a>"
            : "<span>Вперёд →</span>");
        builder.Append("</div>");
    }

    private static string PageLink(int page, string? language, string sort)
    {
        var builder = new StringBuilder($"/rating?sort={WebUtility.UrlEncode(sort)}&page={page}");
        if (!string.IsNullOrWhiteSpace(language))
            builder.Append($"&language={WebUtility.UrlEncode(language)}");

        return builder.ToString();
    }
}
