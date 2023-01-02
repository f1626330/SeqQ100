namespace Hywire.CommLibrary
{
    public static class CheckSumCalculator
    {
        public static byte Cal(byte[] array, int offset, int length)
        {
            byte result = 0;
            for (int i = 0; i < length; i++)
            {
                result = (byte)(result ^ array[i]);
            }
            return result;
        }
    }
}
