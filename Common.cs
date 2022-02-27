using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{           
    public class Common {
        private static float c_inv = 1.0f / 255;

        public static Color uintToColor(uint c) {
            return new Color(
                (byte)(c >> 16) * c_inv, 
                (byte)(c >> 8) * c_inv, 
                (byte)(c) * c_inv, 
                (byte)(c >> 24) * c_inv);
        }
    }

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

    public interface MaterialMesh {
        public uint submeshCount();
        public string texture(uint index);
        public Color color(uint index);
    }
}