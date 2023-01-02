namespace Sequlite.ALF.Common
{
    //Json serialization
    public class FluidicsValueSettings
    {
        public double AspRate { get; set; }
        public double DisRate { get; set; }
        public double Volume { get; set; }
        public int Buffer1Pos { get; set; }
        public int Buffer2Pos { get; set; }
        public int Buffer3Pos { get; set; }
        public FluidicsValueSettings() { }
    }

}
