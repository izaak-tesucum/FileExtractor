using Serilog;
using STATCodingExercise.Models;
using STATCodingExercise.Services;
using System.IO.Compression;

Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

Log.Warning("Application started");

AppConfigService appConfig = new AppConfigService();

DynamoDBService dynamoDB = new DynamoDBService(appConfig.DDBAccessKey, appConfig.DDBSecret);

S3Service s3Service = new S3Service(appConfig.S3AccessKey, appConfig.S3Secret, dynamoDB);

string s3Bucket = appConfig.S3Bucket;

string tempDownloadPath = Path.Combine(Path.GetTempPath(), "Extracted");

var fileObjsWithLogs = await s3Service.GetZipFilesNotProcessedAsync(s3Bucket); // returns dictionary where key is zipfile name and value is zipfile log record
var unProcessedZipFiles = fileObjsWithLogs.Keys.ToList();
List<List<string>> zipFileChunks = unProcessedZipFiles
            .Select((value, index) => new { Index = index, Value = value })
            .GroupBy(x => x.Index / 5)
            .Select(g => g.Select(x => x.Value).ToList())
            .ToList();  //Convert list of zipfiles into chunks of 5 so that 5 zip files will be downloaded at a time
                        //rather than trying to download a bunch of files in one round

FileProcessingService processingService = new(s3Service, tempDownloadPath);

FileUploadService fileUploadService = new(s3Service, dynamoDB, s3Bucket, "by-po");

foreach (var zipChunk in zipFileChunks)
{
    try
    {
        await s3Service.BatchDownloadFilesAsync(zipChunk, tempDownloadPath, s3Bucket);

        List<string> extractedFolders = await processingService.ExtractZipFilesAsync(zipChunk, s3Bucket);

        var updateTasks = extractedFolders.Select(async folder =>
        {
            string zipName = new DirectoryInfo(folder).Name;

            Dictionary<string, List<string>> poToAttachmentMapper = FileProcessingService.ParseCSVFiles(folder);
            if (poToAttachmentMapper.Count > 0)
            {
                List<ProcessedFileRecord> filesToProcessRecords = await dynamoDB.GetFileRecordsByZip(zipName);

                await fileUploadService.ProcessAndUploadFiles(poToAttachmentMapper, folder, filesToProcessRecords);

                if (fileObjsWithLogs.TryGetValue(string.Format("{0}.zip", zipName), out ProcessedZipRecord? zipRecord))
                {
                    zipRecord.Completed = filesToProcessRecords.All(r => r.Completed == true);
                    await dynamoDB.SaveProcessedZipFileRecord(zipRecord);
                }
            }

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        });

        await Task.WhenAll(updateTasks);
    }
    catch(Exception e)
    {
        Log.Error(e.Message);
    }
}
Log.Information("\n");
Log.CloseAndFlush();

