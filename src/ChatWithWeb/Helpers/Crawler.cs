using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Abot2.Crawler;
using Abot2.Poco;
using ChatWithWeb.Data;
using ChatWithWeb.Models;
using HtmlAgilityPack;
using Microsoft.SemanticMemory;
using Microsoft.SemanticMemory.MemoryStorage.DevTools;

namespace ChatWithWeb.Helpers;
public class Crawler
{
    public EventHandler<PageItem> ItemCrawled;
    public EventHandler<IndexItem> ItemIndexed;
    TextExtractor _textExtractor;
    static string _CrawledDir;
    public static string CrawledDir
    {
        get
        {
            if (string.IsNullOrEmpty(_CrawledDir))
            {
                var fPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                _CrawledDir = Path.Combine(fPath, "Crawled");
            }           
            return _CrawledDir;
        }
    }
    
    static string _VectorDir;
    public static string VectorDir
    {
        get
        {
            if (string.IsNullOrEmpty(_VectorDir))
            {
                var fPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                _VectorDir = Path.Combine(fPath, "VectorData");
            }           
            return _VectorDir;
        }
    }
    string WebToCrawl { set; get; }
    public bool IsProcessing { set; get; } = false;

    public int Counter { get; set; } = 0;
    HtmlDocument htmlDoc;
    
