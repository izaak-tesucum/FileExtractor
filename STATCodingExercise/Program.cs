using Serilog;
using STATCodingExercise.Models;
using STATCodingExercise.Services;
using System.IO.Compression;

Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

try
{
    Log.Information("Application started");

    AppConfigService appConfig = new AppConfigService();
    DynamoDBService dynamoDB = new DynamoDBService(appConfig.DDBAccessKey, appConfig.DDBSecret);
    S3Service s3Service = new S3Service(appConfig.S3AccessKey, appConfig.S3Secret, dynamoDB);
    
    string s3Bucket = appConfig.S3Bucket;
    string tempDownloadPath = Path.Combine(Path.GetTempPath(), "Extracted");
    
    // Ensure temp directory exists and is empty
    if (Directory.Exists(tempDownloadPath))
    {
        Directory.Delete(tempDownloadPath, true);
    }
    Directory.CreateDirectory(tempDownloadPath);

    try
    {
        var fileObjsWithLogs = await s3Service.GetZipFilesNotProcessedAsync(s3Bucket);
        var unProcessedZipFiles = fileObjsWithLogs.Keys.ToList();
        
        if (!unProcessedZipFiles.Any())
        {
            Log.Information("No unprocessed zip files found.");
            return;
        }

        List<List<string>> zipFileChunks = unProcessedZipFiles
                    .Select((value, index) => new { Index = index, Value = value })
                    .GroupBy(x => x.Index / 5)
                    .Select(g => g.Select(x => x.Value).ToList())
                    .ToList();

        FileProcessingService processingService = new(s3Service, tempDownloadPath);
        FileUploadService fileUploadService = new(s3Service, dynamoDB, s3Bucket, "by-po");

        foreach (var zipChunk in zipFileChunks)
        {
            try
            {
                Log.Information($"Processing chunk of {zipChunk.Count} files");
                await s3Service.BatchDownloadFilesAsync(zipChunk, tempDownloadPath, s3Bucket);

                List<string> extractedFolders = await processingService.ExtractZipFilesAsync(zipChunk, s3Bucket);

                var updateTasks = extractedFolders.Select(async folder =>
                {
                    try
                    {
                        string zipName = new DirectoryInfo(folder).Name;
                        Log.Information($"Processing folder: {zipName}");

                        Dictionary<string, List<string>> poToAttachmentMapper = FileProcessingService.ParseCSVFiles(folder);
                        if (poToAttachmentMapper.Count > 0)
                        {
                            List<ProcessedFileRecord> filesToProcessRecords = await dynamoDB.GetFileRecordsByZip(zipName);
                            await fileUploadService.ProcessAndUploadFiles(poToAttachmentMapper, folder, filesToProcessRecords);

                            if (fileObjsWithLogs.TryGetValue($"{zipName}.zip", out ProcessedZipRecord? zipRecord))
                            {
                                zipRecord.Completed = filesToProcessRecords.All(r => r.Completed == true);
                                await dynamoDB.SaveProcessedZipFileRecord(zipRecord);
                                Log.Information($"Completed processing {zipName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error processing folder {folder}");
                        throw;
                    }
                    finally
                    {
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, true);
                        }
                    }
                });

                await Task.WhenAll(updateTasks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing zip chunk");
                // Continue with next chunk instead of stopping the entire process
                continue;
            }
        }
    }
    finally
    {
        // Cleanup temp directory
        if (Directory.Exists(tempDownloadPath))
        {
            Directory.Delete(tempDownloadPath, true);
        }
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

