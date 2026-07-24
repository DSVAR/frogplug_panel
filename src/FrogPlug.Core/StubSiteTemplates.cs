using System.Security.Cryptography;
using System.Text;

namespace FrogPlug.Core;

public static class StubSiteTemplates
{
    public const string PersonalBlog = "personal-blog";
    public const string Photography = "photography";
    public const string ItConsulting = "it-consulting";

    public static readonly string[] All = [PersonalBlog, Photography, ItConsulting];

    public static IReadOnlyDictionary<string, string> BuildSite(string template, string title, Guid nodeId, DateOnly? currentDate = null)
    {
        var random = new Random(GetSeed(nodeId));
        var selected = All.Contains(template, StringComparer.Ordinal) ? template : PersonalBlog;
        var siteTitle = string.IsNullOrWhiteSpace(title) ? GetTitle(selected, random) : title.Trim();
        var palette = GetPalette(random);
        var variant = random.Next(2) == 0 ? "atlas" : "folio";
        var today = currentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var posts = BuildPosts(selected, random, today);
        var startYear = posts.Count == 0 ? today.Year : posts.Min(post => post.Date).Year;
        var site = new SiteModel(siteTitle, selected, palette, variant, GetAuthor(selected, random), startYear, today.Year);
        return BuildFiles(site, posts);
    }

