using HSEQ.Common;
using System;
using System.Text.RegularExpressions;

namespace HSEQ.Domain.DocumentNumbering
{
    // Value object for the full Document Number:
    //
    //     PPPP + O + AA + DD + SSS + R
    //
    // Base code (PPPP+O+AA+DD+SSS) is always 12 characters: Project(4) + Organizational
    // Management(1) + Organizational Activity(2) + Document Type(2) + Serial(3).
    //
    // Pure structural validation only - no database access, no master-data lookups.
    public sealed class DocumentCode
    {
        private const string CodeCharPattern = "^[A-Z0-9]+$";

        public string ProjectCode { get; }
        public string ManagementCode { get; }
        public string ActivityCode { get; }
        public string DocumentTypeCode { get; }
        public int SerialNumber { get; }
        public RevisionCode Revision { get; }

        // PPPP+O+AA+DD+SSS, without the revision suffix.
        public string BaseCode { get; }

        // The complete, final Document Number, including revision.
        public string Value { get; }

        private DocumentCode(
            string projectCode,
            string managementCode,
            string activityCode,
            string documentTypeCode,
            int serialNumber,
            RevisionCode revision,
            string baseCode,
            string value)
        {
            ProjectCode = projectCode;
            ManagementCode = managementCode;
            ActivityCode = activityCode;
            DocumentTypeCode = documentTypeCode;
            SerialNumber = serialNumber;
            Revision = revision;
            BaseCode = baseCode;
            Value = value;
        }

        public static DocumentCode Create(
            string projectCode,
            string managementCode,
            string activityCode,
            string documentTypeCode,
            int serialNumber,
            RevisionCode revision)
        {
            if (revision is null)
                throw new ArgumentNullException(nameof(revision));

            var validProjectCode = ValidateExact(projectCode, 4, "Project Code");
            var validManagementCode = ValidateExact(managementCode, 1, "Organizational Management Code");
            var validActivityCode = ValidateExact(activityCode, 2, "Organizational Activity Code");
            var validDocumentTypeCode = ValidateExact(documentTypeCode, 2, "Document Type Code");

            if (serialNumber < 1 || serialNumber > 999)
                throw new InvalidDocumentCodeException($"Serial number must be between 001 and 999, got '{serialNumber}'.");

            var serialFormatted = serialNumber.ToString("000");
            var baseCode = $"{validProjectCode}{validManagementCode}{validActivityCode}{validDocumentTypeCode}{serialFormatted}";
            var value = $"{baseCode}{revision.Format()}";

            return new DocumentCode(
                validProjectCode, validManagementCode, validActivityCode, validDocumentTypeCode,
                serialNumber, revision, baseCode, value);
        }

        private static string ValidateExact(string value, int exactLength, string componentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDocumentCodeException($"{componentName} is required.");

            var normalized = value.Trim().ToUpperInvariant();

            if (normalized.Length != exactLength)
                throw new InvalidDocumentCodeException(
                    $"{componentName} must be exactly {exactLength} character(s); got '{value}' ({normalized.Length}).");

            if (!Regex.IsMatch(normalized, CodeCharPattern))
                throw new InvalidDocumentCodeException(
                    $"{componentName} may only contain uppercase letters and digits (A-Z, 0-9); got '{value}'.");

            return normalized;
        }

        public override string ToString() => Value;
    }
}