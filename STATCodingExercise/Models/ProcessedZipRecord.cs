using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STATCodingExercise.Models
{
    // Model to manage Zipfile log using DynamoDB table
    [DynamoDBTable("ZipFileTracker")]
    public class ProcessedZipRecord
    {
        [DynamoDBHashKey] // Primary key (Partition Key)
        public required string ZipFile { get; set; }

        //[DynamoDBRangeKey] // Secondary index to facilitate quicker querying
        [DynamoDBProperty]
        public required string LastUpdated { get; set; }

        [DynamoDBProperty]
        public bool Completed { get; set; }

        public string? Error { get; set; }
    }
}
