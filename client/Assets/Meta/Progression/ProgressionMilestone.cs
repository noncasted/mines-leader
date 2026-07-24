using System;
using Internal;

namespace Meta
{
    public interface IProgressionMilestone
    {
        int Required { get; }
        IViewableProperty<ProgressionMilestoneStatus> Status { get; }
        IViewableProperty<Guid> BoxId { get; }
    }

    public class ProgressionMilestone : IProgressionMilestone
    {
        public ProgressionMilestone(int required, ProgressionMilestoneStatus initialStatus)
        {
            Required = required;
            _status = new ViewableProperty<ProgressionMilestoneStatus>(initialStatus);
            _boxId = new ViewableProperty<Guid>(Guid.Empty);
        }
        
        private readonly ViewableProperty<ProgressionMilestoneStatus> _status;
        private readonly ViewableProperty<Guid> _boxId;
        
        public int Required { get; }
        public IViewableProperty<ProgressionMilestoneStatus> Status => _status;
        public IViewableProperty<Guid> BoxId => _boxId;

        internal void SetStatus(ProgressionMilestoneStatus status)
        {
            _status.Set(status);
        }

        internal void SetBoxId(Guid boxId)
        {
            _boxId.Set(boxId);
        }
    }
}
