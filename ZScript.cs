using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{

    public class ZScript : IDisposable {

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct AniData {
            uint layer;
            float blendIn;
            float blendOut;
            uint flags;
            uint dirReverse;
            int firstFrame;
            int lastFrame;
            float maxFps;
            float speed;
            float colVolScale;
        }

        public class Ani {
            AniData data;
            string name;
            string next;
            string asc;
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_script_deinit(IntPtr mesh);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_script_registeredmesh_count(IntPtr mesh);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_registeredmesh_get(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_script_meshtree(IntPtr mesh);

        //Ani[] getAnis()

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

        public void Dispose() {
            zg_script_deinit(handle);
        }
    }
}