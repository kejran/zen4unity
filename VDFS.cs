using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{
    public class VDFS : IDisposable
    {
        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vdfs_init(string argv0);

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_vdfs_deinit(IntPtr vdfs);

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vdfs_load(IntPtr vdfs, string archive_path);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_vdfs_finalize(IntPtr vdfs);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vdfs_file_count(IntPtr vdfs);

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vdfs_file_exists(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vdfs_file_name_get(IntPtr vdfs, uint index);

        private IntPtr handle = IntPtr.Zero;

        public IntPtr NativeHandle()
        {
            return handle;
        }

        public VDFS()
        {
            handle = zg_vdfs_init(Application.persistentDataPath);
        }

        public void LoadArchive(string archive)
        {
            zg_vdfs_load(handle, archive);
        }

        public void FinalizeLoad()
        {
            zg_vdfs_finalize(handle);
        }

        public bool Exists(string file)
        {
            return zg_vdfs_file_exists(handle, file) > 0;
        }

        public string[] Files()
        {
            var count = zg_vdfs_file_count(handle);
            var result = new string[count];
            for (uint i = 0; i < count; ++i)
            {
                var ptr = zg_vdfs_file_name_get(handle, i);
                result[i] = Marshal.PtrToStringAnsi(ptr);
            }
            return result;
        }

        public void Dispose()
        {
            zg_vdfs_deinit(handle);
            handle = IntPtr.Zero;
        }
    }
}