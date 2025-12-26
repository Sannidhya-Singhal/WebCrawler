// Models/CrawlData.cs
namespace WebCrawler.Models
{
    public class CrawlData
    {
        public string SearchOnGoogleUrl { get; set; }
        public string SearchPageUrl { get; set; }
        public string DetailPageUrl { get; set; }
        
        // Crawled data
        public List<string> GoogleHeadings { get; set; } = new();
        public List<string> AmazonSearchTitles { get; set; } = new();
        public Dictionary<string, string> ProductOverview { get; set; } = new();
        public string AboutThisItem { get; set; }
    }
}