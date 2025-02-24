using CsvHelper.Configuration;

namespace STATCodingExercise.Models
{
    //Model used for parsing csv rows.  Most fields are nullable since most fields can be empty in file.
    public class CSVDeduction
    {
        public int? Id { get; set; }

        public int? ClaimNumber { get; set; }

        public DateTime? ClaimDate { get; set; }

        public double? OpenAmount { get; set; }

        public double? OriginalAmount { get; set; }

        public string? Status { get; set; }

        public string? CustomerName { get; set; }

        public string? ARReasonCode { get; set; }

        public string? CustomerReasonCode { get; set; }

        public required string AttachmentList { get; set; } = string.Empty;

        // Convert attachment list string into a list of strings containing file names
        public List<string> AttachmentStringList => AttachmentList?.Split(',')?.Where(s => s.Trim() != "")?.Select(s => Path.GetFileName(s.Trim()))?.ToList() ?? new List<string>();

        public string? CheckNumber { get; set; }

        public DateTime? CheckDate { get; set; }

        public string? Comments { get; set; }

        public int? DaysOutstanding { get; set; }

        public int? Division { get; set; }

        public required string PONumber { get; set; } = string.Empty;

        public string? Brand { get; set; }

        public string? MergeStatus { get; set; }

        public double? UnresolvedAmount { get; set; }

        public string? DocumentType { get; set; }

        public DateTime? DocumentDate { get; set; }

        public string? OriginalCustomer { get; set; }

        public string? Location { get; set; }

        public string? CustomerLocation { get; set; }
        
        public DateTime? CreateDate { get; set; }

        public string? LoadId { get; set; }

        public string? CarrierName { get; set; }

        public string? InvoiceStoreNumber { get; set; }
    }

    public class CSVDeductionClassMap : ClassMap<CSVDeduction>
    {
        public CSVDeductionClassMap()
        {
            Map(m => m.Id).Name("Id");
            Map(m => m.ClaimNumber).Name("Claim Number");
            Map(m => m.ClaimDate).Name("Claim Date");
            Map(m => m.OpenAmount).Name("Open Amount");
            Map(m => m.OriginalAmount).Name("Original Amount");
            Map(m => m.Status).Name("Status");
            Map(m => m.CustomerName).Name("Customer Name");
            Map(m => m.ARReasonCode).Name("AR Reason Code");
            Map(m => m.CustomerReasonCode).Name("Customer Reason Code");
            Map(m => m.AttachmentList).Name("Attachment List");
            Map(m => m.CheckNumber).Name("Check Number");
            Map(m => m.CheckDate).Name("Check Date");
            Map(m => m.Comments).Name("Comments");
            Map(m => m.DaysOutstanding).Name("Days Outstanding");
            Map(m => m.Division).Name("Division");
            Map(m => m.PONumber).Name("PO Number");
            Map(m => m.Brand).Name("Brand");
            Map(m => m.MergeStatus).Name("Merge Status");
            Map(m => m.UnresolvedAmount).Name("Unresolved Amount");
            Map(m => m.DocumentType).Name("Document Type");
            Map(m => m.DocumentDate).Name("Document Date");
            Map(m => m.OriginalCustomer).Name("Original Customer");
            Map(m => m.Location).Name("Location");
            Map(m => m.CustomerLocation).Name("Customer Location");
            Map(m => m.CreateDate).Name("Create Date");
            Map(m => m.LoadId).Name("Load Id");
            Map(m => m.CarrierName).Name("Carrier Name");
            Map(m => m.InvoiceStoreNumber).Name("Invoice Store Number");
        }
    }

}
