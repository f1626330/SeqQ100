using InteractiveDataDisplay.WPF;
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class TileItem : BaseModel
    {
        public string Name { get; set; }
        
        public bool UseTile { get => Get<bool>(); set => Set<bool>(value); }
        public bool IsInAllRun { get => Get<bool>(); set => Set<bool>(value); }
    }

    public class TileSelectionViewModel : DialogViewModelBase
    {
        ISeqLog Logger { get; }
        public bool IsOk { get; private set; }

        

        #region FOR_MAP_FIELDS
        MapXYModel _MapXYSetting = new MapXYModel();
        public MapXYModel MapXYSetting { get => _MapXYSetting; set => SetProperty(ref _MapXYSetting, value); }
        //key lane number
        Dictionary<int, HeatMapData> _HeatMapDataList;
        int _MaxNumLanes;
        int MaxNumLanes { get => _MaxNumLanes; }
        int[] _MapDataLock = new int[0];
        ObservableCollection<HeatmapGraph> _HeatMaps;
        public ObservableCollection<HeatmapGraph> HeatMaps { get => _HeatMaps; set => SetProperty(ref _HeatMaps, value); }
        double _TileRatio;
        public double TileRatio { get => _TileRatio; set => SetProperty(ref _TileRatio, value); }
        int _CurrrentMap = 0;
        public int CurrrentMap { get => _CurrrentMap; set => SetProperty(ref _CurrrentMap, value); }
        int _MaxWidthHeatmap = 800;
        public int MaxWidthHeatmap { get => _MaxWidthHeatmap; set => SetProperty(ref _MaxWidthHeatmap, value); }
        private AutoResetEvent _MapRenderComplete = new AutoResetEvent(true);
       
        HeatMapData HeatMapData(int heatmapIndex) => _HeatMapDataList[heatmapIndex];
        #endregion

        public TileSelectionViewModel(ISeqLog logger,
            List<TileItem> allAvailableTiles,//List<SeqTile> allAvailableTiles, 
            List<string> selectedTiles)
        {
            Logger = logger;
            IsModal = true;
            ObservableCollection<TileItem> tileListToBeSeleted = new ObservableCollection<TileItem>();
            bool bSelected;
            foreach (var it in allAvailableTiles)
            {
                if (selectedTiles == null)
                {
                    bSelected = true;
                }
                else
                {
                    var item = selectedTiles.Where(i => i == it.Name).FirstOrDefault();
                    if (item != default(string))
                    {
                        bSelected = true;
                    }
                    else
                    {
                        bSelected = false;
                    }
                }

                tileListToBeSeleted.Add(new TileItem() { Name = it.Name, UseTile = bSelected && it.IsInAllRun, IsInAllRun=it.IsInAllRun });
            }
            TileList = tileListToBeSeleted;
        }

        ObservableCollection<TileItem> _TileList;
        public ObservableCollection<TileItem> TileList
        {
            get => _TileList;
            set => SetProperty(ref _TileList, value);

        }

        


        protected override void RunOKCommand(object o)
        {
            int n = int.Parse((string) o);
            IsOk = (n == 1);
            Close();
        }

        ICommand _DeselectAllCommand;
        public ICommand DeselectAllCommand
        {
            get
            {
                if (_DeselectAllCommand == null)
                    _DeselectAllCommand = new RelayCommand(
                        (o) => this.DeselectAll());

                return _DeselectAllCommand;
            }
        }

        void DeselectAll()
        {
            foreach (var it in TileList)
            {
                it.UseTile = false;
            }
        }

      
        #region FOR_MAP_FUNCTIONS
        public void BuildHeatMaps(List<SeqTile> allSeqTiles, int maxRows, int maxColumns, int maxLanes)
        {
            HeatMaps = new ObservableCollection<HeatmapGraph>();
            _MaxNumLanes = maxLanes;
            int NX = maxColumns;
            int NY = maxRows;
            TileRatio = NY / (double)NX;
            Palette defaultHeat = Palette.Heat;
            Palette heatPalette = new Palette(false, defaultHeat.Range, defaultHeat.Points);
            List<HeatmapGraph> heatMaps = new List<HeatmapGraph>();
            _HeatMapDataList = new Dictionary<int, HeatMapData>();
            int lane;
            Dispatch(() =>
            {
                for (int i = 0; i < MaxNumLanes; i++)
                {
                    lane = maxLanes - i;
                    HeatmapGraph hm = new HeatmapGraph() { Palette = heatPalette, AspectRatio = 1.0 }; //up-down from 0 to  maxLanes-1, the lane# shall be from maxLens to 1
                    hm.TooltipContentFunc = HeaterMapTooltipContent;
                    heatMaps.Add(hm);
                    _HeatMapDataList.Add(i, new HeatMapData() { Data = new double[NX, NY], Lane = lane });

                }
            });
            HeatMaps = new ObservableCollection<HeatmapGraph>(heatMaps);

            double[] XMap = new double[NX + 1];
            double[] YMap = new double[NY + 1];
            for (int i = 0; i <= NX; i++)
                XMap[i] = i;
            for (int j = 0; j <= NY; j++)
                YMap[j] = j;

            MapXYSetting.NXMap = NX;
            MapXYSetting.NYMap = NY;
            MapXYSetting.XMap = XMap;
            MapXYSetting.YMap = YMap;
            MapXYSetting.XMapMin = 0;
            MapXYSetting.XMapWidth = NX - MapXYSetting.XMapMin;
            MapXYSetting.YMapMin = 0;
            MapXYSetting.YMapHeight = NY - MapXYSetting.YMapMin;

            UpdateHeatMaps();
        }

        Object HeaterMapTooltipContent(Point pt)
        {
            int x = (int)Math.Ceiling(HeatMaps[CurrrentMap].XFromLeft(pt.X));
            int y = (int)Math.Ceiling(HeatMaps[CurrrentMap].YFromTop(pt.Y));
            
            lock (_MapDataLock)
            {
                if (x >= 1 && x <= _HeatMapDataList[CurrrentMap].Data.GetLongLength(0) &&
                    y >= 1 && y <= _HeatMapDataList[CurrrentMap].Data.GetLongLength(1))
                {
                    //char c = Convert.ToChar(y + 64);
                    //return $"bL{MaxNumLanes - CurrrentMap}{x:D2}{c}";
                    return TileName(MaxNumLanes - CurrrentMap, "b", x, y);
                }
                else
                {
                    return null;
                }
            }
        }

        string TileName(int lane, string surface, int column, int row)
        {
            char c = Convert.ToChar(row + 64);
            return $"{surface}L{lane}{column:D2}{c}";
        }

        private delegate void PlotDataDel(HeatmapGraph hp, double[] x, double[] y, double[,] data, string mapName);
        private void PlotData(HeatmapGraph heatmap, double[] xp, double[] yp, double[,] fp, string mapName)
        {
            // HeatmapGraph objects prepare images to be drawn in a background thread. 
            // The Plot method cancels current incomplete images before starting a new one. 
            // This increases responsiveness of the UI but may result in loss of certain 
            // or even all of the frames.
            // The following code shows how to wait until a certain data is actually drawn.
            try
            {
                long id = heatmap.Plot(fp, xp, yp); // receive a unique operation identifier
                heatmap.RenderCompletion // an observable of completed and canceled operations
                    .Where(rc => rc.TaskId == id) // filter out an operation with the known id
                    .Subscribe(dummy => { OnMapPlotted(); }, (o) => OnMapPlotError(o)); // signal when the id is observed
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to plot {mapName} with exception error: {ex.Message}");
                _MapRenderComplete.Set();
            }
        }

        void OnMapPlotted()
        {
            try
            {
                _MapRenderComplete.Set();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Heat Plot error: {ex.Message}");
                _MapRenderComplete.Set();
            }
        }
        void OnMapPlotError(Exception ex)
        {
            Logger.LogError($"Catched Heat Plot error: {ex.Message}");
            _MapRenderComplete.Set();
        }

        void UpdateHeatMaps()
        {
            //key -- map index
            Dictionary<int, HeatMapDisplayData> heatMapDisplayList = new Dictionary<int, HeatMapDisplayData>();
            bool hasError = false;
            try
            {
                lock (_MapDataLock)
                {
                    HeatMapData heatMapData;
                    double[,] fp, f;
                    double[] XMap, YMap;
                    int NX, NY;

                    for (int mapIndex = 0; mapIndex < MaxNumLanes; mapIndex++)
                    {
                        heatMapData = HeatMapData(mapIndex);
                        f = heatMapData.Data;
                        NX = f.GetLength(0);
                        NY = f.GetLength(1);
                        fp = new double[NX, NY];
                        string tileName;
                        for (int i = 0; i < NX; i++) //column
                        {
                            for (int j = 0; j < NY; j++) //row
                            {
                                //fp[i, j] = (f[i, j] - minV) / diff; //normalized
                                tileName = TileName(MaxNumLanes - mapIndex, "b", i + 1, j + 1);
                                var item = TileList.Where(it => it.Name == tileName).FirstOrDefault();
                                if (item != default(TileItem))
                                {
                                    if (item.UseTile)
                                    {
                                        fp[i, j] = 0.5;
                                    }
                                    else
                                    {
                                        fp[i, j] = 1;
                                    }
                                }
                                else
                                {
                                    fp[i, j] = 0;
                                }
                                
                            }
                        }
                        XMap = MapXYSetting.XMap.Clone() as double[];
                        YMap = MapXYSetting.YMap.Clone() as double[];
                        heatMapDisplayList.Add(mapIndex,
                            new HeatMapDisplayData()
                            {
                                Data = fp,
                                XMap = XMap,
                                YMap = YMap,
                                DataBound = new MinMaxRange() { Min = 0, Max = 1 },
                                Lane = heatMapData.Lane,
                            });
                    } //for

                } //lock
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to UpdateHeatMaps with exception error: {ex.Message}");
                hasError = true;
            }

            if (!hasError)
            {
                
                foreach (var it in heatMapDisplayList)
                {
                    try
                    {
                        //_MapRenderComplete.WaitOne();
                        HeatmapGraph heatmap = HeatMaps[it.Key];
                        this.Dispatch(() => PlotData(heatmap, it.Value.XMap, it.Value.YMap, it.Value.Data, $"Heatmap{it.Key}"));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"failed to plot heatmap {it.Key} with exception error: {ex.Message}");
                    }
                }
            }
        }
        #endregion

    }
}
