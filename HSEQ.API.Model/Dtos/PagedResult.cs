using System;
using System.Collections.Generic;

namespace HSEQ.API.Model.Dtos
{
    public class PagedResult
    {
        public List<DocumentDto> Items { get; set; } = [];
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
