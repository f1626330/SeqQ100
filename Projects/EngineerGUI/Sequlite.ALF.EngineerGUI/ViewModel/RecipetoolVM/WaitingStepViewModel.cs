using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class WaitingStepViewModel : StepsTreeViewModel
    {
        double _WaitingTime = RecipeStepDefaultSettings.WaitingTime;
        bool _ResetPump = true;
        #region Constructor
        public WaitingStepViewModel()
        {

        }
        public WaitingStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            WaitingStep step = content.Step as WaitingStep;
            if (step != null)
            {
                WaitingTime = step.Time;
                ResetPump = step.ResetPump;
            }
        }
        #endregion Constructor

        public double WaitingTime
        {
            get { return _WaitingTime; }
            set
            {
                if (_WaitingTime != value)
                {
                    _WaitingTime = value;
                    RaisePropertyChanged(nameof(WaitingTime));
                }
            }
        }

        public bool ResetPump
        {
            get { return _ResetPump; }
            set
            {
                if (_ResetPump != value)
                {
                    _ResetPump = value;
                    RaisePropertyChanged(nameof(ResetPump));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            WaitingStepViewModel clonedVm = new WaitingStepViewModel()
            {
                WaitingTime = this.WaitingTime,
                ResetPump = this.ResetPump
            };
            return clonedVm;
        }
    }
}
