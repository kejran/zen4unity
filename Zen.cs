using System;
using System.Runtime.InteropServices;

namespace ZenGlue {
    public class Zen : IDisposable
    {
        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_init(IntPtr vdfs, string name, bool g2);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_deinit(IntPtr zen);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_mesh(IntPtr zen);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_data(IntPtr zen);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_data_vobs(IntPtr zendata);

        private IntPtr handle = IntPtr.Zero;
        private string name;

        public string Name { get { return name; } }

        public IntPtr NativeHandle() {
            return handle;
        }

        public Zen(VDFS vdfs, string name, bool forceG2) {
            handle = zg_zen_init(vdfs.NativeHandle(), name, forceG2);
            this.name = name;
        }

        public ZMesh mesh() {
            var ptr = zg_zen_mesh(handle);
            return new ZMesh(ptr);
        }

        public class WorldData
        {
            IntPtr handle;
            public WorldData(IntPtr ptr) {
                handle = ptr;
            }
            public VOB[] vobs() {
                var vobArray = zg_zen_data_vobs(handle);
                return VOB.constructVOBArray(vobArray);
            }
        }

        public WorldData data()
        {
            var ptr = zg_zen_data(handle);
            return new WorldData(ptr);
        }

        public void Dispose() {
            zg_zen_deinit(handle);
            handle = IntPtr.Zero;
        }
    }
}