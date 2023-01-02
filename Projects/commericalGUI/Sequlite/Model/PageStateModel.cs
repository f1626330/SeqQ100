using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public enum PageStateEnum
    {
        EnterPage,
        ExitPage,
    }

    public class PageStateModel : ModelBase
    {

        //private bool _EnterPageStatus;
        //public bool EnterPageStatus
        //{
        //    get => _EnterPageStatus;
        //    set
        //    {
        //        _EnterPageStatus = value;
        //        OnPropertyChanged();
        //    }
        //}

        //private bool _ExitPageStatus;
        //public bool ExitPageStatus
        //{
        //    get => _ExitPageStatus;
        //    set
        //    {
        //        _ExitPageStatus = value;
        //        OnPropertyChanged();
        //    }
        //}

        public PageStateEnum PageState { get; set; }
        private bool _IsPlayingAnimation;
        public bool IsPlayingAnimation
        {
            get => _IsPlayingAnimation;
            set
            {
                _IsPlayingAnimation = value;
                OnPropertyChanged();
                
            }
        }

        private bool _IsStopPlayingAnimation;
        public bool IsStopPlayingAnimation
        {
            get => _IsStopPlayingAnimation;
            set
            {
                _IsStopPlayingAnimation = value;
               
                OnPropertyChanged();
            }
        }
    }
}
