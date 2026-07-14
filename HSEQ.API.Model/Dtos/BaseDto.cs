namespace HSEQ.API.Model.Dtos
{
    public class BaseDto
    {
        public Guid Key { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