    private static Dictionary<string, string> BuildFiles(SiteModel site, List<SitePost> posts)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["index.html"] = BuildHome(site, posts),
            ["about.html"] = BuildAbout(site),
            ["contacts.html"] = BuildContacts(site),
            ["archive.html"] = BuildArchive(site, posts),
            ["404.html"] = BuildNotFound(site),
            ["assets/site.css"] = BuildCss(site),
            ["favicon.svg"] = BuildFavicon(site.Palette),
            ["assets/img/avatar.svg"] = BuildAvatar(site.Palette),
            ["robots.txt"] = "User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n",
            ["sitemap.xml"] = BuildSitemap(posts),
            ["rss.xml"] = BuildRss(site, posts)
        };
        foreach (var post in posts)
        {
            files[$"notes/{post.Slug}.html"] = BuildPost(site, post, posts);
            files[$"assets/img/{post.Slug}.svg"] = BuildIllustration(site.Palette, post.Number);
        }
        foreach (var tag in posts.SelectMany(post => post.Tags).Distinct(StringComparer.Ordinal).Order())
        {
            files[$"tags/{ToSlug(tag)}.html"] = BuildTag(site, tag, posts.Where(post => post.Tags.Contains(tag)).ToList());
        }
        return files;
    }

    private static int GetSeed(Guid nodeId)
    {
        var hash = SHA256.HashData(nodeId.ToByteArray());
        return BitConverter.ToInt32(hash, 0) & int.MaxValue;
    }

    private static string GetTitle(string template, Random random)
    {
        var titles = template switch
        {
            Photography => new[] { "Свет в пути", "Тихие кадры", "Городские заметки" },
            ItConsulting => new[] { "Контур практики", "Рабочие системы", "Тетрадь инженера" },
            _ => new[] { "Обычный день", "Записки без спешки", "Небольшие истории" }
        };
        return titles[random.Next(titles.Length)];
    }

    private static string GetAuthor(string template, Random random)
    {
        var names = template switch
        {
            Photography => new[] { "Лера Савина", "Мира Ветрова", "Ника Орлова" },
            ItConsulting => new[] { "Артём Лесин", "Илья Северин", "Роман Ладин" },
            _ => new[] { "Нина Белова", "Марина Соколова", "Саша Воронова" }
        };
        return names[random.Next(names.Length)];
    }

    private static List<SitePost> BuildPosts(string template, Random random, DateOnly today)
    {
        var subjects = GetSubjects(template);
        var tags = GetTags(template);
        // Сайт ведётся с фиксированной для установки даты (2023–2025) и дальше пополняется до сегодняшнего дня.
        var baseSeed = random.Next();
        var cursor = new DateOnly(2023, 6, 1).AddDays(random.Next(0, 730));
        var posts = new List<SitePost>();
        for (var index = 0; cursor <= today && index < 500; index++)
        {
            // У каждой записи свой генератор: новые записи не меняют уже опубликованные.
            var local = new Random(unchecked(baseSeed + index * 7919));
            var subject = subjects[(index + local.Next(subjects.Length)) % subjects.Length];
            var number = index + 1;
            var postTags = PickTags(tags, local, number);
            var heading = GetHeading(template, subject, number);
            var slug = $"{ToSlug(subject)}-{cursor:yyyy-MM-dd}-{number}";
            posts.Add(new SitePost(heading, slug, cursor, postTags, BuildParagraphs(template, subject, local), number));
            cursor = cursor.AddDays(local.Next(10, 21));
        }
        return posts.OrderByDescending(post => post.Date).ToList();
    }

    private static string[] GetSubjects(string template)
    {
        return template switch
        {
            Photography => ["утренний свет", "тихий двор", "берег", "окно в поезде", "туман", "переулок", "дождь"],
            ItConsulting => ["очередь задач", "карта сервиса", "дежурство", "контракт", "интерфейс", "релиз", "метрика"],
            _ => ["утренняя прогулка", "маленькая мастерская", "книга", "разговор", "длинная неделя", "поездка", "кухня" ]
        };
    }

    private static string[] GetTags(string template)
    {
        return template switch
        {
            Photography => ["плёнка", "город", "путешествия", "свет", "наблюдения"],
            ItConsulting => ["разработка", "процессы", "инфраструктура", "команда", "практика"],
            _ => ["будни", "маршруты", "чтение", "дом", "наблюдения"]
        };
    }

    private static string[] PickTags(string[] tags, Random random, int number)
    {
        var count = 1 + random.Next(3);
        return Enumerable.Range(0, count).Select(offset => tags[(number + offset + random.Next(tags.Length)) % tags.Length])
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string GetHeading(string template, string subject, int number)
    {
        var endings = template == ItConsulting
            ? new[] { "без героизма", "в обычный вторник", "перед следующим шагом", "в деталях" }
            : new[] { "без спешки", "после паузы", "в привычном ритме", "внимательно" };
        return char.ToUpperInvariant(subject[0]) + subject[1..] + " " + endings[number % endings.Length];
    }

    private static string[] BuildParagraphs(string template, string subject, Random random)
    {
        var bank = template switch
        {
            Photography => new[]
            {
                "Камера помогает мне не торопиться: сначала смотрю, как меняется {0}, и только потом выбираю кадр.",
                "В этом месте не было ничего постановочного. Несколько минут тишины дали больше, чем длинный маршрут.",
                "Я оставила в кадре воздух и неровный край света; именно он напоминает о времени лучше точных деталей.",
                "Позже снимок оказался не про место, а про спокойное внимание к тому, что обычно проходит мимо.",
                "Полезно вернуться сюда в другой сезон: один и тот же угол честно показывает перемену настроения."
            },
            ItConsulting => new[]
            {
                "Работу с темой «{0}» я начал не с инструмента, а с вопроса: какое решение станет понятнее команде через месяц?",
                "Небольшая запись наблюдений сняла лишние догадки и позволила договориться о следующем шаге без лишних встреч.",
                "Мы выбрали короткий цикл: проверить предположение, собрать сигнал, описать вывод и только затем менять процесс.",
                "Хорошая техническая деталь не живёт отдельно от людей: у неё есть владелец, причина и понятный момент пересмотра.",
                "Этот подход оставляет место для ошибок, но делает их видимыми достаточно рано, чтобы исправить спокойно."
            },
            _ => new[]
            {
                "В истории про {0} мне важнее всего оказался не результат, а несколько тихих решений, сделанных по дороге.",
                "Я записываю такие моменты, чтобы не превращать неделю в список выполненных дел и замечать её настоящий ритм.",
                "Ничего выдающегося не произошло, но именно в этом было облегчение: день не требовал срочно стать другим.",
                "Позже разговор вернулся в памяти одной точной фразой и помог посмотреть на привычный порядок чуть мягче.",
                "Пожалуй, это хороший способ сохранить внимание: оставить в заметке не вывод, а честный след от наблюдения."
            }
        };
        var count = 3 + random.Next(7);
        return Enumerable.Range(0, count).Select(index => string.Format(bank[(index + random.Next(bank.Length)) % bank.Length], subject))
            .ToArray();
    }

    private static SitePalette GetPalette(Random random)
    {
        var palettes = new[]
        {
            new SitePalette("#f5f1e8", "#1e2931", "#b45236", "#d8d0c2", "#fffdf8"),
            new SitePalette("#102126", "#e7f1ed", "#86c2a3", "#29434a", "#183036"),
            new SitePalette("#f4f5fb", "#20243c", "#635bcb", "#d9dcf0", "#ffffff"),
            new SitePalette("#fff7ef", "#34251f", "#d27245", "#edd7c5", "#fffdf9")
        };
        return palettes[random.Next(palettes.Length)];
    }

    private static string BuildHome(SiteModel site, List<SitePost> posts)
    {
        var cards = string.Join(string.Empty, posts.Take(9).Select(post => PostCard(post, true)));
        var tags = TagLinks(posts.SelectMany(post => post.Tags).Distinct(StringComparer.Ordinal));
        var content = $"<section class=\"hero\"><p class=\"eyebrow\">личный журнал · {Html(site.Author)}</p><h1>{Html(site.Title)}</h1>" +
            $"<p>{Intro(site.Template)}</p><a class=\"button\" href=\"/archive\">Все записи</a></section>" +
            $"<section class=\"content-grid\"><div><div class=\"section-head\"><h2>Свежие записи</h2><span>{posts.Count} заметок</span></div>" +
            $"<div class=\"card-grid\">{cards}</div></div><aside class=\"sidebar\"><h2>О проекте</h2><p>{AboutShort(site.Template)}</p>" +
            $"<a href=\"/about\">История автора</a><h2>Темы</h2><div class=\"tags\">{tags}</div></aside></section>";
        return Document(site, site.Title, Intro(site.Template), "/assets/img/avatar.svg", content);
    }

    private static string BuildPost(SiteModel site, SitePost post, List<SitePost> posts)
    {
        var paragraphs = string.Join(string.Empty, post.Paragraphs.Select(Html).Select(text => $"<p>{text}</p>"));
        var detail = site.Template == ItConsulting
            ? $"<h2>Небольшой рабочий приём</h2><pre><code>{Pick(CodeSnippets, post.Number)}</code></pre>"
            : $"<blockquote>{Pick(Quotes, post.Number)}</blockquote>";
        var related = RelatedPosts(post, posts);
        var nav = PostNavigation(post, posts);
        var checklist = string.Join(string.Empty, Enumerable.Range(0, 3)
            .Select(offset => $"<li>{Pick(ChecklistItems, post.Number * 3 + offset)}</li>"));
        var content = $"<article class=\"post\"><p class=\"eyebrow\">{Date(post.Date)} · {ReadingTime(post)} мин чтения</p>" +
            $"<h1>{Html(post.Title)}</h1><div class=\"tags\">{TagLinks(post.Tags)}</div>" +
            $"<figure><img src=\"/assets/img/{post.Slug}.svg\" alt=\"Иллюстрация к записи\">" +
            $"<figcaption>{Pick(Captions, post.Number)}</figcaption></figure>{paragraphs}{detail}" +
            $"<h2>{Pick(ChecklistTitles, post.Number)}</h2><ul>{checklist}</ul></article>" +
            $"<nav class=\"post-nav\">{nav}</nav><section><h2>Похожие записи</h2><div class=\"related\">{related}</div></section>";
        return Document(site, post.Title, post.Paragraphs[0], $"/assets/img/{post.Slug}.svg", content);
    }

    private static string BuildArchive(SiteModel site, List<SitePost> posts)
    {
        var groups = posts.GroupBy(post => new { post.Date.Year, post.Date.Month }).Select(group =>
            $"<section class=\"archive-group\"><h2>{Month(group.Key.Month)} {group.Key.Year}</h2>" +
            string.Join(string.Empty, group.Select(post =>
                $"<a href=\"/notes/{post.Slug}\"><time>{post.Date:dd.MM}</time>{Html(post.Title)}</a>")) + "</section>");
        var head = "<section class=\"page-head\"><p class=\"eyebrow\">архив</p><h1>Все записи</h1></section>";
        return Document(site, "Архив", "Все заметки по месяцам", "/assets/img/avatar.svg", head + string.Join(string.Empty, groups));
    }

    private static string BuildTag(SiteModel site, string tag, List<SitePost> posts)
    {
        var content = $"<section class=\"page-head\"><p class=\"eyebrow\">тема</p><h1>#{Html(tag)}</h1>" +
            $"<p>Записей в подборке: {posts.Count}.</p></section>" +
            $"<div class=\"card-grid\">{string.Join(string.Empty, posts.Select(post => PostCard(post, true)))}</div>";
        return Document(site, $"Тема: {tag}", $"Записи по теме {tag}", "/assets/img/avatar.svg", content);
    }

    private static string BuildAbout(SiteModel site)
    {
        var content = "<article class=\"post about\"><img class=\"avatar\" src=\"/assets/img/avatar.svg\" alt=\"Иллюстрация автора\">" +
            $"<p class=\"eyebrow\">об авторе</p><h1>Привет, я {Html(site.Author)}</h1><p>{AboutShort(site.Template)}</p>" +
            "<p>Этот сайт вырос из привычки бережно собирать рабочие и личные наблюдения.</p>" +
            "<p>Здесь нет срочных советов и громких обещаний. Есть опыт, который можно перечитать, уточнить и продолжить.</p></article>";
        return Document(site, "Об авторе", "История автора и проекта", "/assets/img/avatar.svg", content);
    }

    private static string BuildContacts(SiteModel site)
    {
        var content = "<article class=\"post\"><p class=\"eyebrow\">связь</p><h1>Остаёмся на связи</h1>" +
            "<p>Сайт ведётся для себя и друзей, поэтому формы обратной связи здесь нет.</p>" +
            "<p>Если мы знакомы, вы знаете, как меня найти. Новые записи удобно читать через RSS.</p></article>";
        return Document(site, "Контакты", "Как связаться с автором", "/assets/img/avatar.svg", content);
    }

    private static string BuildNotFound(SiteModel site)
    {
        var content = "<section class=\"not-found\"><p class=\"eyebrow\">ошибка 404</p><h1>Страница ушла гулять</h1>" +
            "<p>Возможно, адрес изменился или заметка ещё не появилась.</p><a class=\"button\" href=\"/\">На главную</a></section>";
        return Document(site, "Страница не найдена", "Страница не найдена", "/assets/img/avatar.svg", content);
    }

    private static string Document(SiteModel site, string pageTitle, string description, string image, string content)
    {
        var fullTitle = pageTitle == site.Title ? site.Title : $"{pageTitle} — {site.Title}";
        var years = site.StartYear == site.EndYear ? $"{site.EndYear}" : $"{site.StartYear}–{site.EndYear}";
        var footer = $"© {years} {Html(site.Author)} · <a href=\"/contacts\">Контакты</a> · <a href=\"/archive\">Архив</a>";
        return $$"""
<!doctype html>
<html lang="ru">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="theme-color" content="{{site.Palette.Accent}}">
    <meta name="description" content="{{Html(description)}}">
    <meta property="og:title" content="{{Html(fullTitle)}}">
    <meta property="og:description" content="{{Html(description)}}">
    <meta property="og:image" content="{{image}}">
    <title>{{Html(fullTitle)}}</title>
    <link rel="icon" href="/favicon.svg" type="image/svg+xml">
    <link rel="stylesheet" href="/assets/site.css">
</head>
<body class="{{site.Variant}}">
    <div class="shell">
        <header class="site-header"><a class="brand" href="/"><span class="mark">●</span>{{Html(site.Title)}}</a>
            <nav><a href="/archive">Архив</a><a href="/about">О сайте</a><a href="/contacts">Контакты</a></nav></header>
        <main>{{content}}</main>
        <footer>{{footer}}</footer>
    </div>
</body>
</html>
""";
    }

    private static string PostCard(SitePost post, bool image)
    {
        var cover = image
            ? $"<a class=\"cover\" href=\"/notes/{post.Slug}\"><img src=\"/assets/img/{post.Slug}.svg\" alt=\"Иллюстрация к записи\"></a>"
            : string.Empty;
        return $"<article class=\"card\">{cover}<p class=\"eyebrow\">{Date(post.Date)} · {ReadingTime(post)} мин</p>" +
            $"<h3><a href=\"/notes/{post.Slug}\">{Html(post.Title)}</a></h3><p>{Html(post.Paragraphs[0])}</p>{TagLinks(post.Tags)}</article>";
    }

    // Соседние записи по дате: слева более старая, справа более новая.
    private static string PostNavigation(SitePost post, List<SitePost> posts)
    {
        var ordered = posts.OrderBy(item => item.Date).ThenBy(item => item.Number).ToList();
        var index = ordered.IndexOf(post);
        var older = index > 0 ? ordered[index - 1] : null;
        var newer = index < ordered.Count - 1 ? ordered[index + 1] : null;
        return NavLink(older, "Предыдущая") + NavLink(newer, "Следующая");
    }

    // Похожие — только более старые записи с общими темами, чтобы страница не менялась при новых постах.
    private static string RelatedPosts(SitePost post, List<SitePost> posts)
    {
        var related = posts
            .Where(item => item.Date < post.Date && item.Tags.Intersect(post.Tags, StringComparer.Ordinal).Any())
            .OrderByDescending(item => item.Date)
            .Take(3)
            .Select(item => PostCard(item, false))
            .ToList();
        return related.Count == 0 ? "<p>Пока это единственная заметка на эту тему.</p>" : string.Join(string.Empty, related);
    }

    private static string NavLink(SitePost? post, string label)
    {
        return post is null ? "<span></span>" : $"<a href=\"/notes/{post.Slug}\"><small>{label}</small>{Html(post.Title)}</a>";
    }

    private static string TagLinks(IEnumerable<string> tags)
    {
        return string.Join(string.Empty, tags.Select(tag => $"<a href=\"/tags/{ToSlug(tag)}\">#{Html(tag)}</a>"));
    }

    private static string Intro(string template)
    {
        return template switch
        {
            Photography => "Фотографические прогулки, найденный свет и истории, которые остаются за границей кадра.",
            ItConsulting => "Заметки о спокойной инженерной работе, ясных процессах и командах, которым можно доверять.",
            _ => "Личный журнал о маршрутах, книгах, маленьких делах и внимании к обычному дню."
        };
    }

    private static string AboutShort(string template)
    {
        return template == ItConsulting ? "Практика без лишнего шума: наблюдения о системах и людях." :
            template == Photography ? "Визуальный дневник о медленном взгляде на знакомые места." : "Тихие истории о том, что происходит рядом.";
    }

    private static int ReadingTime(SitePost post) => Math.Max(2, post.Paragraphs.Length * 2);

    private static string Date(DateOnly date) => $"{date.Day} {MonthGenitive[date.Month - 1]} {date:yyyy}";

    private static readonly string[] MonthGenitive =
        ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];

    private static string Month(int month)
    {
        return new[] { "январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь" }[month - 1];
    }

    private static string ToSlug(string value)
    {
        return string.Concat(value.ToLowerInvariant().Select(character =>
            Translit.TryGetValue(character, out var part) ? part : char.IsLetterOrDigit(character) ? character.ToString() : "-")).Trim('-');
    }

    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ж'] = "zh", ['з'] = "z",
        ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p",
        ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ы'] = "y", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ь'] = "", ['ъ'] = ""
    };

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static string BuildCss(SiteModel site)
    {
        return $$"""
:root {
  --bg: {{site.Palette.Background}};
  --ink: {{site.Palette.Ink}};
  --accent: {{site.Palette.Accent}};
  --line: {{site.Palette.Line}};
  --paper: {{site.Palette.Paper}};
}
* {
  box-sizing: border-box;
}
body {
  margin: 0;
  background: var(--bg);
  color: var(--ink);
  font: 17px/1.65 Georgia, "Times New Roman", serif;
}
a {
  color: inherit;
  text-underline-offset: 4px;
  text-decoration-color: var(--accent);
}
.shell {
  max-width: 1180px;
  margin: 0 auto;
  padding: 24px 28px 64px;
}
.site-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  border-bottom: 1px solid var(--line);
  padding-bottom: 18px;
  font-family: Arial, sans-serif;
}
.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 800;
  text-decoration: none;
  letter-spacing: -.03em;
}
.mark {
  color: var(--accent);
  font-size: 1.4rem;
}
.site-header nav {
  display: flex;
  gap: 18px;
  font-size: .9rem;
}
.site-header nav a {
  text-decoration: none;
}
.hero {
  max-width: 760px;
  padding: 92px 0 56px;
}
.hero h1, .page-head h1, .post h1, .not-found h1 {
  font-size: clamp(2.7rem, 7vw, 6rem);
  line-height: .97;
  letter-spacing: -.06em;
  margin: 10px 0 22px;
}
.hero > p:not(.eyebrow) {
  max-width: 620px;
  font-size: 1.22rem;
}
.eyebrow {
  color: var(--accent);
  font: 700 .72rem/1.4 Arial, sans-serif;
  letter-spacing: .12em;
  text-transform: uppercase;
}
.button {
  display: inline-block;
  margin-top: 14px;
  padding: 10px 16px;
  background: var(--accent);
  color: var(--bg);
  border-radius: 99px;
  font: 700 .82rem Arial, sans-serif;
  text-decoration: none;
}
.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 250px;
  gap: 44px;
}
.section-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}
.section-head span {
  color: var(--accent);
  font: .8rem Arial, sans-serif;
}
h2 {
  font-size: 1.55rem;
  letter-spacing: -.03em;
}
.card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
}
.card {
  overflow: hidden;
  border: 1px solid var(--line);
  background: var(--paper);
  padding: 18px;
  border-radius: 12px;
  transition: transform .2s ease, box-shadow .2s ease;
}
.card:hover {
  transform: translateY(-4px);
  box-shadow: 0 14px 28px #0002;
}
.cover {
  display: block;
  margin: -18px -18px 16px;
}
.cover img, figure img {
  display: block;
  width: 100%;
  aspect-ratio: 16 / 9;
  object-fit: cover;
}
.card h3 {
  margin: 8px 0;
  font-size: 1.4rem;
  line-height: 1.1;
}
.card h3 a {
  text-decoration: none;
}
.card p:not(.eyebrow) {
  margin-bottom: 12px;
  font-size: .95rem;
}
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}
.tags a {
  border: 1px solid var(--line);
  border-radius: 99px;
  padding: 3px 8px;
  font: .7rem Arial, sans-serif;
  text-decoration: none;
}
.sidebar {
  border-left: 1px solid var(--line);
  padding-left: 22px;
}
.sidebar h2 {
  margin-top: 0;
}
.sidebar > a {
  font: .85rem Arial, sans-serif;
}
.post {
  max-width: 760px;
  margin: 68px auto 38px;
}
.post figure {
  margin: 32px 0;
}
.post figcaption {
  color: var(--accent);
  font: .78rem Arial, sans-serif;
  margin-top: 7px;
}
.post p, .post li {
  max-width: 68ch;
}
.post blockquote {
  margin: 28px 0;
  padding: 12px 20px;
  border-left: 3px solid var(--accent);
  font-size: 1.2rem;
}
.post pre {
  overflow-x: auto;
  padding: 16px;
  background: #0001;
  border-radius: 8px;
}
.avatar {
  float: right;
  width: 148px;
  margin: 0 0 18px 24px;
}
.post-nav {
  display: flex;
  justify-content: space-between;
  gap: 20px;
  max-width: 760px;
  margin: 0 auto 52px;
}
.post-nav a {
  display: grid;
  font-size: .94rem;
}
.post-nav small {
  color: var(--accent);
  font-family: Arial, sans-serif;
}
.related {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 15px;
}
.related .card {
  padding: 14px;
}
.page-head {
  padding: 68px 0 30px;
}
.archive-group {
  max-width: 760px;
  border-top: 1px solid var(--line);
  padding: 18px 0;
}
.archive-group a {
  display: grid;
  grid-template-columns: 80px 1fr;
  gap: 14px;
  padding: 7px 0;
  text-decoration: none;
}
.archive-group time {
  color: var(--accent);
  font: .78rem Arial, sans-serif;
}
.not-found {
  min-height: 56vh;
  display: grid;
  align-content: center;
  max-width: 760px;
}
.not-found p {
  font-size: 1.2rem;
}
footer {
  margin-top: 84px;
  border-top: 1px solid var(--line);
  padding-top: 18px;
  color: var(--accent);
  font: .78rem Arial, sans-serif;
}
.folio .hero {
  max-width: 900px;
}
.folio .card {
  border-radius: 0;
}
.folio .mark {
  transform: rotate(45deg);
}
.atlas .sidebar {
  border-left-width: 3px;
}
@media (max-width: 760px) {
  .shell {
    padding: 18px 18px 42px;
  }
  .site-header, .content-grid {
    display: block;
  }
  .site-header nav {
    margin-top: 14px;
  }
  .hero {
    padding: 58px 0 38px;
  }
  .card-grid {
    grid-template-columns: 1fr;
  }
  .sidebar {
    border-left: 0;
    border-top: 1px solid var(--line);
    padding: 24px 0 0;
    margin-top: 30px;
  }
  .related {
    grid-template-columns: 1fr;
  }
  .post {
    margin-top: 42px;
  }
  .avatar {
    width: 100px;
  }
}
""";
    }

    private static string BuildFavicon(SitePalette palette)
    {
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">" +
            $"<rect width=\"64\" height=\"64\" rx=\"16\" fill=\"{palette.Accent}\"/>" +
            $"<circle cx=\"32\" cy=\"32\" r=\"13\" fill=\"{palette.Background}\"/></svg>";
    }

    private static string BuildAvatar(SitePalette palette)
    {
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 240 240\">" +
            $"<rect width=\"240\" height=\"240\" rx=\"42\" fill=\"{palette.Accent}\"/>" +
            $"<circle cx=\"120\" cy=\"91\" r=\"44\" fill=\"{palette.Paper}\"/>" +
            $"<path d=\"M42 220c13-51 43-75 78-75s65 24 78 75\" fill=\"{palette.Paper}\"/></svg>";
    }

    // Обложки записей: три композиции, формы и положение зависят от номера записи.
    private static string BuildIllustration(SitePalette palette, int number)
    {
        var shift = 24 + number % 7 * 37;
        var height = 180 + number * 53 % 220;
        var body = (number % 3) switch
        {
            0 => $"<circle cx=\"{220 + shift}\" cy=\"180\" r=\"{110 + number % 5 * 15}\" fill=\"{palette.Accent}\" opacity=\".82\"/>" +
                $"<path d=\"M0 540 310 {height + 100}l210 170 210-250 470 340v135H0Z\" fill=\"{palette.Ink}\" opacity=\".88\"/>" +
                $"<rect x=\"720\" y=\"82\" width=\"310\" height=\"310\" rx=\"155\" fill=\"{palette.Paper}\" opacity=\".8\"/>",
            1 => $"<rect x=\"{80 + shift}\" y=\"90\" width=\"420\" height=\"{height + 120}\" rx=\"28\" fill=\"{palette.Accent}\" opacity=\".75\"/>" +
                $"<rect x=\"{560 - shift / 2}\" y=\"{height}\" width=\"520\" height=\"260\" rx=\"28\" fill=\"{palette.Ink}\" opacity=\".85\"/>" +
                $"<circle cx=\"960\" cy=\"150\" r=\"70\" fill=\"{palette.Paper}\"/>",
            _ => $"<path d=\"M0 {height + 200}C300 {height} 600 {height + 280} 1200 {height + 60}V675H0Z\" fill=\"{palette.Accent}\" opacity=\".8\"/>" +
                $"<path d=\"M0 {height + 300}C400 {height + 160} 800 {height + 360} 1200 {height + 220}V675H0Z\" fill=\"{palette.Ink}\" opacity=\".9\"/>" +
                $"<circle cx=\"{900 - shift}\" cy=\"140\" r=\"64\" fill=\"{palette.Paper}\"/>"
        };
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1200 675\">" +
            $"<rect width=\"1200\" height=\"675\" fill=\"{palette.Background}\"/>{body}</svg>";
    }

    private static string BuildSitemap(IEnumerable<SitePost> posts)
    {
        var list = posts.ToList();
        var latest = list.Count == 0 ? new DateOnly(2023, 1, 1) : list.Max(post => post.Date);
        var first = list.Count == 0 ? latest : list.Min(post => post.Date);
        var urls = new List<(string Url, DateOnly Date)> { ("/", latest), ("/about", first), ("/contacts", first), ("/archive", latest) };
        urls.AddRange(list.Select(post => ($"/notes/{post.Slug}", post.Date)));
        urls.AddRange(list.SelectMany(post => post.Tags).Distinct(StringComparer.Ordinal).Select(tag => ($"/tags/{ToSlug(tag)}", latest)));
        var entries = string.Join(string.Empty, urls.Select(item => $"<url><loc>{item.Url}</loc><lastmod>{item.Date:yyyy-MM-dd}</lastmod></url>"));
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">{entries}</urlset>";
    }

    private static string BuildRss(SiteModel site, IEnumerable<SitePost> posts)
    {
        var items = string.Join(string.Empty, posts.OrderByDescending(post => post.Date).Take(15).Select(post =>
            $"<item><title>{Html(post.Title)}</title><link>/notes/{post.Slug}</link>" +
            $"<description>{Html(post.Paragraphs[0])}</description>" +
            $"<pubDate>{post.Date.ToDateTime(TimeOnly.MinValue):R}</pubDate></item>"));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><rss version=\"2.0\"><channel>" +
            $"<title>{Html(site.Title)}</title><link>/</link><description>{Html(Intro(site.Template))}</description>{items}</channel></rss>";
    }

    private static string Pick(string[] bank, int index) => bank[Math.Abs(index) % bank.Length];

    private static readonly string[] Quotes =
    [
        "Иногда достаточно назвать увиденное, чтобы оно осталось с нами надолго.",
        "Лучшие планы складываются из маленьких шагов, которые не страшно начать.",
        "Медленная прогулка часто даёт больше, чем длинный список дел.",
        "Хорошая привычка — та, которую не нужно каждый раз заново уговаривать.",
        "Чем проще запись, тем легче к ней вернуться через год.",
        "Тишина в выходной утром стоит отдельной заметки.",
        "Ошибки полезнее всего, пока они ещё свежие и неловкие."
    ];

    private static readonly string[] CodeSnippets =
    [
        "наблюдение → решение → проверка",
        "git log --since=\"2 weeks\" --oneline",
        "SELECT count(*) FROM orders WHERE created_at &gt; now() - interval '1 day';",
        "docker compose logs -f --tail=100",
        "curl -s -o /dev/null -w \"%{http_code}\" https://example.org",
        "find . -name \"*.log\" -mtime +7 -delete"
    ];

    private static readonly string[] Captions =
    [
        "Заметка из архива автора", "Набросок на полях блокнота", "Схема, которую рисовал по ходу",
        "Из подборки за этот месяц", "Черновик, который пригодился", "Вид из окна в тот день"
    ];

    private static readonly string[] ChecklistTitles = ["На заметку", "Что запомнить", "Коротко", "Итог", "Что проверить в следующий раз"];

    private static readonly string[] ChecklistItems =
    [
        "Оставить время на наблюдение.", "Записать вывод простыми словами.", "Вернуться к записи через неделю.",
        "Не усложнять с самого начала.", "Сначала проверить очевидное.", "Сохранить ссылки на источники.",
        "Сделать перерыв, если идея не идёт.", "Спросить мнение со стороны.", "Убрать лишнее перед публикацией.",
        "Отметить, что получилось лучше ожидаемого.", "Сравнить с прошлым разом.", "Оставить пометку на будущее."
    ];

    private sealed record SitePost(string Title, string Slug, DateOnly Date, string[] Tags, string[] Paragraphs, int Number);

    private sealed record SitePalette(string Background, string Ink, string Accent, string Line, string Paper);

    private sealed record SiteModel(string Title, string Template, SitePalette Palette, string Variant, string Author, int StartYear, int EndYear);
}
