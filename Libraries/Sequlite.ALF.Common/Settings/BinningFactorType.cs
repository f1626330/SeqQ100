namespace Sequlite.ALF.Common
{
    //Json serialization
    public class BinningFactorType
    {
        public int Position
        {
            get;
            set;
        }

        public string DisplayName
        {
            get;
            set;
        }

        public int VerticalBins
        {
            get;
            set;
        }

        public int HorizontalBins
        {
            get;
            set;
        }

        public BinningFactorType()
        {
        }

        // copy constructor
        public BinningFactorType(BinningFactorType binFactor)
        {
            this.Position = binFactor.Position;
            this.DisplayName = binFactor.DisplayName;
            this.VerticalBins = binFactor.VerticalBins;
            this.HorizontalBins = binFactor.HorizontalBins;
        }

        public BinningFactorType(int position, int horizontalBin, int verticalBin, string displayName)
        {
            this.Position = position;
            this.HorizontalBins = horizontalBin;
            this.VerticalBins = verticalBin;
            this.DisplayName = displayName;
        }
    }

    //Json serialization
    public class GainType
    {
        #region Public properties...

        public int Position { get; set; }

        public int Value { get; set; }

        public string DisplayName { get; set; }

        #endregion

        #region Constructors...

        public GainType()
        {
        }

        public GainType(int position, int value, string displayName)
        {
            this.Position = position;
            this.Value = value;
            this.DisplayName = displayName;
        }

        #endregion
    }

    public class ReadoutType
    {
        #region Public properties...

        public int Position { get; set; }

        public int Value { get; set; }

        public string DisplayName { get; set; }

        #endregion

        #region Constructors...

        public ReadoutType()
        {
        }

        public ReadoutType(int position, int value, string displayName)
        {
            this.Position = position;
            this.Value = value;
            this.DisplayName = displayName;
        }

        #endregion
    }


}
