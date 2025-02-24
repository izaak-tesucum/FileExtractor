using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Serilog;
using STATCodingExercise.Models;

namespace STATCodingExercise.Services
{
    // Service created to manage logs and interact with dynamo db tables
    public class DynamoDBService
    {
        private readonly AmazonDynamoDBClient _client;
        private readonly DynamoDBContext _context;

        public DynamoDBService(string accessKey, string secret)
        {
            _client = new AmazonDynamoDBClient(accessKey, secret, RegionEndpoint.USWest2);
            _context = new DynamoDBContext(_client);
        }

        // Check if zipfile log exists already otherwise instantiate a new log
        public async Task<ProcessedZipRecord?> IsZipProcessedAsync(string zipFileName)
        {
            try
            {
                var file = await _context.LoadAsync<ProcessedZipRecord>(zipFileName);
                return file ?? new ProcessedZipRecord { ZipFile = "", Completed = false, Error = null, LastUpdated = DateTime.UtcNow.ToString("O") }; ;
            }
            catch (AmazonDynamoDBException e)
            {
                Log.Error($"!!! Error retrieving zip record {zipFileName}: {e.Message} !!!");
                return null;
            }
        }

        // Get all pdf file logs by zipfile name
        public async Task<List<ProcessedFileRecord>> GetFileRecordsByZip(string zipName)
        {
            try
            {
                Log.Information($"Fetching file records for zipfile {zipName}...");
                return await _context.QueryAsync<ProcessedFileRecord>(zipName).GetRemainingAsync();
            }
            catch (AmazonDynamoDBException e)
            {
                Log.Error($"!!! Error retrieving file records for zip {zipName}: {e.Message} !!!");
                return new List<ProcessedFileRecord>();
            }
        }

        //Batch save list of pdf file logs
        public async Task BatchSaveProcessedFileRecords(List<ProcessedFileRecord> processedFiles)
        {
            try
            {
                if (processedFiles.Count == 0)
                {
                    Log.Information("No new files were processed.");
                    return;
                }

                var batchUpload = _context.CreateBatchWrite<ProcessedFileRecord>();
                batchUpload.AddPutItems(processedFiles);
                await batchUpload.ExecuteAsync();

                Log.Information($"Successfully updated processed files logs:");
                processedFiles.Select(f => f.FileName)
                 .ToList()
                 .ForEach(fileName => Log.Information($"- {fileName}"));
            }
            catch (AmazonDynamoDBException e)
            {
                Log.Error($"!!! Error saving file records: {e.Message} !!!");
            }
        }

        //Batch save list of zip file logs
        public async Task BatchSaveProcessedZipFileRecords(List<ProcessedZipRecord> processedZips)
        {
            try
            {
                if (processedZips.Count == 0)
                {
                    Log.Information("No zip files were processed.");
                    return;
                }

                var batchUpload = _context.CreateBatchWrite<ProcessedZipRecord>();
                batchUpload.AddPutItems(processedZips);
                await batchUpload.ExecuteAsync();

                Log.Information($"Successfully updated processed zip logs:");
                processedZips.Select(f => f.ZipFile)
                 .ToList()  
                 .ForEach(zipFile => Log.Information($"- {zipFile}"));
            }
            catch (AmazonDynamoDBException e)
            {
                Log.Error($"!!! Error saving zip records: {e.Message} !!!");
            }
        }

        //Save zip file log
        public async Task<bool> SaveProcessedZipFileRecord(ProcessedZipRecord processedZip)
        {
            try
            {
                await _context.SaveAsync(processedZip);
                Log.Information($"Successfully updated zipfile record: {processedZip.ZipFile}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"!!! Error saving zip record {processedZip.ZipFile}: {e.Message} !!!");
                return false;
            }
        }

        public async Task<bool> SaveProcessedFileRecord(ProcessedFileRecord processedFile)
        {
            try
            {
                await _context.SaveAsync(processedFile);
                Log.Information($"Successfully updated file record: {processedFile.FileName}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"!!! Error saving zip record {processedFile.ZipFile}: {e.Message} !!!");
                return false;
            }
        }
    }
}
