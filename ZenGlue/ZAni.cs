using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{
    public class ZAni : IDisposable
    {

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_ani_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_ani_deinit(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_ani_node_count(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_ani_frame_count(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern float zg_ani_fps(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_ani_nodeindex_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_ani_sample_position_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float4 zg_ani_sample_rotation_get(IntPtr lib, uint index);
        
        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_ani_next(IntPtr lib);

        private IntPtr handle;

        public ZAni(VDFS archive, string name)
        {
            handle = zg_ani_init(archive.NativeHandle(), name);
        }

        public uint[] nodeIndices() {
            var count = zg_ani_node_count(handle);
            var result = new uint[count];
            for (uint i = 0; i < count; ++i)
                result[i] = zg_ani_nodeindex_get(handle, i);
            return result;
         
        }
    
        public float fps() {
            return zg_ani_fps(handle);
        }

        public uint frames() {
            return zg_ani_frame_count(handle);
        }

        public Pose[] packedSamples()
        {
            var count = zg_ani_frame_count(handle) * zg_ani_node_count(handle);
            var result = new Pose[count];
            for (uint i = 0; i < count; ++i)
            {
                var pos = zg_ani_sample_position_get(handle, i).toUnityAbsolute();
                var rot = zg_ani_sample_rotation_get(handle, i).toUnityQuaternion();
                result[i] = new Pose(pos, rot);
            }
            return result;
        }

        public string next() 
        {
            return Marshal.PtrToStringAnsi(zg_ani_next(handle));
        }

        public void Dispose()
        {
            zg_ani_deinit(handle);
        }
    }

}
