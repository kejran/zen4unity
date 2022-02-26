using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue {
    public class ZMesh : IDisposable {

        [StructLayout(LayoutKind.Sequential)]
        public struct float3
        {
            public float x;
            public float y;
            public float z;
            public override string ToString()
            {
                return $"float3({x}, {y}, {z})";
            }
            public Vector3 toUnityAbsolute()
            {
                return new Vector3(x * 0.01f, y * 0.01f, z * 0.01f);
            }
            public Vector3 toUnityRelative()
            {
                return new Vector3(x, y, z);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct float2
        {
            public float x;
            public float y;
            public override string ToString()
            {
                return $"float3({x}, {y})";
            }
            public Vector2 toUnityRelative()
            {
                return new Vector2(x, y);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct mat3x3
        {
            public float3 m0;
            public float3 m1;
            public float3 m2;

            public Quaternion toUnity()
            {
                var mat = new Matrix4x4(
                    m0.toUnityRelative(), 
                    m1.toUnityRelative(), 
                    m2.toUnityRelative(), 
                    new Vector4(0, 0, 0, 1));
                return mat.transpose.rotation;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct mat4x4
        {
            public float m00, m01, m02, m03;
            public float m10, m11, m12, m13;
            public float m20, m21, m22, m23;
            public float m30, m31, m32, m33;

            public Matrix4x4 toUnity()
            {
                return new Matrix4x4(
                    new Vector4(m00, m01, m02, m03),
                    new Vector4(m10, m11, m12, m13),
                    new Vector4(m20, m21, m22, m23),
                    new Vector4(m30, m31, m32, m33)
                );
            }
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_mesh_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_mesh_deinit(IntPtr mesh);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_mesh_vertex_count(IntPtr mesh);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_mesh_vertex_position_get(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_mesh_vertex_normal_get(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float2 zg_mesh_vertex_texcoord_get(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_mesh_submesh_count(IntPtr mesh);

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_mesh_submesh_material_texture(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_mesh_submesh_material_color(IntPtr mesh, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_mesh_submesh_element_count(IntPtr mesh, uint submesh);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_mesh_submesh_element_get(IntPtr mesh, uint submesh, uint element);

        private IntPtr handle;

        public ZMesh(IntPtr nativeptr) {
            handle = nativeptr;
        }

        public ZMesh(VDFS vdfs, string name) {
            handle = zg_mesh_init(vdfs.NativeHandle(), name);
        }

        public uint vertexCount() {
            return zg_mesh_vertex_count(handle);
        }

        public Vector3[] vertexPositions() {
            var count = vertexCount();
            var result = new Vector3[count];
            for (uint i = 0; i < count; ++i)
                result[i] = zg_mesh_vertex_position_get(handle, i).toUnityAbsolute();
            return result;
        }

        public Vector3[] vertexNormals() {
            var count = vertexCount();
            var result = new Vector3[count];
            for (uint i = 0; i < count; ++i)
                result[i] = zg_mesh_vertex_normal_get(handle, i).toUnityRelative();
            return result;
        }

        public Vector2[] vertexUVs() {
            var count = vertexCount();
            var result = new Vector2[count];
            for (uint i = 0; i < count; ++i)
                result[i] = zg_mesh_vertex_texcoord_get(handle, i).toUnityRelative();
            return result;
        }

        public uint submeshCount() {
            return zg_mesh_submesh_count(handle);
        }

        public uint submeshElementCount(uint index) {
            return zg_mesh_submesh_element_count(handle, index);
        }
        public int[] submeshElements(uint index) {
            var count = submeshElementCount(index);
            var elements = new int[count];
            for (uint e = 0; e < count; e += 3) {
                elements[e] = (int)zg_mesh_submesh_element_get(handle, index, e);
                elements[e + 1] = (int)zg_mesh_submesh_element_get(handle, index, e + 2);
                elements[e + 2] = (int)zg_mesh_submesh_element_get(handle, index, e + 1);
            }
            return elements;
        }

        public string texture(uint index) {
            var tex_p = zg_mesh_submesh_material_texture(handle, index);
            return Marshal.PtrToStringAnsi(tex_p);
        }
        public Color color(uint index) {
            uint c = zg_mesh_submesh_material_color(handle, index);
            return Common.uintToColor(c);
        }      

        public void Dispose() {
            zg_mesh_deinit(handle);
        }
    }
}
