// Services/CefSharpCrawlerService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using HtmlAgilityPack;

namespace WebCrawler.Services
{
    public class CefSharpCrawlerService : IDisposable
    {
        private static ChromiumWebBrowser _browser;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static bool _isInitialized = false;

        public CefSharpCrawlerService()
        {
            InitializeBrowser();
        }

        /// <summary>
        /// Initialize browser only once (singleton pattern)
        /// </summary>
        private void InitializeBrowser()
        {
            if (_isInitialized) return;

            lock (typeof(CefSharpCrawlerService))
            {
                if (_isInitialized) return;

                try
                {
                    Console.WriteLine("Initializing CefSharp browser...");
                    
                    _browser = new ChromiumWebBrowser("");
                    
                    // Wait for browser initialization
                    var initTask = _browser.WaitForInitialLoadAsync();
                    initTask.Wait(TimeSpan.FromSeconds(10));

                    _isInitialized = true;
                    Console.WriteLine("? CefSharp browser initialized");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Failed to initialize CefSharp: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Load a URL and get the fully-rendered HTML
        /// Thread-safe version using semaphore
        /// </summary>
        private async Task<string> LoadPageHtml(string url)
        {
            await _semaphore.WaitAsync(); // Only allow one page load at a time

            try
            {
                Console.WriteLine($"  ? Loading with CefSharp: {url}");

                if (!_isInitialized || _browser == null)
                {
                    Console.WriteLine("  ?? Browser not initialized");
                    return null;
                }

                // Load the page
                var loadResponse = await _browser.LoadUrlAsync(url);

                if (!loadResponse.Success)
                {
                    Console.WriteLine($"  ?? Failed to load: {loadResponse.ErrorCode} - {loadResponse.HttpStatusCode}");
                    return null;
                }

                // Wait for JavaScript to execute
                await Task.Delay(3000);

                // Get the HTML after JavaScript execution
                var html = await _browser.GetSourceAsync();

                Console.WriteLine($"  ? Loaded with CefSharp ({html.Length} chars)");
                return html;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? CefSharp error: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Crawl Google search results
        /// </summary>
        public async Task<List<string>> CrawlGoogleHeadings(string url, int resultCount = 10)
        {
            var headings = new List<string>();

            try
            {
                var html = await LoadPageHtml(url);

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("  ? Could not load Google page");
                    return headings;
                }

                // Check for CAPTCHA
                if (html.Contains("recaptcha") || html.Contains("unusual traffic"))
                {
                    Console.WriteLine("  ?? CAPTCHA detected!");
                    return headings;
                }

                // Parse with HtmlAgilityPack
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Multiple extraction strategies
                var strategies = new Func<List<string>>[]
                {
                    () => ExtractShoppingProducts(doc,resultCount),
                    () => ExtractGenericH3(doc,resultCount),
                    () => ExtractDivHeadings(doc,resultCount)
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
                return headings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Google: {ex.Message}");
                return headings;
            }
        }

        /// <summary>
        /// Crawl Amazon search results - ULTRA SIMPLIFIED
        /// </summary>
        public async Task<List<string>> CrawlAmazonSearchTitles(string url, int resultCount = 10)
        {
            var titles = new List<string>();

            try
            {
                var html = await LoadPageHtml(url);

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("  ? Could not load Amazon search page");
                    return titles;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Get ALL h2 tags - Amazon typically uses h2 for product titles
                var h2Nodes = doc.DocumentNode.Descendants("h2");
                
                if (h2Nodes != null && h2Nodes.Any())
                {
                    titles = h2Nodes
                        .Select(n => n.InnerText.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Where(t => t.Length > 15 && t.Length < 300)
                        .Distinct()
                        .Skip(2)
                        .Take(resultCount)
                        .ToList();
                        
                    Console.WriteLine($"  ? Found {titles.Count} potential product titles");
                    
                    // Print first few for debugging
                    foreach (var title in titles.Take(3))
                    {
                        Console.WriteLine($"    Sample: {title.Substring(0, Math.Min(60, title.Length))}...");
                    }
                    
                    return titles;
                }

                // Fallback: Get all spans and filter by length
                Console.WriteLine("  ?? No h2 tags found, trying spans...");
                var spanNodes = doc.DocumentNode.Descendants("span");
                
                if (spanNodes != null && spanNodes.Any())
                {
                    titles = spanNodes
                        .Select(n => n.InnerText.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Where(t => t.Length > 20 && t.Length < 200)
                        .Where(t => !t.Contains("$"))
                        .Where(t => !t.Contains("?"))
                        .Where(t => !t.StartsWith("("))
                        .GroupBy(t => t)
                        .Select(g => g.Key)
                        .Take(resultCount)
                        .ToList();
                        
                    Console.WriteLine($"  ? Found {titles.Count} potential titles from spans");
                    return titles;
                }

                Console.WriteLine("  ? Could not find any product titles");
                //SaveDebugHtml(html, $"amazon_no_results_{DateTime.Now:yyyyMMddHHmmss}.html");
                
                return titles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error: {ex.Message}");
                return titles;
            }
        }

        /// <summary>
        /// Crawl Amazon product page
        /// </summary>
        public async Task<(Dictionary<string, string> overview, string aboutItem)> CrawlAmazonProductPage(string url)
        {
            var overview = new Dictionary<string, string>();
            var aboutItem = string.Empty;

            try
            {
                var html = await LoadPageHtml(url);

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("  ? Could not load Amazon product page");
                    return (overview, aboutItem);
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // === PRODUCT OVERVIEW - Multiple strategies ===
                Console.WriteLine("  ? Extracting product overview...");
                
                // Strategy 1: Look for ANY table with rows (th/td pairs)
                var allTables = doc.DocumentNode.Descendants("table");
                foreach (var table in allTables)
                {
                    var rows = table.Descendants("tr");
                    foreach (var row in rows)
                    {
                        var th = row.Descendants("th").FirstOrDefault();
                        var td = row.Descendants("td").FirstOrDefault();
                        
                        if (th != null && td != null)
                        {
                            var key = th.InnerText.Trim();
                            var value = td.InnerText.Trim();
                            
                            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value) 
                                && key.Length < 100 && value.Length < 500)
                            {
                                if (!overview.ContainsKey(key))
                                {
                                    overview[key] = value;
                                }
                            }
                        }
                    }
                }

                // Strategy 2: Look for div pairs that might be key-value
                if (overview.Count == 0)
                {
                    Console.WriteLine("  ? Trying alternate overview extraction...");
                    var divs = doc.DocumentNode.Descendants("div")
                        .Where(d => d.GetAttributeValue("class", "").Contains("prodDetSectionEntry"));
                    
                    foreach (var div in divs.Take(20))
                    {
                        var text = div.InnerText.Trim();
                        if (text.Contains(":"))
                        {
                            var parts = text.Split(new[] { ':' }, 2);
                            if (parts.Length == 2)
                            {
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();
                                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                                {
                                    overview[key] = value;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"  ? Found {overview.Count} product overview attributes");

                // === ABOUT THIS ITEM - Multiple strategies ===
                Console.WriteLine("  ? Extracting 'About This Item'...");
                
                // Strategy 1: Get ALL <li> tags (Amazon uses lists for features)
                var allListItems = doc.DocumentNode.Descendants("li")
                    .Select(li => li.InnerText.Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Where(text => text.Length > 20 && text.Length < 500)  // Reasonable feature length
                    .Where(text => !text.Contains("$"))  // Exclude prices
                    .Where(text => !text.StartsWith("("))  // Exclude parenthetical notes
                    .Distinct()
                    .ToList();

                if (allListItems.Any())
                {
                    // Take the most substantial list items (likely product features)
                    var features = allListItems
                        .OrderByDescending(t => t.Length)
                        .Take(10)
                        .ToList();
                    
                    aboutItem = string.Join(" | ", features);
                    Console.WriteLine($"  ? Found {features.Count} product features");
                }

                // Strategy 2: If no list items, try finding any div with substantial text
                if (string.IsNullOrEmpty(aboutItem))
                {
                    Console.WriteLine("  ? Trying alternate feature extraction...");
                    
                    var divTexts = doc.DocumentNode.Descendants("div")
                        .Select(d => d.InnerText.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Where(t => t.Length > 50 && t.Length < 1000)
                        .Distinct()
                        .Take(5)
                        .ToList();
                    
                    if (divTexts.Any())
                    {
                        aboutItem = string.Join(" | ", divTexts);
                        Console.WriteLine($"  ? Found {divTexts.Count} text blocks");
                    }
                }

                if (overview.Count == 0 && string.IsNullOrEmpty(aboutItem))
                {
                    Console.WriteLine("  ?? No product details found - saving debug HTML");
                    SaveDebugHtml(html, $"amazon_product_debug_{DateTime.Now:yyyyMMddHHmmss}.html");
                }

                return (overview, aboutItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Amazon product: {ex.Message}");
                return (overview, aboutItem);
            }
        }

        /// <summary>
        /// Crawl Amazon product page - Extract individual list items
        /// </summary>
        public async Task<(Dictionary<string, string> overview, List<string> aboutItems)> CrawlAmazonProductPageV1(string url)
        {
            var overview = new Dictionary<string, string>();
            var aboutItems = new List<string>();

            try
            {
                var html = await LoadPageHtml(url);

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("  ? Could not load Amazon product page");
                    return (overview, aboutItems);
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // === PRODUCT OVERVIEW ===
                Console.WriteLine("  ? Extracting product overview...");
                
                var allTables = doc.DocumentNode.Descendants("table");
                foreach (var table in allTables)
                {
                    var rows = table.Descendants("tr");
                    foreach (var row in rows)
                    {
                        var th = row.Descendants("th").FirstOrDefault();
                        var td = row.Descendants("td").FirstOrDefault();
                        
                        if (th != null && td != null)
                        {
                            var key = th.InnerText.Trim();
                            var value = td.InnerText.Trim();
                            
                            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value) 
                                && key.Length < 100 && value.Length < 500)
                            {
                                if (!overview.ContainsKey(key))
                                {
                                    overview[key] = value;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"  ? Found {overview.Count} product overview attributes");

                // === ABOUT THIS ITEM - Extract individual <li> tags ===
                Console.WriteLine("  ? Extracting 'About This Item' list items...");
                
                // Strategy 1: Look for feature-bullets section specifically
                var featureBulletsDiv = doc.DocumentNode.SelectSingleNode("//div[@id='feature-bullets']");
                
                if (featureBulletsDiv != null)
                {
                    var listItems = featureBulletsDiv.Descendants("li")
                        .Where(li => li.GetAttributeValue("class", "").Contains("a-spacing-mini"))
                        .ToList();

                    foreach (var li in listItems)
                    {
                        // Get the span with class="a-list-item"
                        var span = li.Descendants("span")
                            .FirstOrDefault(s => s.GetAttributeValue("class", "").Contains("a-list-item"));
                        
                        if (span != null)
                        {
                            var text = span.InnerText.Trim();
                            
                            // Clean up the text
                            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                            
                            if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                            {
                                aboutItems.Add(text);
                                Console.WriteLine($"    • Feature {aboutItems.Count}: {text.Substring(0, Math.Min(60, text.Length))}...");
                            }
                        }
                    }
                }

                // Strategy 2: Try detailBullets_feature_div (NEW FALLBACK)
                if (aboutItems.Count == 0)
                {
                    Console.WriteLine("  ? Trying detailBullets_feature_div extraction...");
                    
                    var detailBulletsDiv = doc.DocumentNode.SelectSingleNode("//div[@id='detailBullets_feature_div']");
                    
                    if (detailBulletsDiv != null)
                    {
                        // Extract list items from the unordered list
                        var listItems = detailBulletsDiv.Descendants("li")
                            .ToList();

                        foreach (var li in listItems)
                        {
                            // Get the span with class="a-list-item"
                            var listItemSpan = li.Descendants("span")
                                .FirstOrDefault(s => s.GetAttributeValue("class", "").Contains("a-list-item"));
                            
                            if (listItemSpan != null)
                            {
                                var text = listItemSpan.InnerText.Trim();
                                
                                // Clean up the text
                                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                                
                                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                                {
                                    aboutItems.Add(text);
                                    Console.WriteLine($"    • Detail Bullet {aboutItems.Count}: {text.Substring(0, Math.Min(60, text.Length))}...");
                                }
                            }
                        }
                    }
                }

                // Strategy 3: If still not found, try generic li extraction
                if (aboutItems.Count == 0)
                {
                    //Console.WriteLine("  ? Trying generic list item extraction...");
                    
                    //var allListItems = doc.DocumentNode.Descendants("li")
                    //    .Select(li => li.InnerText.Trim())
                    //    .Where(text => !string.IsNullOrWhiteSpace(text))
                    //    .Where(text => text.Length > 20 && text.Length < 500)
                    //    .Where(text => !text.Contains("$"))
                    //    .Where(text => !text.StartsWith("("))
                    //    .Distinct()
                    //    .Take(10)
                    //    .ToList();

                    //aboutItems.AddRange(allListItems);
                }

                Console.WriteLine($"  ? Found {aboutItems.Count} product features");

                if (overview.Count == 0 && aboutItems.Count == 0)
                {
                    Console.WriteLine("  ?? No product details found - saving debug HTML");
                    //SaveDebugHtml(html, $"amazon_product_debug_{DateTime.Now:yyyyMMddHHmmss}.html");
                }

                return (overview, aboutItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? Error crawling Amazon product: {ex.Message}");
                return (overview, aboutItems);
            }
        }

        /// <summary>
        /// Save HTML to desktop for debugging
        /// </summary>
        private void SaveDebugHtml(string html, string filename)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                    filename
                );
                System.IO.File.WriteAllText(path, html);
                Console.WriteLine($"  ?? Debug HTML saved: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ?? Could not save debug HTML: {ex.Message}");
            }
        }

        // Extraction methods
        private List<string> ExtractShoppingProducts(HtmlDocument doc, int resultCount = 10)
        {
            return doc.DocumentNode
                .SelectNodes("//div[@data-docid]//h3 | //div[@data-content-id]//h3 | //h3[@class='tAxDx']")
                ?.Select(n => n.InnerText.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .Take(resultCount)
                .ToList() ?? new List<string>();
        }

        private List<string> ExtractGenericH3(HtmlDocument doc, int resultCount = 10)
        {
            return doc.DocumentNode
                .SelectNodes("//h3")
                ?.Select(n => n.InnerText.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 10 && t.Length < 200)
                .Distinct()
                .Take(resultCount)
                .ToList() ?? new List<string>();
        }

        private List<string> ExtractDivHeadings(HtmlDocument doc, int resultCount = 10)
        {
            return doc.DocumentNode
                    .SelectNodes("//div[contains(@class, 'pla-unit')]//span[contains(@class, 'title')]")
                    ?.Take(resultCount)
                    .Select(n => n.InnerText.Trim())
                    .ToList() ?? new List<string>();
        }

        public void Dispose()
        {
            // Don't dispose the static browser - it's shared
            // Only dispose when application exits
        }

        /// <summary>
        /// Call this when application is closing
        /// </summary>
        public static void Shutdown()
        {
            if (_browser != null)
            {
                _browser.Dispose();
                _browser = null;
                _isInitialized = false;
                Console.WriteLine("? CefSharp browser disposed");
            }
        }
    }
}