using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{
    public class ZMorph : IDisposable
    {

        public class Blend {
            public string name;
            public Vector3[] vertices;
            public uint[] indices;
            public uint frames;
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_morph_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_morph_deinit(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_morph_blend_count(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_morph_frame_count(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_morph_vertex_count(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_morph_vertex_index_get(IntPtr lib, uint ani, uint vertex);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_morph_vertex_sample_get(IntPtr lib, uint ani, uint sample);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_morph_name_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_morph_mesh(IntPtr lib);

        private IntPtr handle;

        public ZMorph(VDFS archive, string name)
        {
            handle = zg_morph_init(archive.NativeHandle(), name);
        }

        public ZMesh mesh() 
        {
            return new ZMesh(zg_morph_mesh(handle));
        }
    
        public Blend[] blends() 
        {
            var count = zg_morph_blend_count(handle);
            var result = new Blend[count];
            for (uint i = 0; i < count; ++i) 
            {
                var b = new Blend();
                b.name = Marshal.PtrToStringAnsi(zg_morph_name_get(handle, i));
                var vcount = zg_morph_vertex_count(handle, i);
                var fcount = zg_morph_frame_count(handle, i);
                b.frames = fcount;
                b.vertices = new Vector3[vcount * fcount];
                b.indices = new uint[vcount];
                for (uint v = 0; v < vcount; ++v) 
                {
                    for (uint f = 0; f < fcount; ++f) 
                        b.vertices[v + f * vcount] = zg_morph_vertex_sample_get(handle, i, v + f * vcount).toUnityAbsolute();
                    b.indices[v] = zg_morph_vertex_index_get(handle, i, v);
                }
                result[i] = b;
            }
            return result;
        }

        public void Dispose()
        {
            zg_morph_deinit(handle);
        }
    }

}
