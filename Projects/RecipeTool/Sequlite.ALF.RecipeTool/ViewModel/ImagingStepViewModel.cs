using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class ImagingStepViewModel : StepsTreeViewModel
    {
        #region Private Fields
        bool _IsAutoFocusOn = RecipeStepDefaultSettings.IsAutoFocusOn;
        int _SelectedRegionIndex;
        String _Lane = "";
        String _X = "";
        String _Y = "";
        ImagingRegionViewModel _SelectedAddedRegion;
        double _NewRefFocusPos;
        string _NewRefFocusName;
        #endregion Private Fields

        #region Public Properties
        public ImageSettingsViewModel NewImagingSetting { get; }
        public bool IsAutoFocusOn
        {
            get { return _IsAutoFocusOn; }
            set
            {
                if (_IsAutoFocusOn != value)
                {
                    _IsAutoFocusOn = value;
                    RaisePropertyChanged(nameof(IsAutoFocusOn));
                }
            }
        }
        public List<int> RegionIndexOptions { get; }
        public int SelectedRegionIndex
        {
            get { return _SelectedRegionIndex; }
            set
            {
                if (_SelectedRegionIndex != value)
                {
                    _SelectedRegionIndex = value;
                    RaisePropertyChanged(nameof(SelectedRegionIndex));
                }
            }
        }
        public String Lane
        {
            get { return _Lane; }
            set
            {
                if (_Lane != value)
                {
                    _Lane = value;
                    RaisePropertyChanged(nameof(Lane));
                }
            }
        }
        public String X
        {
            get { return _X; }
            set
            {
                if (_X != value)
                {
                    _X = value;
                    RaisePropertyChanged(nameof(X));
                }
            }
        }
        public String Y
        {
            get { return _Y; }
            set
            {
                if (_Y != value)
                {
                    _Y = value;
                    RaisePropertyChanged(nameof(_Y));
                }
            }
        }
        public ImagingRegionViewModel SelectedAddedRegion
        {
            get { return _SelectedAddedRegion; }
            set
            {
                if (_SelectedAddedRegion != value)
                {
                    _SelectedAddedRegion = value;
                    RaisePropertyChanged(nameof(SelectedAddedRegion));
                }
            }
        }

        public string NewFocusName
        {
            get { return _NewRefFocusName; }
            set
            {
                if (_NewRefFocusName != value)
                {
                    _NewRefFocusName = value;
                    RaisePropertyChanged(nameof(NewFocusName));
                }
            }
        }
        public double NewFocusPos
        {
            get { return _NewRefFocusPos; }
            set
            {
                if (_NewRefFocusPos != value)
                {
                    _NewRefFocusPos = value;
                    RaisePropertyChanged(nameof(NewFocusPos));
                }
            }
        }

        public ObservableCollection<ImagingRegionViewModel> AddedRegions { get; set; }
        #endregion Public Properties

        #region Constructor
        public ImagingStepViewModel()
        {
            NewImagingSetting = new ImageSettingsViewModel();

            RegionIndexOptions = new List<int>();
            for (int i = 1; i < 41; i++)
            {
                RegionIndexOptions.Add(i);
            }
            SelectedRegionIndex = RegionIndexOptions[0];
            AddedRegions = new ObservableCollection<ImagingRegionViewModel>();
        }

        public ImagingStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            NewImagingSetting = new ImageSettingsViewModel();

            RegionIndexOptions = new List<int>();
            for (int i = 1; i < 41; i++)
            {
                RegionIndexOptions.Add(i);
            }
            SelectedRegionIndex = RegionIndexOptions[0];
            AddedRegions = new ObservableCollection<ImagingRegionViewModel>();

            ImagingStep step = content.Step as ImagingStep;
            if(step != null)
            {
                IsAutoFocusOn = step.IsAutoFocusOn;
                foreach(var region in step.Regions)
                {
                    AddedRegions.Add(new ImagingRegionViewModel(region));
                }
            }
        }
        #endregion Constructor

        #region Add Region Command
        private RelayCommand _AddRegionCmd;
        public ICommand AddRegionCmd
        {
            get
            {
                if (_AddRegionCmd == null)
                {
                    _AddRegionCmd = new RelayCommand(ExecuteAddRegionCmd, CanExecuteAddRegionCmd);
                }
                return _AddRegionCmd;
            }
        }

        private void ExecuteAddRegionCmd(object obj)
        {
            ImagingRegionViewModel newRegionVm = new ImagingRegionViewModel();
            newRegionVm.Index = SelectedRegionIndex;
            newRegionVm.Lane = Lane;
            newRegionVm.X = X;
            newRegionVm.Y = Y;
            AddedRegions.Add(newRegionVm);
            SelectedAddedRegion = newRegionVm;
        }

        private bool CanExecuteAddRegionCmd(object obj)
        {
            return true;
        }
        #endregion

        #region Remove Region Command
        private RelayCommand _RemoveRegionCmd;
        public ICommand RemoveRegionCmd
        {
            get
            {
                if (_RemoveRegionCmd == null)
                {
                    _RemoveRegionCmd = new RelayCommand(ExecuteRemoveRegionCmd, CanExecuteRemoveRegionCmd);
                }
                return _RemoveRegionCmd;
            }
        }

        private void ExecuteRemoveRegionCmd(object obj)
        {
            AddedRegions.Remove(SelectedAddedRegion);
        }

        private bool CanExecuteRemoveRegionCmd(object obj)
        {
            return SelectedAddedRegion != null;
        }
        #endregion Remove Region Command

        #region Add Image Command
        private RelayCommand _AddImageCmd;
        public ICommand AddImageCmd
        {
            get
            {
                if (_AddImageCmd == null)
                {
                    _AddImageCmd = new RelayCommand(ExecuteAddImageCmd, CanExecuteAddImageCmd);
                }
                return _AddImageCmd;
            }
        }

        private void ExecuteAddImageCmd(object obj)
        {
            if (SelectedAddedRegion == null)
            {
                MessageBox.Show("Please select an added region to add Image");
                return;
            }
            ImageSettingsViewModel newImage = new ImageSettingsViewModel(NewImagingSetting);
            SelectedAddedRegion.Imagings.Add(new ImageSettingsViewModel(newImage));
            SelectedAddedRegion.SelectedImage = newImage;
        }

        private bool CanExecuteAddImageCmd(object obj)
        {
            return true;
        }
        #endregion Add Image Command

        #region Remove Image Command
        private RelayCommand _RemoveImageCmd;
        public ICommand RemoveImageCmd
        {
            get
            {
                if (_RemoveImageCmd == null)
                {
                    _RemoveImageCmd = new RelayCommand(ExecuteRemoveImageCmd, CanExecuteRemoveImageCmd);
                }
                return _RemoveImageCmd;
            }
        }

        private void ExecuteRemoveImageCmd(object obj)
        {
            if (SelectedAddedRegion == null)
            {
                MessageBox.Show("Please select an added region to Remove Image");
                return;
            }
            if(SelectedAddedRegion.SelectedImage == null)
            {
                MessageBox.Show("Please select an added image to remove");
                return;
            }
            SelectedAddedRegion.Imagings.Remove(SelectedAddedRegion.SelectedImage);
        }

        private bool CanExecuteRemoveImageCmd(object obj)
        {
            return true;
        }
        #endregion Remove Image Command

        #region Focus Command
        private RelayCommand _FocusCmd;
        public ICommand FocusCmd
        {
            get
            {
                if (_FocusCmd == null)
                {
                    _FocusCmd = new RelayCommand(ExecuteFocusCmd, CanExecuteFocusCmd);
                }
                return _FocusCmd;
            }
        }

        private void ExecuteFocusCmd(object obj)
        {
            string parameter = obj as string;
            if (parameter == "Add")
            {
                if (SelectedAddedRegion == null)
                {
                    MessageBox.Show("No region selected. please select a region at first");
                    return;
                }
                FocusViewModel newFocusVm = new FocusViewModel();
                newFocusVm.FocusName = NewFocusName;
                newFocusVm.FocusPos = NewFocusPos;
                SelectedAddedRegion.RefFocuses.Add(newFocusVm);
            }
            else if (parameter == "Delete")
            {
                if (SelectedAddedRegion == null)
                {
                    MessageBox.Show("No region selected. please select a region at first");
                    return;
                }
                if (SelectedAddedRegion.SelectedFocus == null)
                {
                    MessageBox.Show("No Reference focus selected, please select a reference focus at first");
                    return;
                }

                SelectedAddedRegion.RefFocuses.Remove(SelectedAddedRegion.SelectedFocus);
            }
        }

        private bool CanExecuteFocusCmd(object obj)
        {
            return true;
        }
        #endregion Focus Command

        public override StepsTreeViewModel Clone()
        {
            ImagingStepViewModel clonedVm = new ImagingStepViewModel();
            clonedVm.IsAutoFocusOn = this.IsAutoFocusOn;
            clonedVm.AddedRegions = new ObservableCollection<ImagingRegionViewModel>(this.AddedRegions);
            return clonedVm;
        }
    }

    internal class ImagingRegionViewModel : ViewModelBase
    {
        private int _Index;
        private String _X;
        private String _Y;
        private String _Lane;
        private ImageSettingsViewModel _SelectedImage;
        private FocusViewModel _SelectedFocus; 
        public int Index
        {
            get { return _Index; }
            set
            {
                if (_Index != value)
                {
                    _Index = value;
                    RaisePropertyChanged(nameof(Index));
                }
            }
        }
        public String Lane
        {
            get { return _Lane; }
            set
            {
                if (_Lane != value)
                {
                    _Lane = value;
                    RaisePropertyChanged(nameof(Lane));
                }
            }
        }
        public String X
        {
            get { return _X; }
            set
            {
                if (_X != value)
                {
                    _X = value;
                    RaisePropertyChanged(nameof(X));
                }
            }
        }
        public String Y
        {
            get { return _Y; }
            set
            {
                if (_Y != value)
                {
                    _Y = value;
                    RaisePropertyChanged(nameof(Y));
                }
            }
        }
        public ImageSettingsViewModel SelectedImage
        {
            get { return _SelectedImage; }
            set
            {
                if (_SelectedImage != value)
                {
                    _SelectedImage = value;
                    RaisePropertyChanged(nameof(SelectedImage));
                }
            }
        }
        public FocusViewModel SelectedFocus
        {
            get { return _SelectedFocus; }
            set
            {
                if (_SelectedFocus != value)
                {
                    _SelectedFocus = value;
                    RaisePropertyChanged(nameof(SelectedFocus));
                }
            }
        }
        public ObservableCollection<FocusViewModel> RefFocuses { get; set; } = new ObservableCollection<FocusViewModel>();
        public ObservableCollection<ImageSettingsViewModel> Imagings { get; set; } = new ObservableCollection<ImageSettingsViewModel>();

        public ImagingRegionViewModel()
        {

        }
        public ImagingRegionViewModel(ImagingRegion region)
        {
            Index = region.RegionIndex;
            Lane = region.Lane;
            X = region.X;
            Y = region.Y;
            foreach(var image in region.Imagings)
            {
                Imagings.Add(new ImageSettingsViewModel(image));
            }
            foreach(var focus in region.ReferenceFocuses)
            {
                RefFocuses.Add(new FocusViewModel(focus));
            }
        }
        public ImagingRegionViewModel(ImagingRegionViewModel otherVm)
        {
            Index = otherVm.Index;
            RefFocuses = new ObservableCollection<FocusViewModel>(otherVm.RefFocuses);
            Imagings = new ObservableCollection<ImageSettingsViewModel>(otherVm.Imagings);
        }
    }

    internal class ImageSettingsViewModel : ViewModelBase
    {
        string _SelectedChannel;
        FilterTypes _SelectedFilter;
        double _RedExposure = RecipeStepDefaultSettings.RedExposure;
        double _GreenExposure = RecipeStepDefaultSettings.GreenExposure;
        uint _RedIntensity = RecipeStepDefaultSettings.RedIntensity;
        uint _GreenIntensity = RecipeStepDefaultSettings.GreenIntensity;

        public ImageSettingsViewModel()
        {
            ChannelOptions = new List<string>();
            ChannelOptions.Add("[R]");
            ChannelOptions.Add("[G]");
            ChannelOptions.Add("[R,G]");
            SelectedChannel = ChannelOptions[0];

            FilterOptions = new List<FilterTypes>();
            foreach(FilterTypes type in Enum.GetValues(typeof(FilterTypes)))
            {
                FilterOptions.Add(type);
            }
            SelectedFilter = FilterOptions[0];
        }

        public ImageSettingsViewModel(ImagingSetting setting):this()
        {
            GreenExposure = setting.GreenExposureTime;
            GreenIntensity = setting.GreenIntensity;
            RedExposure = setting.RedExposureTime;
            RedIntensity = setting.RedIntensity;
            switch (setting.Channels)
            {
                case ImagingChannels.Green:
                    SelectedChannel = ChannelOptions[1];
                    break;
                case ImagingChannels.Red:
                    SelectedChannel = ChannelOptions[0];
                    break;
                case ImagingChannels.RedGreen:
                    SelectedChannel = ChannelOptions[2];
                    break;
            }
            SelectedFilter = FilterOptions.Find(p => p == setting.Filter);
        }

        public ImageSettingsViewModel(ImageSettingsViewModel otherVm):this()
        {
            SelectedChannel = ChannelOptions.Find(p => p == otherVm.SelectedChannel);
            SelectedFilter = FilterOptions.Find(p => p == otherVm.SelectedFilter);
            RedExposure = otherVm.RedExposure;
            GreenExposure = otherVm.GreenExposure;
            RedIntensity = otherVm.RedIntensity;
            GreenIntensity = otherVm.GreenIntensity;
        }

        public List<string> ChannelOptions { get; }
        public string SelectedChannel
        {
            get { return _SelectedChannel; }
            set
            {
                if (_SelectedChannel != value)
                {
                    _SelectedChannel = value;
                    RaisePropertyChanged(nameof(SelectedChannel));
                    RaisePropertyChanged(nameof(IsRedChannel));
                    RaisePropertyChanged(nameof(IsGreenChannel));
                }
            }
        }
        public List<FilterTypes> FilterOptions { get; }
        public FilterTypes SelectedFilter
        {
            get { return _SelectedFilter; }
            set
            {
                if(_SelectedFilter!=value)
                {
                    _SelectedFilter = value;
                    RaisePropertyChanged(nameof(SelectedFilter));
                }
            }
        }
        public bool IsRedChannel
        {
            get
            {
                if (SelectedChannel == "[R]" || SelectedChannel == "[R,G]")
                {
                    return true;
                }
                else return false;
            }
        }
        public bool IsGreenChannel
        {
            get
            {
                if (SelectedChannel == "[G]" || SelectedChannel == "[R,G]")
                {
                    return true;
                }
                else return false;
            }
        }
        public double RedExposure
        {
            get { return _RedExposure; }
            set
            {
                if (_RedExposure != value)
                {
                    _RedExposure = value;
                    RaisePropertyChanged(nameof(RedExposure));
                }
            }
        }
        public double GreenExposure
        {
            get { return _GreenExposure; }
            set
            {
                if (_GreenExposure != value)
                {
                    _GreenExposure = value;
                    RaisePropertyChanged(nameof(GreenExposure));
                }
            }
        }
        public uint RedIntensity
        {
            get { return _RedIntensity; }
            set
            {
                if (_RedIntensity != value)
                {
                    _RedIntensity = value;
                    RaisePropertyChanged(nameof(RedIntensity));
                }
            }
        }
        public uint GreenIntensity
        {
            get { return _GreenIntensity; }
            set
            {
                if (_GreenIntensity != value)
                {
                    _GreenIntensity = value;
                    RaisePropertyChanged(nameof(GreenIntensity));
                }
            }
        }
    }

    internal class FocusViewModel : ViewModelBase
    {
        string _FocusName;
        double _FocusPos;

        public FocusViewModel()
        {

        }

        public FocusViewModel(FocusSetting focus)
        {
            FocusName = focus.Name;
            FocusPos = focus.Position;
        }

        public string FocusName
        {
            get { return _FocusName; }
            set
            {
                if (_FocusName != value)
                {
                    _FocusName = value;
                    RaisePropertyChanged(nameof(FocusName));
                }
            }
        }
        public double FocusPos
        {
            get { return _FocusPos; }
            set
            {
                if (_FocusPos != value)
                {
                    _FocusPos = value;
                    RaisePropertyChanged(nameof(FocusPos));
                }
            }
        }
    }
}
