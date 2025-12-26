// Demos/HtmlAgilityPackDemo.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace WebCrawler.Demos
{
    /// <summary>
    /// Demo: Using HtmlAgilityPack (No JavaScript Execution)
    /// </summary>
    public class HtmlAgilityPackDemo
    {
        private readonly HtmlWeb _htmlWeb;

        public HtmlAgilityPackDemo()
        {
            _htmlWeb = new HtmlWeb();
        }

        /// <summary>
        /// Load HTML using HtmlAgilityPack - NO JavaScript execution
        /// </summary>
        public async Task<List<Product>> LoadProducts(string url)
        {
            var products = new List<Product>();

            try
            {
                Console.WriteLine("\n=== HtmlAgilityPack Demo ===");
                Console.WriteLine($"Loading: {url}");
                Console.WriteLine("Note: This does NOT execute JavaScript!\n");

                // Load HTML (synchronous, no JS execution)
                var doc = await Task.Run(() => _htmlWeb.Load(url));

                Console.WriteLine($"? HTML loaded ({doc.DocumentNode.OuterHtml.Length} chars)");
                Console.WriteLine("\nSearching for products...\n");

                // Extract ALL products (static and dynamic)
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
                            Console.WriteLine($"  Found: {product.Name}");
                        }
                    }
                }

                // Check for loading message (indicates JS didn't run)
                var loadingMessage = doc.DocumentNode.SelectSingleNode("//p[@class='loading']");
                if (loadingMessage != null)
                {
                    Console.WriteLine($"\n??  Found loading message: '{loadingMessage.InnerText}'");
                    Console.WriteLine("??  This means JavaScript did NOT execute!");
                }

                Console.WriteLine($"\n? Total products found: {products.Count}");
                Console.WriteLine("? Dynamic products NOT loaded (JS didn't run)\n");

                return products;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                return products;
            }
        }

        /// <summary>
        /// Demo: Show raw HTML that HtmlAgilityPack sees
        /// </summary>
        public async Task ShowRawHtml(string url)
        {
            Console.WriteLine("\n=== Raw HTML (HtmlAgilityPack View) ===\n");

            var doc = await Task.Run(() => _htmlWeb.Load(url));
            var productContainer = doc.DocumentNode.SelectSingleNode("//div[@id='product-container']");

            if (productContainer != null)
            {
                Console.WriteLine("Product Container HTML:");
                Console.WriteLine(productContainer.InnerHtml);
                Console.WriteLine("\n^ Notice: Only shows 'Loading products...' because JS didn't execute\n");
            }
        }
    }

    /// <summary>
    /// Simple product model for demo
    /// </summary>
    public class Product
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Price { get; set; }

        public override string ToString()
        {
            return $"{Name} - {Description} - {Price}";
        }
    }
}