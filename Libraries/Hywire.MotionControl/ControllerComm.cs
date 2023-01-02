using Hywire.CommLibrary;

namespace Hywire.MotionControl
{
    internal class ControllerComm : SerialCommBase
    {
        public Frame Response { get; private set; }

        public ControllerComm() : base(1000)
        {

        }

        #region Public Functions
        public void SetStatusIdle()
        {
            Status = CommStatus.Idle;
        }
        #endregion Public Functions
        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            detectedFrameIndex = 0;
            if (ReadIndex >= 8)
            {
                for (int i = 0; i <= ReadIndex - 8; i++)
                {
                    if (ReadBuf[i] == 0x56)
                    {
                        int frameLength = 0;
                        Response = Frame.ResonseDecode(ReadBuf, i, ReadIndex, out frameLength);
                        if(Response == null) { return; }
                        detectedFrameIndex = i + frameLength;
                        Status = CommStatus.Idle;
                        break;
                    }
                }
            }
        }
    }
}
