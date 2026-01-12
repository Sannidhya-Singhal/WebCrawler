using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WebCrawler.Services;

namespace WebCrawler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _selectedFilePath;
        private FileProcessingService _fileService;
        private CefSharpCrawlerService _crawlerService;

        public MainWindow()
        {
            InitializeComponent();
            _fileService = new FileProcessingService();
            
            // Initialize crawler once at startup
            _crawlerService = new CefSharpCrawlerService();
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                Title = "Select Input File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                TxtSelectedFile.Text = Path.GetFileName(_selectedFilePath);
                BtnStart.IsEnabled = true;
                Log($"File selected: {_selectedFilePath}");
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Please select an input file first.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate input counts from TextBoxes
            if (!int.TryParse(TxtGoogleCount.Text, out int googleCount) || googleCount < 1 || googleCount > 50)
            {
                MessageBox.Show("Please enter a valid number for Google Headings Count (1-50).", "Invalid Input", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtAmazonCount.Text, out int amazonCount) || amazonCount < 1 || amazonCount > 50)
            {
                MessageBox.Show("Please enter a valid number for Amazon Search Results Count (1-50).", "Invalid Input", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnStart.IsEnabled = false;
            BtnSelectFile.IsEnabled = false;
            TxtGoogleCount.IsEnabled = false;
            TxtAmazonCount.IsEnabled = false;
            ProgressBar.Value = 0;

            try
            {
                Log($"Starting crawling process...");
                Log($"Settings: Google Headings={googleCount}, Amazon Results={amazonCount}");
                
                // Read input file
                Log("Reading input file...");
                var inputData = await _fileService.ReadInputFile(_selectedFilePath);
                Log($"Loaded {inputData.Count} records");
                
                // Process each row
                for (int i = 0; i < inputData.Count; i++)
                {
                    var row = inputData[i];
                    Log($"\nProcessing row {i + 1}/{inputData.Count}");

                    var googleUrl = row.ContainsKey("search_on_google_url") ? row["search_on_google_url"]?.ToString() : null;
                    var amazonSearchUrl = row.ContainsKey("search_page_url") ? row["search_page_url"]?.ToString() : null;
                    var productUrl = row.ContainsKey("detail_page_url") ? row["detail_page_url"]?.ToString() : null;

                    // Crawl Google headings with custom count
                    if (!string.IsNullOrWhiteSpace(googleUrl))
                    {
                        Log($"  Crawling Google headings (taking {googleCount} results)...");
                        var googleHeadings = await _crawlerService.CrawlGoogleHeadings(googleUrl,googleCount);
                        var selectedHeadings = googleHeadings.Skip(2).Take(googleCount).ToList();
                        row[$"Google_Headings"] = string.Join(Environment.NewLine, selectedHeadings);
                        //Log($"  Found {googleHeadings.Count} Google headings, selected {selectedHeadings.Count}");
                    }

                    // Crawl Amazon search titles with custom count
                    if (!string.IsNullOrWhiteSpace(amazonSearchUrl))
                    {
                        Log($"  Crawling Amazon search titles (taking {amazonCount} results)...");
                        var searchTitles = await _crawlerService.CrawlAmazonSearchTitles(amazonSearchUrl, amazonCount);
                        row[$"Amazon Search Titles"] = string.Join(Environment.NewLine, searchTitles);
                        Log($"  Found {searchTitles.Count} Amazon titles");
                    }

                    // Crawl Amazon product page
                    if (!string.IsNullOrWhiteSpace(productUrl))
                    {
                        Log("  Crawling Amazon product page...");
                        var (overview, aboutItem) = await _crawlerService.CrawlAmazonProductPageV1(productUrl);

                        // Combine all overview items into a single column
                        if (overview != null && overview.Any())
                        {
                            var overviewLines = overview.Select(kvp => $"{kvp.Key}: {kvp.Value}");
                            row["Product Overview"] = string.Join(Environment.NewLine, overviewLines);
                        }
                        else
                        {
                            row["Product Overview"] = "";
                        }
                        
                        // Store each item on a new line in the same cell
                        if (aboutItem != null && aboutItem.Any())
                        {
                            row["About_This_Item"] = string.Join(Environment.NewLine, aboutItem.Take(10));
                        }
                        else
                        {
                            row["About_This_Item"] = "";
                        }
                        Log($"  Found {overview.Count} product attributes");
                    }

                    // Update progress
                    ProgressBar.Value = ((i + 1) * 100.0) / inputData.Count;
                }

                // Export results
                Log("\nExporting results...");
                var outputPath = Path.Combine(
                    Path.GetDirectoryName(_selectedFilePath),
                    $"{Path.GetFileNameWithoutExtension(_selectedFilePath)}_crawled.xlsx"
                );
                
                await _fileService.ExportToExcel(inputData, outputPath);
                Log($"Results exported to: {outputPath}");
                
                MessageBox.Show($"Crawling completed successfully!\n\nOutput file: {outputPath}", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"\nERROR: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnSelectFile.IsEnabled = true;
                TxtGoogleCount.IsEnabled = true;
                TxtAmazonCount.IsEnabled = true;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Clean up when window is closing
            CefSharpCrawlerService.Shutdown();
            base.OnClosing(e);
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
                TxtLog.ScrollToEnd();
            });
        }
    }
}