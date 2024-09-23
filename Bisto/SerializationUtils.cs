using System.Runtime.InteropServices;

namespace Bisto
{
    public static class SerializationUtils
    {
        public static T BytesToStructure<T>(byte[] bytes)
            where T : struct
        {
            T structure;
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                structure = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            return structure;
        }

        public static byte[] StructureToBytes<T>(T structure)
            where T : struct
        {
            int size = Marshal.SizeOf(structure);
            byte[] bytes = new byte[size];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }

            return bytes;
        }
    }
}
