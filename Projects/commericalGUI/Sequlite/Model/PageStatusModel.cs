using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public enum PageStatusEnum
    {
        Reset,
        Start,
        Complted_Success,
        Complted_Warning,
        Complted_Error,
    }
    public class PageStatusModel : ModelBase
    {
        public PageStatusModel() 
        {
        }
        public string Name { get; set; }

        public string DisaplyName { get; set; }

        public bool? IsSuccess
        {
            get
            {
                if (Status == PageStatusEnum.Complted_Success)
                {
                    return true;
                }
                else if (Status == PageStatusEnum.Complted_Error || Status == PageStatusEnum.Complted_Warning)
                {
                    return false;
                }
                else
                {
                    return null;
                }
            }
        }

        public bool IsStarted
        {
            get
            {
                return Status == PageStatusEnum.Start;
            }
        }

        public string Message
        {
            get => _Message;
            set
            {
                _Message = value;
                OnPropertyChanged();
            }
        }

        public PageStatusEnum Status
        {
            get => _Status;
            set
            {
                _Status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSuccess));
                OnPropertyChanged(nameof(IsStarted));
            }
        }

       

        private string _Message;
        private PageStatusEnum _Status = PageStatusEnum.Reset;

       
    }
}
