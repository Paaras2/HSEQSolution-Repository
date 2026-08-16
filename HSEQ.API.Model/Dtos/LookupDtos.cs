using System;

namespace HSEQ.API.Model.Dtos
{
    // Read-only lookups for populating Document form dropdowns. Deliberately
    // separate from the full entity DTOs - these only carry what a picker needs.
    public class ProjectLookupDto
    {
        public Guid Key { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public bool IsProjectRelated { get; set; }
    }

    public class OrganizationalManagementLookupDto
    {
        public Guid Key { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
    }

    public class OrganizationalActivityLookupDto
    {
        public Guid Key { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public Guid OrganizationalManagementId { get; set; }
    }

    public class DocumentTypeLookupDto
    {
        public Guid Key { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
    }
}
