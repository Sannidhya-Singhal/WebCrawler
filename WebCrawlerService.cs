// Services/WebCrawlerService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace WebCrawler.Services
{
    public class WebCrawlerService : IDisposable
    {
        private IWebDriver _driver;
        private HtmlWeb _htmlWeb;
        private Random _random = new Random();
        private bool _useSelenium = false;

        public WebCrawlerService(bool useSeleniumByDefault = false)
        {
            _useSelenium = useSeleniumByDefault;
            
            // Initialize HtmlWeb for simple requests
            _htmlWeb = new HtmlWeb
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            };
        }

        private void InitializeSelenium()
        {
            if (_driver != null) return;

            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            _driver = new ChromeDriver(options);
            Console.WriteLine("? Selenium initialized");
        }

        /// <summary>
        /// Load HTML using HtmlAgilityPack (fast, no JavaScript)
        /// </summary>
        private async Task<HtmlDocument> LoadHtmlSimple(string url)
        {
            try
            {
           
                Console.WriteLine($"  ? Loading with HtmlWeb: {url}");
                
                // Add small delay to be polite
                await Task.Delay(_random.Next(500, 1500));
                
                var doc = await Task.Run(() => _htmlWeb.Load(url));
                
                if (doc == null || doc.DocumentNode == null)
                {
                    Console.WriteLine("  ?? Failed to load with HtmlWeb");
                    return null;
                }

                Console.WriteLine("  ? Loaded with HtmlWeb");
                return doc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ?? HtmlWeb error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load HTML using Selenium (slower, executes JavaScript)
        /// </summary>
        private async Task<HtmlDocument> LoadHtmlWithSelenium(string url)
        {
            try
            {
                InitializeSelenium();
                
                Console.WriteLine($"  ? Loading with Selenium: {url}");
                
                await Task.Delay(_random.Next(2000, 4000));
                _driver.Navigate().GoToUrl(url);
                await Task.Delay(_random.Next(3000, 5000));

                var pageSource = _driver.PageSource;

                // Check for CAPTCHA
                if (IsCaptchaPage(pageSource))
                {
                    Console.WriteLine("  ?? CAPTCHA detected!");
                    SaveDebugHtml(pageSource, $"captcha_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                    return null;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(pageSource);
                
                Console.WriteLine("  ? Loaded with Selenium");
                return doc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Selenium error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Smart loader: Try simple first, fallback to Selenium
        /// </summary>
        private async Task<HtmlDocument> LoadHtml(string url, bool forceSelenium = false)
        {
            if (forceSelenium || _useSelenium)
            {
                return await LoadHtmlWithSelenium(url);
            }

            // Try simple method first
            DebugPrintAllNodesSimple(url);
                    var doc = await LoadHtmlSimple(url);
            
            // If simple method fails or returns empty content, try Selenium
            if (doc == null || doc.DocumentNode.InnerText.Length < 100)
            {
                Console.WriteLine("  ? Falling back to Selenium...");
                return await LoadHtmlWithSelenium(url);
            }

            return doc;
        }

        public async Task<List<string>> CrawlGoogleHeadings(string url)
        {
            var headings = new List<string>();

            try
            {
                // Google usually requires Selenium due to JavaScript
                var doc = await LoadHtml(url, forceSelenium: false);
                
                if (doc == null)
                {
                    Console.WriteLine("  ? Could not load Google page");
                    return headings;
                }

                // Multiple extraction strategies
                var strategies = new Func<List<string>>[]
                {
                    () => ExtractShoppingProducts(doc),
                    () => ExtractGenericH3(doc),
                    () => ExtractDivHeadings(doc)
                };

                foreach (var strategy in strategies)
                {
                    var results = strategy();
                    if (results.Any())
                    {
                        Console.WriteLine($"  ? Found {results.Count} Google headings");
                        return results;
                    }
                }

                Console.WriteLine("  ?? No products found on Google");
                SaveDebugHtml(doc.DocumentNode.OuterHtml, $"no_products_{DateTime.Now:yyyyMMdd_HHmmss}.html");

                return headings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Google: {ex.Message}");
                return headings;
            }
        }

        public async Task<List<string>> CrawlAmazonSearchTitles(string url)
        {
            var titles = new List<string>();

            try
            {
                // Try simple method first for Amazon search
                var doc = await LoadHtml(url, forceSelenium: false);
                
                if (doc == null)
                {
                    Console.WriteLine("  ? Could not load Amazon search page");
                    return titles;
                }

                // Amazon search page selectors
                var productTitles = doc.DocumentNode
                    .SelectNodes("//span[contains(@class, 'a-text-normal')] | //h2[contains(@class, 'a-size-mini')]//span | //h2[@class='a-size-mini']//span")
                    ?.Select(n => n.InnerText.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 10)
                    .Distinct()
                    .Take(10)
                    .ToList();

                if (productTitles != null && productTitles.Any())
                {
                    Console.WriteLine($"  ? Found {productTitles.Count} Amazon titles");
                    return productTitles;
                }

                Console.WriteLine("  ?? No Amazon titles found");
                return titles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Amazon search: {ex.Message}");
                return titles;
            }
        }

        public async Task<(Dictionary<string, string> overview, string aboutItem)> CrawlAmazonProductPage(string url)
        {
            var overview = new Dictionary<string, string>();
            var aboutItem = string.Empty;

            try
            {
                // Product pages often work with simple loading
                var doc = await LoadHtml(url, forceSelenium: false);
                
                if (doc == null)
                {
                    Console.WriteLine("  ? Could not load Amazon product page");
                    return (overview, aboutItem);
                }

                // Product Overview
                var overviewNodes = doc.DocumentNode
                    .SelectNodes("//div[@id='productOverview_feature_div']//tr | //table[@id='productDetails_techSpec_section_1']//tr");

                if (overviewNodes != null)
                {
                    foreach (var row in overviewNodes)
                    {
                        var key = row.SelectSingleNode(".//th")?.InnerText.Trim();
                        var value = row.SelectSingleNode(".//td")?.InnerText.Trim();

                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            overview[key] = value;
                        }
                    }
                }

                // About This Item
                var aboutNodes = doc.DocumentNode
                    .SelectNodes("//div[@id='feature-bullets']//li//span[@class='a-list-item'] | //div[@id='feature-bullets']//li");

                if (aboutNodes != null)
                {
                    aboutItem = string.Join(" | ", aboutNodes
                        .Select(n => n.InnerText.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 10));
                }

                Console.WriteLine($"  ? Found {overview.Count} product attributes");
                return (overview, aboutItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Amazon product: {ex.Message}");
                return (overview, aboutItem);
            }
        }

        private List<string> ExtractShoppingProducts(HtmlDocument doc)
        {
            return doc.DocumentNode
                .SelectNodes("//div[@data-docid]//h3 | //div[@data-content-id]//h3 | //h3[@class='tAxDx']")
                ?.Select(n => n.InnerText.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .Take(10)
                .ToList() ?? new List<string>();
        }

        private List<string> ExtractGenericH3(HtmlDocument doc)
        {
            return doc.DocumentNode
                .SelectNodes("//h3")
                ?.Select(n => n.InnerText.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 10 && t.Length < 200)
                .Distinct()
                .Take(10)
                .ToList() ?? new List<string>();
        }

        private List<string> ExtractDivHeadings(HtmlDocument doc)
        {
            return doc.DocumentNode
                .SelectNodes("//div[contains(@class, 'g')]//h3")
                ?.Select(n => n.InnerText.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .Take(10)
                .ToList() ?? new List<string>();
        }

        private bool IsCaptchaPage(string html)
        {
            var indicators = new[] { "recaptcha", "captcha-form", "g-recaptcha", "unusual traffic" };
            return indicators.Any(i => html.Contains(i, StringComparison.OrdinalIgnoreCase));
        }

        private void SaveDebugHtml(string html, string filename)
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
                File.WriteAllText(path, html);
                Console.WriteLine($"  ?? Debug HTML: {path}");
            }
            catch { }
        }

        /// <summary>
        /// Simple debug method - prints all nodes recursively using Debug.WriteLine
        /// </summary>
        private async Task DebugPrintAllNodesSimple(string url)
        {
            try
            {
                Debug.WriteLine("============================================");
                Debug.WriteLine($"DEBUG: Loading URL");
                Debug.WriteLine($"URL: {url}");
                Debug.WriteLine("============================================");
                Debug.WriteLine("");

                var doc = await LoadHtmlSimple(url);

                if (doc == null || doc.DocumentNode == null)
                {
                    Debug.WriteLine("ERROR: Document is NULL!");
                    return;
                }

                Debug.WriteLine($"Document loaded successfully");
                Debug.WriteLine($"Total HTML Length: {doc.DocumentNode.OuterHtml.Length} characters");
                Debug.WriteLine($"Root Child Nodes Count: {doc.DocumentNode.ChildNodes.Count}");
                Debug.WriteLine("");
                Debug.WriteLine("=== PRINTING ALL NODES ===");
                Debug.WriteLine("");

                // Print all nodes recursively
                PrintNodeRecursive(doc.DocumentNode, 0, maxDepth: 5);

                Debug.WriteLine("");
                Debug.WriteLine("=== END OF NODE TREE ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Recursively print node tree
        /// </summary>
        private void PrintNodeRecursive(HtmlNode node, int depth, int maxDepth = 5, int maxChildren = 50)
        {
            if (depth > maxDepth) return;

            string indent = new string(' ', depth * 2);
            
            // Print current node
            Debug.WriteLine($"{indent}[{node.NodeType}] {node.Name}");
            
            // Print attributes if any
            if (node.Attributes != null && node.Attributes.Count > 0)
            {
                foreach (var attr in node.Attributes.Take(5)) // Limit to 5 attributes
                {
                    string attrValue = attr.Value.Length > 50 
                        ? attr.Value.Substring(0, 50) + "..." 
                        : attr.Value;
                    Debug.WriteLine($"{indent}  @{attr.Name} = \"{attrValue}\"");
                }
            }
            
            // Print text content if it's a text node
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = node.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string displayText = text.Length > 100 
                        ? text.Substring(0, 100) + "..." 
                        : text;
                    Debug.WriteLine($"{indent}  TEXT: \"{displayText}\"");
                }
            }
            
            // Print children (limit to avoid overwhelming output)
            if (node.ChildNodes != null && node.ChildNodes.Count > 0)
            {
                Debug.WriteLine($"{indent}  Children: {node.ChildNodes.Count}");
                
                int childrenToPrint = Math.Min(node.ChildNodes.Count, maxChildren);
                for (int i = 0; i < childrenToPrint; i++)
                {
                    PrintNodeRecursive(node.ChildNodes[i], depth + 1, maxDepth, maxChildren);
                }
                
                if (node.ChildNodes.Count > maxChildren)
                {
                    Debug.WriteLine($"{indent}  ... {node.ChildNodes.Count - maxChildren} more children omitted");
                }
            }
        }

        public void Dispose()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}