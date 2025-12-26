// Services/FileProcessingService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml;
using System.Globalization;

namespace WebCrawler.Services
{
    public class FileProcessingService
    {
        public async Task<List<Dictionary<string, object>>> ReadInputFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            if (extension == ".csv")
                return await ReadCsvFile(filePath);
            else if (extension == ".xlsx")
                return await ReadExcelFile(filePath);
            else
                throw new NotSupportedException("Only CSV and XLSX files are supported");
        }

        private async Task<List<Dictionary<string, object>>> ReadCsvFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();
            
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            
            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            
            while (await csv.ReadAsync())
            {
                var record = new Dictionary<string, object>();
                foreach (var header in headers)
                {
                    record[header] = csv.GetField(header);
                }
                records.Add(record);
            }
            
            return records;
        }

        private async Task<List<Dictionary<string, object>>> ReadExcelFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            
            var headers = new List<string>();
            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
            {
                headers.Add(worksheet.Cells[1, col].Text);
            }
            
            for (int row = 2; row <= worksheet.Dimension.Rows; row++)
            {
                var record = new Dictionary<string, object>();
                for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                {
                    record[headers[col - 1]] = worksheet.Cells[row, col].Text;
                }
                records.Add(record);
            }
            
            return records;
        }

        public async Task ExportToExcel(List<Dictionary<string, object>> data, string outputPath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Results");
            
            if (data.Count == 0) return;
            
            // Write headers
            var headers = data[0].Keys.ToList();
            for (int i = 0; i < headers.Count; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }
            
            // Write data
            for (int row = 0; row < data.Count; row++)
            {
                for (int col = 0; col < headers.Count; col++)
                {
                    var value = data[row][headers[col]];
                    worksheet.Cells[row + 2, col + 1].Value = value?.ToString();
                }
            }
            
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            
            await package.SaveAsAsync(new FileInfo(outputPath));
        }
    }
}