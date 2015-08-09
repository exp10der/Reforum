using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using HtmlAgilityPack;

internal class ReforumParser
{
    // NOTE: Вставить куки
    private static readonly string cookieSpb =
        @"";

    private static readonly string cookieMsk = @"";


    public XDocument xDocument { get; private set; }

    public ReforumParser()
    {
        
    }


    public void Builder()
    {
        var mark = new Markets(Html("http://reforum.ru/cpanel/realty/admin.html"));

        // Получаем исходный код всех страниц
        // создаем список страниц -> делаем проекцию для получения исходного кода страницы -> делаем выравнивание
        var query = mark.MarketsList
            .SelectMany(n => Enumerable.Range(1, n.Pages).Select((x, i) => Html(n.Url + (i + 1)))).ToList();
        // Делаем запрос на получение всех обратных сылок
        var q = query.AsParallel().SelectMany(n =>
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(n);
            return
                doc.DocumentNode.SelectNodes(".//*[@id='realty_grid']//*[@class='lc_link_under_address']/a")
                    .Select(x => x.GetAttributeValue("href", ""));
        }).ToList();

        // Убираем дубли
        var noCopy = q.Distinct().ToList();

        // NOTE: указываем временной диапазон для csv файла
        // http://reforum.ru/cpanel/realty/statCsv.html?advertiserId=105699&AdvertAnalytics%5BdateFrom%5D=2015-08-01&AdvertAnalytics%5BdateTo%5D=2015-08-02
        //$"http://reforum.ru/cpanel/realty/statCsv.html?advertiserId=105699&AdvertAnalytics%5BdateFrom%5D={DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")}&AdvertAnalytics%5BdateTo%5D={DateTime.Now.ToString("yyyy-MM-dd")}";

        //var csv =
        //    string.Format(
        //        "http://reforum.ru/cpanel/realty/statCsv.html?advertiserId=105699&AdvertAnalytics%5BdateFrom%5D={0}&AdvertAnalytics%5BdateTo%5D={1}",
        //        DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"), DateTime.Now.ToString("yyyy-MM-dd"));

        var csv =
            string.Format(
                "http://reforum.ru/cpanel/realty/statCsv.html?advertiserId=105699&AdvertAnalytics%5BdateFrom%5D={0}&AdvertAnalytics%5BdateTo%5D={1}",
                DateTime.Now.ToString("yyyy-MM-dd"), DateTime.Now.ToString("yyyy-MM-dd"));

        // Получаем csv
        var str = Html(csv);

        // Сопостовляем сылки пример
        // "http://spb.reforum.ru/flat/prodam-pyatikomnatnuyu-kvartiru-140542433.html"  по этому номеру 140542433 можно найти в csv всю необходимую инфу 
        //100804131; 3318712; 2015-08-02; 3; 3
        // первая и последняя строка мусор нужно скипать
        /* Формат CSV файла:
           
        * 1) номер в сылки              100804131
        * 2) RequestId                  3318712                НЕ ВСЕГДА ЕСТЬ ПОЧЕМУ ТО!
        * 3) дата выбрасываем           2015-08-02
        * 4) Просмотры карточки         3
        * 5) Показы                     3

        */

        var report =
            str.Split(new[] {"\n"}, StringSplitOptions.RemoveEmptyEntries).Skip(1).Reverse().Skip(1).Select(n =>
            {
                var tmp = n.Split(';').ToArray();
                return new {Number = tmp[0], RequestId = tmp[1], visits = tmp[3]};
            }).ToArray();
        
        // Джойним cvs файл(почему то в нем есть дубликаты убираем их) с обратными сылками
        var result = noCopy
            .Join(report, x => string.Concat(x.Where(char.IsNumber)), y => y.Number, (x, y) => new
            {
                Id = y.RequestId,
                Url = x,
                CountView = y.visits
            }).GroupBy(n => n.Id).Select(n => n.First());


        // Создаем XDocumet
        xDocument = new XDocument(
            new XDeclaration(null, "utf-8", null),
            new XElement("Root",
                result.Select(
                    n =>
                        new XElement("Advert", new XElement("Id", n.Id), new XElement("Url", n.Url),
                            new XElement("CountView", n.CountView))
                    )));
    }

    public static string Html(string url)
    {
        var request = (HttpWebRequest) WebRequest.Create(url);
        request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:39.0) Gecko/20100101 Firefox/39.0";
        request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        request.Headers.Set(HttpRequestHeader.AcceptLanguage, "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3");
        request.Referer = "http://spb.reforum.ru/auth.html";
        request.Headers.Set(HttpRequestHeader.Cookie, cookieSpb);
        request.Headers.Add("DNT", @"1");
        request.KeepAlive = true;

        using (var response = (HttpWebResponse) request.GetResponse())
        {
            using (var stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return stream.ReadToEnd();
            }
        }
    }
}


internal class Markets
{
    public Markets(string html)
    {
        MarketsList = new List<Market>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var q = doc.DocumentNode.SelectNodes(".//*[@id='yw2']/li/a");

        // Фильтруем рынки где нету заявок и заполняем List<Market>
        MarketsList = q.Where(n => int.Parse(n.Element("strong").InnerText) > 0).Select(n => new Market
        {
            Name = n.FirstChild.InnerText,
            Url = "http://reforum.ru" + n.GetAttributeValue("href", "") + "&AdvertComm_page=",
            // На одной странице 25 объявлений
            Pages = (int) Math.Ceiling(int.Parse(n.Element("strong").InnerText)/25.0)
        }).ToList();
    }

    public List<Market> MarketsList { get; private set; }
}

internal class Market
{
    public string Name { get; set; }
    public string Url { get; set; }
    public int Pages { get; set; }
}