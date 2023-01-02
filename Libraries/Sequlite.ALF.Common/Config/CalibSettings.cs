using Sequlite.ALF.Common.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sequlite.ALF.Common.SystemCalibJson;

namespace Sequlite.ALF.Common
{
    public class CalibSettings
    {
        //from config file one-one ma, read only shall be never changed after reading from config file------------------------------
        public SystemCalibJson _SystemCalibfig;
        public CalibSettings _CalibSettings;
        public SystemCalibJson SystemCalibfig { get { return _SystemCalibfig; } set { _SystemCalibfig = value; } }
        //----end

        public CalibrationVersion Version { get { return _SystemCalibfig.Version; } }
        public AutoFocusSettings AutoFocusingSettings { get { return _SystemCalibfig.AutoFocusingSettings; } }
        public InstrumentInfo InstrumentInfo { get { return _SystemCalibfig.InstrumentInfo; } }

        public CameraCalib CameraCalibSettings { get { return _SystemCalibfig.CameraCalibSettings; } }
        public FluidicsCalib FluidicsCalibSettings { get { return _SystemCalibfig.FluidicsCalibSettings; } }
        public Dictionary<string, double> StageRegions { get { return _SystemCalibfig.StageRegions; } }
        public Dictionary<RegionIndex, double[]> StageRegionMaps { get; } = new Dictionary<RegionIndex, double[]>();
        public int FCLane { get; set; } = 0;
        public int FCRow { get; set; } = 0;
        public int FCColumn { get; set; } = 0;
        public Dictionary<string, ImageTransformConfig> ImageTransforms { get { return _SystemCalibfig.ImageTransforms; } }

        public void SetLoadedCalibConfig(SystemCalibJson cfs, ISeqLog logger)
        {
            _SystemCalibfig = cfs;
            {//start block
                double lane = 0;
                double row = 0;
                double col = 0;
                double vinterval = 0;
                double hinterval = 0;
                double[][] startpoint = new double[4][];
                startpoint[0] = new double[2];
                startpoint[1] = new double[2];
                startpoint[2] = new double[2];
                startpoint[3] = new double[2];
                foreach (var it in StageRegions)
                {
                    string name = it.Key;
                    double value = it.Value;
                    if (name == "Lane") { lane = value; }
                    if (name == "Row") { row = value; }
                    if (name == "Column") { col = value; }
                    if (name == "HInterval") { hinterval = value; }
                    if (name == "VInterval") { vinterval = value; }
                    if (name == "StartPointX1") { startpoint[0][0] = value; }
                    if (name == "StartPointY1") { startpoint[0][1] = value; }
                    if (name == "StartPointX2") { startpoint[1][0] = value; }
                    if (name == "StartPointY2") { startpoint[1][1] = value; }
                    if (name == "StartPointX3") { startpoint[2][0] = value; }
                    if (name == "StartPointY3") { startpoint[2][1] = value; }
                    if (name == "StartPointX4") { startpoint[3][0] = value; }
                    if (name == "StartPointY4") { startpoint[3][1] = value; }
                }
                FCColumn = (int)col;
                FCLane = (int)lane;
                FCRow = (int)row;
                double[] newpoint = new double[2];
                if (lane * col * row == 0)
                {
                    logger.LogError("Wrong RegionSettings in Calib Config file");
                    throw new Exception("Wrong RegionSettings in Calib Config file ");
                }

                for (int l = 1; l < lane + 1; l++)
                {
                    for (int r = 1; r < row + 1; r++)
                    {
                        for (int c = 1; c < col + 1; c++)
                        {

                            int colx;
                            if (r % 2 == 0) { colx = (int)col - c + 1; } else { colx = c; }
                            int[] regionindex = new int[3] { l, colx, r };
                            newpoint = new double[2] { (startpoint[(l - 1)][0] + (hinterval * (colx - 1))), (startpoint[(l - 1)][1] + (vinterval * (r - 1))) };
                            StageRegionMaps.Add(new RegionIndex(regionindex), newpoint);
                        }
                    }
                }
            } //end block
        }

    }
}
