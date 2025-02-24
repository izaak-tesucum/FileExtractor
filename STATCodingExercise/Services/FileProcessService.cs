using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Serilog;
using STATCodingExercise.Models;

namespace STATCodingExercise.Services
{
    // Service created to handle the downloading and extraction of zip files and map po number to pdf file by parsing csv file
    public class FileProcessingService
    {
        private readonly S3Service _s3Service;
        private readonly string _tempDownloadPath;

        public FileProcessingService(S3Service s3Service, string tempDownloadPath)
        {
            _s3Service = s3Service;
            _tempDownloadPath = tempDownloadPath;
            Directory.CreateDirectory(_tempDownloadPath);
        }

        public async Task<List<string>> ExtractZipFilesAsync(List<string> zipFiles, string bucket)
        {
            List<string> extractedFolders = new();

            var extractTasks = zipFiles.Select(async zipFile => //Extract zip folder and delete once done, then add new folder to extracted folders list
            {
                string zipFilePath = Path.Combine(_tempDownloadPath, Path.GetFileName(zipFile));

                string extractedFolder = Path.Combine(_tempDownloadPath, Path.GetFileNameWithoutExtension(zipFile));

                try
                {
                    if (Directory.Exists(extractedFolder))
                    {
                        Directory.Delete(extractedFolder, true);
                    }
                    Directory.CreateDirectory(extractedFolder);

                    Log.Information($"Extracting {zipFilePath} to {extractedFolder}...");
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, extractedFolder));
                    extractedFolders.Add(extractedFolder);
                    Log.Information("Extraction Complete.\n");

                    if (File.Exists(zipFilePath))
                        File.Delete(zipFilePath);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error extracting files from zipfile {zipFile}:\r\n{ex.Message}");
                }
            });

            await Task.WhenAll(extractTasks);

            return extractedFolders;
        }

        public static Dictionary<string, List<string>> ParseCSVFiles(string folderPath)
        {
            var poToAttachmentMapper = new Dictionary<string, List<string>>();
            foreach (var csvFile in Directory.GetFiles(folderPath, "*.csv")) // Assuming there could be more than one csv file in a folder
            {
                try
                {
                    Log.Information($"Parsing data from {csvFile} and mapping POs to pdf files...");
                    using var reader = new StreamReader(csvFile);
                    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "~" });
                    csv.Context.RegisterClassMap<CSVDeductionClassMap>();

                    var rows = csv.GetRecords<CSVDeduction>();
                    foreach (var row in rows)
                    {
                        if (row.PONumber.Trim() == "")
                        {
                            Log.Warning($"PO Number does not exist in file {Path.GetFileName(csvFile)} for row data {JsonConvert.SerializeObject(row, Formatting.Indented)}");
                        }

                        if (row.AttachmentStringList.Where(s => s.EndsWith(".pdf")).Any()) // looking strictly for pdf files in attachemnt list
                        {
                            if (!poToAttachmentMapper.ContainsKey(row.PONumber))
                                poToAttachmentMapper.Add(row.PONumber, new()); //create empty list in case more than one attachment list belongs to a PO number
                            poToAttachmentMapper[row.PONumber].AddRange(row.AttachmentStringList.Where(s => s.EndsWith(".pdf")).ToList());
                        }
                    }
                    Log.Information($"Parsing Complete.\n");
                }
                catch (Exception ex) {
                    Log.Error($"Error while attempting to parse {csvFile}: \r\n{ex.Message}");
                }
            }
            return poToAttachmentMapper;
        }
    }

}
