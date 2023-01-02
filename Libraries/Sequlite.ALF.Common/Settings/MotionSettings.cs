namespace Sequlite.ALF.Common
{
    //Json serialization
    public class MotionSettings
    {
        public double Speed { get; set; }
        public double Accel { get; set; }
        public double Absolute { get; set; }
        public double Relative { get; set; }
        public MotionSettings()
        { }
    }

    //Json serialization
    public class MotionRanges
    {
        public SettingRange SpeedRange { get; set; }
        public SettingRange AccelRange { get; set; }
        public SettingRange MotionRange { get; set; }
        public MotionRanges()
        {
            SpeedRange = new SettingRange();
            AccelRange = new SettingRange();
            MotionRange = new SettingRange();
        }
        public MotionRanges(SettingRange speeds, SettingRange accels, SettingRange motions)
        {
            SpeedRange = speeds;
            AccelRange = accels;
            MotionRange = motions;
        }

        public MotionRanges(MotionRanges other)
        {
            SpeedRange = other.SpeedRange;
            AccelRange = other.AccelRange;
            MotionRange = other.MotionRange;
        }
    }

    //Json serialization
    public enum MotionTypes 
    {
        None,
        Filter,
        YStage,
        ZStage,
        Cartridge,
        XStage,
        FCDoor,
    }
}
