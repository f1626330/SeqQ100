using System;
using System.Runtime.InteropServices;

namespace Hywire.CommLibrary
{
    public static class StructBytesConverter
    {
        public static byte[] GetBytes(object structure)
        {
            int size = Marshal.SizeOf(structure);
            IntPtr handle = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(structure, handle, false);
                byte[] bytes = new byte[size];
                Marshal.Copy(handle, bytes, 0, size);
                return bytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }
        }

        public static object ToStruct(byte[] bytes, Type structType)
        {
            int size = Marshal.SizeOf(structType);
            IntPtr handle = Marshal.AllocHGlobal(size);
            object result = new object();
            try
            {
                Marshal.Copy(bytes, 0, handle, size);
                result = Marshal.PtrToStructure(handle, structType);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }
        }
    }
}
