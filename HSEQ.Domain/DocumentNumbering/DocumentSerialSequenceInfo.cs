namespace HSEQ.Domain.DocumentNumbering
{
    // Identifies the SQL Server SEQUENCE object backing the global Document Serial Number (SSS).
    // Referenced from both ApplicationDbContext (registration) and the numbering repository
    // (NEXT VALUE FOR allocation) so the name/schema exist in exactly one place.
    public static class DocumentSerialSequenceInfo
    {
        public const string Name = "DocumentSerialSequence";
        public const string Schema = "dbo";
        public const string QualifiedName = Schema + "." + Name;
    }
}
