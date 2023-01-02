namespace Sequlite.ALF.Common
{
    //Json serialization
    public class CameraSettings
    {
        public int BinFactor { get; set; }
        public int Gain { get; set; }
        public int RoiLeft { get; set; }
        public int RoiTop { get; set; }
        public int RoiWidth { get; set; }
        public int RoiHeight { get; set; }
        public string ReadoutSpeed { get; set; }
        public double ExtraExposure { get; set; }

        public CameraSettings() { }
        public CameraSettings Clone()
        {
            return (CameraSettings) this.MemberwiseClone();
        }
    }

}
