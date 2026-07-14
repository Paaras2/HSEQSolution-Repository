namespace HSEQ.API.Model.RequestModels
{
    public class BaseUpdateRequestModel
    {
        public bool IsActive { get; set; }
        public Guid Key { get; set; }
    }
}
