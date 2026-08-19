using System;

namespace HSEQ.API.Model.Dtos
{
    // خروجی سبک برای پیشنهاد خودکار هنگام تایپ - فقط همان چیزی که برای نمایش یک گزینه
    // در فهرست پیشنهادها لازم است، نه کل DocumentDto.
    public class DocumentSuggestionDto
    {
        public Guid Key { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
    }
}