    public Crawler()
    {
        _textExtractor = new();
        htmlDoc = new HtmlDocument();

        if (!Directory.Exists(CrawledDir))
        {
            Directory.CreateDirectory(CrawledDir);
        }
        SetupIndexer();
    }
    Memory DocMemory;
    void SetupIndexer()
    {
        var folderText = Crawler.CrawledDir;        
        //string model, apiKey, orgId;
        var (model, apiKey, orgId) = AppConstants.GetSettings();
        var configVector = new SimpleVectorDbConfig() { StorageType = SimpleVectorDbConfig.StorageTypes.TextFile, Directory = VectorDir };
        DocMemory = new Microsoft.SemanticMemory.MemoryClientBuilder()
        .WithSimpleVectorDb(configVector)
        .WithOpenAIDefaults(apiKey, orgId)
        .BuildServerlessClient();

    }

    
    public async Task DoIndexing()
    {
        if (IsProcessing) return;
        IsProcessing = true;
        try
        {
            var Counter = 1;
            DirectoryInfo dir = new DirectoryInfo(CrawledDir);
            foreach (var CurrentFile in dir.GetFiles("*.txt"))
            {
                var tagcollection = new TagCollection();
                tagcollection.Add("user", "user1");
                tagcollection.Add("filename", CurrentFile.Name);
                var bytes = File.ReadAllBytes(CurrentFile.FullName);
                var ms = new MemoryStream(bytes);
                await DocMemory.ImportDocumentAsync(ms, CurrentFile.Name, documentId: CurrentFile.Name, tagcollection);
                System.Console.WriteLine($"file {CurrentFile.Name} has been indexed.");
                ItemIndexed?.Invoke(this, new IndexItem() { FileName = CurrentFile.Name, No = Counter++ });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("index fail:" + ex);
        }
        finally
        {
            IsProcessing = false;
        }
        
    }
    public async Task ProcessCrawl(string WebUrl)
    {       
        if (IsProcessing) return;
        this.WebToCrawl = WebUrl;
        this.Counter = 0;
        IsProcessing = true;
        await Task.Delay(1);
        try
        {
            var crawlConfig = new CrawlConfiguration();
            crawlConfig.MinCrawlDelayPerDomainMilliSeconds = 5000;
            crawlConfig.CrawlTimeoutSeconds = 100;
            crawlConfig.MaxConcurrentThreads = 10;
            crawlConfig.MaxPagesToCrawl = 1000;
            crawlConfig.UserAgentString = "Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/W.X.Y.Z Mobile Safari/537.36";//"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
            //crawlConfig.ConfigurationExtensions.Add("SomeCustomConfigValue1", "1111");
            //crawlConfig.ConfigurationExtensions.Add("SomeCustomConfigValue2", "2222");

            var crawler = new PoliteWebCrawler(crawlConfig);

            crawler.PageCrawlStarting += crawler_ProcessPageCrawlStarting;
            crawler.PageCrawlCompleted += crawler_ProcessPageCrawlCompleted;
            crawler.PageCrawlDisallowed += crawler_PageCrawlDisallowed;
            crawler.PageLinksCrawlDisallowed += crawler_PageLinksCrawlDisallowed;

            var crawlResult = await crawler.CrawlAsync(new Uri(this.WebToCrawl));
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
        }
        finally
        {
            IsProcessing = false;
        }
        Console.WriteLine("Crawl is finished.");

    }
    void crawler_ProcessPageCrawlStarting(object sender, PageCrawlStartingArgs e)
    {
        PageToCrawl pageToCrawl = e.PageToCrawl;
        Console.WriteLine($"About to crawl link {pageToCrawl.Uri.AbsoluteUri} which was found on page {pageToCrawl.ParentUri.AbsoluteUri}");
    }

    void crawler_ProcessPageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
    {
        CrawledPage crawledPage = e.CrawledPage;
        if (crawledPage.HttpRequestException != null || crawledPage.HttpResponseMessage.StatusCode != HttpStatusCode.OK)
            Console.WriteLine($"Crawl of page failed {crawledPage.Uri.AbsoluteUri}");
        else
            Console.WriteLine($"Crawl of page succeeded {crawledPage.Uri.AbsoluteUri}");

        if (string.IsNullOrEmpty(crawledPage.Content.Text))
            Console.WriteLine($"Page had no content {crawledPage.Uri.AbsoluteUri}");
        htmlDoc.LoadHtml(crawledPage.Content.Text);
        var rawPageText = _textExtractor.ExtractText(htmlDoc);
        if (rawPageText == null)
        {
            Console.WriteLine("No content for page {0}", crawledPage?.Uri.AbsoluteUri);
            return;
        }
        /*
        var angleSharpHtmlDocument = crawledPage.AngleSharpHtmlDocument; //AngleSharp parser
        var rawPageText = angleSharpHtmlDocument.Body.InnerHtml;//e.CrawledPage.Content.Text;
        htmlDoc.LoadHtml(rawPageText);
        rawPageText = htmlDoc.DocumentNode.InnerText;
        */
        //remove new line
        rawPageText = Regex.Replace(rawPageText, @"\t|\n|\r", "");
        //remove multi space        
        rawPageText = Regex.Replace(rawPageText, @"\s+", " ");

        rawPageText = rawPageText.Trim();
       
        if (!string.IsNullOrWhiteSpace(rawPageText))
        {
            String originalPath = crawledPage.Uri.OriginalString;
            String pageTitle = originalPath.Remove(0, originalPath.LastIndexOf("/"));
            if (string.IsNullOrEmpty(pageTitle.Trim())) pageTitle = "index";
            var fileHtml = $"{CrawledDir}{pageTitle}_{DateTime.Now.ToString("HH_mm_ss")}.txt";
            File.WriteAllText(fileHtml, rawPageText);
            System.Console.WriteLine($"Page extracted: {crawledPage.Uri} to {fileHtml}");
            ItemCrawled?.Invoke(this,new PageItem() { No = Counter++, FileName = fileHtml, URL = crawledPage.Uri.ToString() });
        }
    }


    public static string RemoveHTMLTagsText(string html, string tag)
    {
        int startIndex = html.IndexOf(tag.Remove(tag.Length - 1));
        startIndex = html.IndexOf(">", startIndex) + 1;
        int endIndex = html.IndexOf(tag.Insert(1, "/"), startIndex) - startIndex;
        html = html.Remove(startIndex, endIndex);
        return html;
    }

    void crawler_PageLinksCrawlDisallowed(object sender, PageLinksCrawlDisallowedArgs e)
    {
        CrawledPage crawledPage = e.CrawledPage;
        Console.WriteLine($"Did not crawl the links on page {crawledPage.Uri.AbsoluteUri} due to {e.DisallowedReason}");
    }

    void crawler_PageCrawlDisallowed(object sender, PageCrawlDisallowedArgs e)
    {
        PageToCrawl pageToCrawl = e.PageToCrawl;
        Console.WriteLine($"Did not crawl page {pageToCrawl.Uri.AbsoluteUri} due to {e.DisallowedReason}");
    }
  

}