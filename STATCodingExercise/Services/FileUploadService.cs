using Serilog;
using STATCodingExercise.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STATCodingExercise.Services
{
    public class FileUploadService
    {
        private readonly S3Service _s3Service;
        private readonly DynamoDBService _dynamoDbService;
        private readonly string _s3BucketName;
        private readonly string _s3FolderPath;

        // Service handles the process of uploading pdf files by po number and updating file logs
        public FileUploadService(S3Service s3Service, DynamoDBService dynamoDbService, string bucket, string s3folderPath)
        {
            _s3Service = s3Service;
            _dynamoDbService = dynamoDbService;
            _s3BucketName = bucket;
            _s3FolderPath = s3folderPath;        
        }

        public async Task ProcessAndUploadFiles(Dictionary<string, List<string>> poToAttachmentMapper, string folder, List<ProcessedFileRecord> unProcessedFileRecords)
        {
            var fileRecordDictionary = unProcessedFileRecords.ToDictionary(r => r.FileName);
            var semaphore = new SemaphoreSlim(5);

            Log.Information("Starting Upload Process...");
            foreach (var poNum in poToAttachmentMapper.Keys)
            {
                if (poToAttachmentMapper[poNum].Count == 0)
                {
                    Log.Information($"No pdf attachments available for PO Number {poNum}");
                    continue;
                }

                var completedFilesByPO = unProcessedFileRecords.Where(r => r.PONumber == poNum && r.Completed).Select(r => r.FileName).ToList();

                await Task.WhenAll(poToAttachmentMapper[poNum].Select(async file =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        if (!completedFilesByPO.Contains(file))
                        {
                            if (!fileRecordDictionary.TryGetValue(file, out var unProcessedFileRecord))
                            {
                                Log.Information($"Creating log record for {file}...");
                                unProcessedFileRecord = new ProcessedFileRecord
                                {
                                    ZipFile = new DirectoryInfo(folder).Name,
                                    FileName = file,
                                    ProcessedDate = DateTime.UtcNow.ToString("O"),
                                    Status = FileStatus.Processing,
                                    Completed = false,
                                };
                                await _dynamoDbService.SaveProcessedFileRecord(unProcessedFileRecord);
                                unProcessedFileRecords.Add(unProcessedFileRecord);
                            }

                            string err = ""; // strings for logging any errors and warning on a file in file log.
                            string warning = "";
                            var s3Path = Path.Combine(_s3FolderPath, poNum, file).Replace('\\', '/').Replace(string.Format("/{0}", _s3FolderPath), _s3FolderPath);
                            try
                            {
                                string localPath = Path.Combine(folder, file);
                                if (!File.Exists(localPath))
                                {
                                    unProcessedFileRecord.Status = FileStatus.Missing;
                                    throw new FileNotFoundException(localPath);
                                }

                                if (!string.IsNullOrWhiteSpace(poNum)) // po number cannot be empty in order to upload file.
                                    await _s3Service.UploadFileAsync(_s3BucketName, localPath, s3Path, "application/pdf");
                                else
                                {
                                    unProcessedFileRecord.Status = FileStatus.InvalidPO;
                                    warning = $"PO Number does not exist for this file: {file}";
                                    Log.Warning(warning);
                                }
                            }
                            catch (FileNotFoundException fe)
                            {
                                warning = fe.ToString();
                                Log.Warning(warning);
                            }
                            catch (Exception e)
                            {
                                err = e.ToString();
                                Log.Error(err);
                            }
                            finally  // update log after processing attachment
                            {
                                unProcessedFileRecord.PONumber = poNum;
                                unProcessedFileRecord.S3Path = s3Path;
                                unProcessedFileRecord.ProcessedDate = DateTime.UtcNow.ToString("O");
                                unProcessedFileRecord.Warning = !string.IsNullOrWhiteSpace(warning) ? warning : null;
                                unProcessedFileRecord.Error = !string.IsNullOrWhiteSpace(err) ? err : null;
                                unProcessedFileRecord.Completed = string.IsNullOrWhiteSpace(err);
                                if (unProcessedFileRecord.Status == FileStatus.Processing)
                                {
                                    unProcessedFileRecord.Status = !string.IsNullOrWhiteSpace(err) ? FileStatus.Error : FileStatus.Successful;
                                }
                                await _dynamoDbService.SaveProcessedFileRecord(unProcessedFileRecord);
                                Log.Information("\n");
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));

                //await Task.WhenAll(fileTasks);
                //await _dynamoDbService.BatchSaveProcessedFileRecords(unProcessedFileRecords.Where(r => poToAttachmentMapper[poNum].Contains(r.FileName)).ToList()); // batch update logs for attachment files.
            }
            Log.Information("Upload Process Complete.");
        }
    }
}
