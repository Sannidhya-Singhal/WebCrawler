// Demos/ComparisonDemo.cs
using System;
using System.Threading.Tasks;

namespace WebCrawler.Demos
{
    /// <summary>
    /// Run both demos side-by-side for comparison
    /// </summary>
    public class ComparisonDemo
    {
        public static async Task RunComparison()
        {
            // Use file:// protocol for local HTML file
            var url = @"C:\Temp\TestPage.html";





            // ========================================
            // Part 1: HtmlAgilityPack (No JavaScript)
            // ========================================
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("PART 1: HtmlAgilityPack (Simple HTTP Download)");
            Console.WriteLine(new string('=', 60));

            var htmlAgilityDemo = new HtmlAgilityPackDemo();
            var productsWithoutJS = await htmlAgilityDemo.LoadProducts(url);
            
            Console.WriteLine("\n?? Results:");
            Console.WriteLine($"   Products found: {productsWithoutJS.Count}");
            foreach (var product in productsWithoutJS)
            {
                Console.WriteLine($"   • {product}");
            }

            await htmlAgilityDemo.ShowRawHtml(url);

            // ========================================
            // Part 2: CefSharp (With JavaScript)
            // ========================================
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("PART 2: CefSharp (Full Browser with JavaScript)");
            Console.WriteLine(new string('=', 60));

            var cefSharpDemo = new CefSharpDemo();
            var productsWithJS = await cefSharpDemo.LoadProducts(url);

            Console.WriteLine("Results:");
            Console.WriteLine($"Products found: {productsWithJS.Count}");
            foreach (var product in productsWithJS)
            {
                Console.WriteLine($"   • {product}");
            }

            await cefSharpDemo.ShowProcessedHtml(url);

            // ========================================
            // Part 3: Comparison Summary
            // ========================================
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("COMPARISON SUMMARY");
            Console.WriteLine(new string('=', 60));



            // Cleanup
            CefSharpDemo.Shutdown();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}