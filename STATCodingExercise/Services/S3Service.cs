using Amazon.S3.Model;
using Amazon.S3;
using Amazon;
using Amazon.S3.Transfer;
using System;
using STATCodingExercise.Models;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Serilog;

namespace STATCodingExercise.Services
{
    // S3Service contains all functions necessary to interact and manage zip and pdf files in S3 bucket
    public class S3Service
    {
        private readonly AmazonS3Client _s3Client;
        private readonly DynamoDBService _dBService;

        public S3Service(string accessKeyId, string secret, DynamoDBService dBService)
        {
            _s3Client = new AmazonS3Client(accessKeyId, secret, RegionEndpoint.USEast2);
            _dBService = dBService; // Needed to manage zip and pdf logs while interacting with bucket
        }

        // Get Zip files that have not beeen processed based on zip records not stored in table yet or not complete
        public async Task<Dictionary<string, ProcessedZipRecord>> GetZipFilesNotProcessedAsync(string bucketName)
        {
            var s3ZipLogs = new Dictionary<string, ProcessedZipRecord>();

            try
            {
                var request = new ListObjectsV2Request { BucketName = bucketName };
                ListObjectsV2Response response;

                Log.Information("Fetching zip files to process...");

                do
                {
                    response = await _s3Client.ListObjectsV2Async(request);
                    foreach (var obj in response.S3Objects)
                    {
                        if (!obj.Key.EndsWith(".zip")) continue;

                        var zipName = Path.GetFileNameWithoutExtension(obj.Key);
                        var zipRecord = await _dBService.IsZipProcessedAsync(zipName);

                        if (zipRecord == null || zipRecord.Completed) continue;

                        if (!s3ZipLogs.ContainsKey(obj.Key))
                        {
                            if (string.IsNullOrWhiteSpace(zipRecord.ZipFile))
                            {
                                zipRecord.ZipFile = zipName;
                                _ = await _dBService.SaveProcessedZipFileRecord(zipRecord); // if zipfile does not yet exist in table then store it.
                            }

                            s3ZipLogs[obj.Key] = zipRecord;
                            Log.Information($"- Adding {obj.Key} (Size: {obj.Size} bytes) for processing.");
                        }
                    }

                    request.ContinuationToken = response.NextContinuationToken; // works like pagination ensuring all zip files are retrived and reviewed, since it's possible for s3 bucket to contain more 1000 zip files 

                } while (response.IsTruncated);

                Log.Information("Fetching complete.\n");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}\n");
            }

            if (s3ZipLogs.Count == 0)
                Log.Information("No zip files to process.\n");

            return s3ZipLogs;
        }

        // Upload File to S3
        public async Task UploadFileAsync(string bucketName, string filePath, string s3FilePath, string contentType)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3FilePath,
                    InputStream = fileStream,
                    ContentType = contentType
                };

                var response = await _s3Client.PutObjectAsync(request);
                Log.Information($"- Uploaded {s3FilePath} successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"!!! Error uploading file: {ex.Message} !!!");
            }
        }

        // Download File from S3
        public async Task DownloadFileAsync(string bucketName, string s3FilePath, string localPath)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3FilePath
                };

                using (var response = await _s3Client.GetObjectAsync(request))
                using (var responseStream = response.ResponseStream)
                using (var fileStream = File.Create(localPath))
                {
                    await responseStream.CopyToAsync(fileStream);
                    Log.Information($"- Downloaded {s3FilePath} to {localPath}.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"!!! Error downloading file: {ex.Message} !!!");
            }
        }

        public async Task BatchDownloadFilesAsync(List<string> s3Files, string localFolder, string bucket)
        {
            var downloadTasks = new List<Task>();

            foreach (var file in s3Files)
            {
                string localFilePath = Path.Combine(localFolder, Path.GetFileName(file));
                downloadTasks.Add(DownloadFileAsync(bucket, file, localFilePath));
                Log.Information($"Downloading {file} to {localFilePath}...");
            }

            await Task.WhenAll(downloadTasks);
            Log.Information("Batch download completed.\n");
        }

        public async Task BatchUploadFilesAsync(List<string> s3Files, string localFolder, string bucket, string ContentType)
        {
            var uploadTasks = new List<Task>();

            foreach (var file in s3Files)
            {
                string localFilePath = Path.Combine(localFolder, Path.GetFileName(file));
                uploadTasks.Add(UploadFileAsync(bucket, localFilePath, file, ContentType));
                Log.Information($"Uploading {file}...");
            }

            await Task.WhenAll(uploadTasks);
            Log.Information("Batch upload completed.\n");
        }

    }
}
