// Demos/CefSharpDemo.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using HtmlAgilityPack;

namespace WebCrawler.Demos
{
    /// <summary>
    /// Demo: Using CefSharp (WITH JavaScript Execution)
    /// </summary>
    public class CefSharpDemo : IDisposable
    {
        private static ChromiumWebBrowser _browser;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static bool _isInitialized = false;

        public CefSharpDemo()
        {
            InitializeBrowser();
        }

        private void InitializeBrowser()
        {
            if (_isInitialized) return;

            lock (typeof(CefSharpDemo))
            {
                if (_isInitialized) return;

                Console.WriteLine("Initializing CefSharp browser...");
                _browser = new ChromiumWebBrowser("");
                var initTask = _browser.WaitForInitialLoadAsync();
                initTask.Wait(TimeSpan.FromSeconds(10));
                _isInitialized = true;
                Console.WriteLine("? CefSharp browser initialized\n");
            }
        }

        /// <summary>
        /// Load HTML using CefSharp - WITH JavaScript execution
        /// </summary>
        public async Task<List<Product>> LoadProducts(string url)
        {
            var products = new List<Product>();

            await _semaphore.WaitAsync();

            try
            {
                Console.WriteLine("\n=== CefSharp Demo ===");
                Console.WriteLine($"Loading: {url}");
                Console.WriteLine("Note: This DOES execute JavaScript!\n");

                // Load the page
                var loadResponse = await _browser.LoadUrlAsync(url);

                if (!loadResponse.Success)
                {
                    Console.WriteLine($"? Failed to load page: {loadResponse.ErrorCode}");
                    return products;
                }

                Console.WriteLine("? Page loaded");
                Console.WriteLine("? Waiting for JavaScript to execute (3 seconds)...\n");

                // Wait for JavaScript to execute
                await Task.Delay(3000);

                // Get HTML AFTER JavaScript has run
                var html = await _browser.GetSourceAsync();

                Console.WriteLine($"? HTML retrieved ({html.Length} chars)");
                Console.WriteLine("\nSearching for products...\n");

                // Parse with HtmlAgilityPack
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract ALL products (static AND dynamic)
                var productNodes = doc.DocumentNode.SelectNodes("//div[@class='product']");

                if (productNodes != null)
                {
                    foreach (var node in productNodes)
                    {
                        var nameNode = node.SelectSingleNode(".//h3");
                        var descNode = node.SelectSingleNode(".//p");
                        var priceNode = node.SelectSingleNode(".//span[@class='price']");

                        if (nameNode != null)
                        {
                            var product = new Product
                            {
                                Name = nameNode.InnerText.Trim(),
                                Description = descNode?.InnerText.Trim() ?? "",
                                Price = priceNode?.InnerText.Trim() ?? ""
                            };

                            products.Add(product);
                            
                            // Indicate if it's static or dynamic
                            var isDynamic = product.Name.Contains("Dynamic");
                            var marker = isDynamic ? "? (JS-loaded)" : "  (static)";
                            Console.WriteLine($"  {marker} {product.Name}");
                        }
                    }
                }

                // Check if loading message is gone (JS executed successfully)
                var loadingMessage = doc.DocumentNode.SelectSingleNode("//p[@class='loading']");
                if (loadingMessage == null)
                {
                    Console.WriteLine($"\n? Loading message is gone - JavaScript executed successfully!");
                }

                Console.WriteLine($"\n? Total products found: {products.Count}");
                Console.WriteLine("? Dynamic products loaded successfully!\n");

                return products;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                return products;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Demo: Show HTML after JavaScript execution
        /// </summary>
        public async Task ShowProcessedHtml(string url)
        {
            await _semaphore.WaitAsync();

            try
            {
                Console.WriteLine("\n=== Processed HTML (CefSharp View - After JS) ===\n");

                await _browser.LoadUrlAsync(url);
                await Task.Delay(3000);

                var html = await _browser.GetSourceAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var productContainer = doc.DocumentNode.SelectSingleNode("//div[@id='product-container']");

                if (productContainer != null)
                {
                    Console.WriteLine("Product Container HTML:");
                    Console.WriteLine(productContainer.InnerHtml.Substring(0, Math.Min(500, productContainer.InnerHtml.Length)));
                    Console.WriteLine("\n^ Notice: Shows actual product HTML because JS executed\n");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            // Disposed on shutdown
        }

        public static void Shutdown()
        {
            _browser?.Dispose();
            _browser = null;
            _isInitialized = false;
        }
    }
}