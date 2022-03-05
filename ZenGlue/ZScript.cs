using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{

    public class ZScript : IDisposable {

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct AniData {
            public uint layer;
            public float blendIn;
            public float blendOut;
            public uint flags;
            public uint dirReverse;
            public int firstFrame;
            public int lastFrame;
            public float maxFps;
            public float speed;
            public float colVolScale;
        }

        public class Ani {
            public AniData data;
            public string name;
            public string next;
            public string asc;
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_script_deinit(IntPtr script);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_script_registeredmesh_count(IntPtr script);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_registeredmesh_get(IntPtr script, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_meshtree(IntPtr script);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_script_ani_count(IntPtr script);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_ani_name_get(IntPtr script, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_ani_next_get(IntPtr script, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_ani_asc_get(IntPtr script, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref AniData zg_script_ani_data_get(IntPtr script, uint index);

        IntPtr handle;

        public ZScript(VDFS vdfs, string name) {
            handle = zg_script_init(vdfs.NativeHandle(), name);
        }

        public string meshTree() {
            return Marshal.PtrToStringAnsi(zg_script_meshtree(handle));
        }

        public string[] registeredMeshes() {
            var count = zg_script_registeredmesh_count(handle);
            var result = new string[count];
            for (uint i = 0; i < count; ++i)
                result[i] = Marshal.PtrToStringAnsi(zg_script_registeredmesh_get(handle, i));
            return result;
        }

        public Ani[] getAnis() {
            var count = zg_script_ani_count(handle);
            var result = new Ani[count];
            for (uint i = 0; i < count; ++i) {
                var a = new Ani();
                a.data = zg_script_ani_data_get(handle, i);
                a.name = Marshal.PtrToStringAnsi(zg_script_ani_name_get(handle, i));
                a.next = Marshal.PtrToStringAnsi(zg_script_ani_next_get(handle, i));
                a.asc = Marshal.PtrToStringAnsi(zg_script_ani_asc_get(handle, i));
                result[i] = a;
            }
            return result;
        }

        public void Dispose() {
            zg_script_deinit(handle);
        }
    }
}