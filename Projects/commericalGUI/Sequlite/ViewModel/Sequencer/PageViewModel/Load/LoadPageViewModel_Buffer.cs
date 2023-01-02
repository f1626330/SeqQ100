using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public partial class LoadPageViewModel : PageViewBaseViewModel
    {
        string _Instruction_LoadBuffer = @"
                <h4><u>Load the Buffer Cartridge:</u></h4>   
                <ol> 
                    <li>Open the right side door</li>
                    <li>Raise the sippers</li>
                    <li>Remove the old cartridge</li>
                    <li>Slide in the new cartridge</li>
                    <li>Lower the sippers</li>
                    <li>Click Done</li>
                </ol>";

        public string Instruction_LoadBuffer
        {
            get
            {
                return _Instruction_LoadBuffer;
            }
        }

        private ICommand _BufferLoadedCmd = null;
        public ICommand BufferLoadedCmd
        {
            get
            {
                if (_BufferLoadedCmd == null)
                {
                    _BufferLoadedCmd = new RelayCommand(o => BufferLoaded(o), o => CanBufferLoaded);
                }
                return _BufferLoadedCmd;
            }
        }

        bool _CanBufferLoaded = true;
        public bool CanBufferLoaded
        {
            get
            {
                return _CanBufferLoaded;
            }
            set
            {
                SetProperty(ref _CanBufferLoaded, value, nameof(CanBufferLoaded), true);
            }
        }

       
        async void BufferLoaded(object o)
        {
            bool bSetBufferLoaded = false;
            try
            {
                CanBufferLoaded = false;
                CanCancelPage = false;
                ResetPageStatus(CurrentSubpageIndex + 1);
                UpdateCurrentPageStatus(LoadBufferPageStatusName_BufferLoaded, PageStatusEnum.Start);
                bSetBufferLoaded = await LoadBuffer();
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to set buffer loaded with error: {0}", ex.Message));
            }
            finally
            {
                CanBufferLoaded = true;
                
                UpdateCurrentPageStatus(LoadBufferPageStatusName_BufferLoaded, bSetBufferLoaded ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }
    }
}
