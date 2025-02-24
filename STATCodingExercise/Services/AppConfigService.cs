using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace STATCodingExercise.Services
{
    //Service to setup app settings and credentials
    public class AppConfigService
    {
        public string S3AccessKey { get; }
        public string S3Secret { get; }
        public string S3Bucket { get; }

        public string DDBAccessKey { get; }
        public string DDBSecret { get; }
        public string DDBTable { get; }

        public AppConfigService()
        {
            string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            S3AccessKey = config["AWSS3:AccessKeyID"];
            S3Secret = config["AWSS3:SecretKey"];
            S3Bucket = config["AWSS3:BucketName"];

            DDBAccessKey = config["AWSDynamoDB:AccessKeyID"];
            DDBSecret = config["AWSDynamoDB:SecretKey"];
            DDBTable = config["AWSDynamoDB:TableName"];
        }
    }

}
