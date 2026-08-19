using HSEQ.Common;
using System;
using System.Text.RegularExpressions;

namespace HSEQ.Domain.DocumentNumbering
{
    // Value object for the "ستاد" (Headquarters) Document Number:
    //
    //     O + AA + DD - SSS - R      e.g. "ASYFM-008-A"
    //
    // Unlike the Project scheme (HSEQ.Domain.DocumentNumbering.DocumentCode), there is no
    // Project prefix, and the serial/revision are dash-separated instead of concatenated -
    // this mirrors exactly how the company's legacy Excel register wrote these numbers.
    // Revision is always the plain A-Z form (never the compound project scheme), since a
    // Headquarters document is never project-related.
    //
    // Pure structural validation only - no database access, no master-data lookups.
    public sealed class HeadquartersDocumentCode
    {
        private const string CodeCharPattern = "^[A-Z0-9]+$";

        public string ManagementCode { get; }
        public string ActivityCode { get; }
        public string DocumentTypeCode { get; }
        public int SerialNumber { get; }
        public RevisionCode Revision { get; }

        // O+AA+DD, e.g. "ASYFM" - the counter key used by HeadquartersCodeCounter.
        public string Code5 { get; }

        // Code5-SSS, without the revision suffix.
        public string BaseCode { get; }

        // The complete, final Document Number, including revision.
        public string Value { get; }

        private HeadquartersDocumentCode(
            string managementCode,
            string activityCode,
            string documentTypeCode,
            int serialNumber,
            RevisionCode revision,
            string code5,
            string baseCode,
            string value)
        {
            ManagementCode = managementCode;
            ActivityCode = activityCode;
            DocumentTypeCode = documentTypeCode;
            SerialNumber = serialNumber;
            Revision = revision;
            Code5 = code5;
            BaseCode = baseCode;
            Value = value;
        }

        public static HeadquartersDocumentCode Create(
            string managementCode,
            string activityCode,
            string documentTypeCode,
            int serialNumber,
            RevisionCode revision)
        {
            if (revision is null)
                throw new ArgumentNullException(nameof(revision));

            if (revision.ContentRevision is not null)
                throw new InvalidDocumentCodeException(
                    "Headquarters documents cannot use the compound project revision scheme.");

            var validManagementCode = ValidateExact(managementCode, 1, "Organizational Management Code");
            var validActivityCode = ValidateExact(activityCode, 2, "Organizational Activity Code");
            var validDocumentTypeCode = ValidateExact(documentTypeCode, 2, "Document Type Code");

            if (serialNumber < 1 || serialNumber > 999)
                throw new InvalidDocumentCodeException($"Serial number must be between 001 and 999, got '{serialNumber}'.");

            var code5 = $"{validManagementCode}{validActivityCode}{validDocumentTypeCode}";
            var serialFormatted = serialNumber.ToString("000");
            var baseCode = $"{code5}-{serialFormatted}";
            var value = $"{baseCode}-{revision.Format()}";

            return new HeadquartersDocumentCode(
                validManagementCode, validActivityCode, validDocumentTypeCode,
                serialNumber, revision, code5, baseCode, value);
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
