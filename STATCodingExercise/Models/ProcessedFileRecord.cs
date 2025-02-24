using Amazon.DynamoDBv2.DataModel;

namespace STATCodingExercise.Models
{
    //Model to Manage pdf logs that have been or is being processed on DynamoDB table
    [DynamoDBTable("ProcessedFilesTracker")]
    public class ProcessedFileRecord
    {
        [DynamoDBHashKey]
        public required string ZipFile { get; set; }

        [DynamoDBRangeKey]
        public required string FileName { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey]
        public required string ProcessedDate { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey]
        public required string Status { get; set; }

        [DynamoDBProperty]
        public bool Completed { get; set; }

        public string? PONumber { get; set; }

        public string? S3Path { get; set; }

        public string? Error { get; set; }

        public string? Warning { get; set; }
    }

    // Used to maintain the different statuses a pdf file may be under
    public class FileStatus
    {
        public const string Processing = "processing";
        public const string Successful = "successful";
        public const string Error = "error";
        public const string Missing = "missing file";
        public const string InvalidPO = "invalid po";
    }
}
