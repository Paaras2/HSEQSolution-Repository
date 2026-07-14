namespace HSEQ.Domain.Common
{
    public abstract class BaseEntity: IEntity, ITimeModification
    {
        public Guid Key { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ModifiedDate { get; set; }

    }



    //Define Marker Interface:
    public interface IEntity
    {

    }
    public interface ITimeModification
    {
        DateTime CreatedTime { get; set; }
        DateTime? ModifiedDate { get; set; }
    }
}
