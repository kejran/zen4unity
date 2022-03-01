using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue 
{
	public class ZSkinnedMesh : IDisposable, MaterialMesh 
	{

		[StructLayout(LayoutKind.Sequential)]
		public struct float3x4
		{
			public float3 v0;
			public float3 v1;
			public float3 v2;
			public float3 v3;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct u8x4
		{
			public byte b0;
			public byte b1;
			public byte b2;
			public byte b3;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct f4
		{
			public float f0;
			public float f1;
			public float f2;
			public float f3;
		}

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern void zg_skinnedmesh_deinit(IntPtr lib);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint zg_skinnedmesh_vertex_count(IntPtr mesh);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern ref float3x4 zg_skinnedmesh_vertex_positions_get(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern ref u8x4 zg_skinnedmesh_vertex_boneindices_get(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern ref float3 zg_skinnedmesh_vertex_normal_get(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern ref float2 zg_skinnedmesh_vertex_texcoord_get(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern ref f4 zg_skinnedmesh_vertex_weights_get(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint zg_skinnedmesh_submesh_count(IntPtr mesh);

		[DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr zg_skinnedmesh_submesh_material_texture(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint zg_skinnedmesh_submesh_material_color(IntPtr mesh, uint index);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint zg_skinnedmesh_submesh_element_count(IntPtr mesh, uint submesh);

		[DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint zg_skinnedmesh_submesh_element_get(IntPtr mesh, uint submesh, uint element);


		private IntPtr handle;

		public ZSkinnedMesh(IntPtr nativeptr) {
			handle = nativeptr;
		}

		public uint vertexCount() {
			return zg_skinnedmesh_vertex_count(handle);
		}

		public Vector3[] bindPoseVertices(Transform[] bones, BoneWeight[] weights) {
			var mats = bones.Select(t => t.localToWorldMatrix).ToArray();
			var count = vertexCount();
			var result = new Vector3[count];
			for (uint i = 0; i < count; ++i) {
				var v = Vector3.zero;
				var w = weights[i];
                var pos = zg_skinnedmesh_vertex_positions_get(handle, i);
				v += w.weight0 * mats[w.boneIndex0].MultiplyPoint(pos.v0.toUnityAbsolute());
				v += w.weight1 * mats[w.boneIndex1].MultiplyPoint(pos.v1.toUnityAbsolute());
				v += w.weight2 * mats[w.boneIndex2].MultiplyPoint(pos.v2.toUnityAbsolute());
				v += w.weight3 * mats[w.boneIndex3].MultiplyPoint(pos.v3.toUnityAbsolute());
				result[i] = v;
			}
			return result;
		}

		public Vector3[] vertexNormals() {
			var count = vertexCount();
			var result = new Vector3[count];
			for (uint i = 0; i < count; ++i)
				result[i] = zg_skinnedmesh_vertex_normal_get(handle, i).toUnityRelative();
			return result;
		}

		public Vector2[] vertexUVs() {
			var count = vertexCount();
			var result = new Vector2[count];
			for (uint i = 0; i < count; ++i)
				result[i] = zg_skinnedmesh_vertex_texcoord_get(handle, i).toUnityRelative();
			return result;
		}

		public BoneWeight[] boneWeights() {
			var count = vertexCount();
			var result = new BoneWeight[count];
			for (uint i = 0; i < count; ++i) {
				var weights = zg_skinnedmesh_vertex_weights_get(handle, i);
				var indices = zg_skinnedmesh_vertex_boneindices_get(handle, i);
				var r = new BoneWeight();
				r.boneIndex0 = indices.b0;
				r.boneIndex1 = indices.b1;
				r.boneIndex2 = indices.b2;
				r.boneIndex3 = indices.b3;
				r.weight0 = weights.f0;
				r.weight1 = weights.f1;
				r.weight2 = weights.f2;
				r.weight3 = weights.f3;
				result[i] = r;
			}
			return result;
		}

		public uint submeshCount() {
			return zg_skinnedmesh_submesh_count(handle);
		}

		public uint submeshElementCount(uint index) {
			return zg_skinnedmesh_submesh_element_count(handle, index);
		}
		public int[] submeshElements(uint index) {
			var count = submeshElementCount(index);
			var elements = new int[count];
			for (uint e = 0; e < count; e += 3) {
				elements[e] = (int)zg_skinnedmesh_submesh_element_get(handle, index, e);
				elements[e + 1] = (int)zg_skinnedmesh_submesh_element_get(handle, index, e + 2);
				elements[e + 2] = (int)zg_skinnedmesh_submesh_element_get(handle, index, e + 1);
			}
			return elements;
		}

		public string texture(uint index) {
			var tex_p = zg_skinnedmesh_submesh_material_texture(handle, index);
			return Marshal.PtrToStringAnsi(tex_p);
		}
		public Color color(uint index) {
			uint c = zg_skinnedmesh_submesh_material_color(handle, index);
			return Common.uintToColor(c);
		}      

		public void Dispose() {
			zg_skinnedmesh_deinit(handle);
		}

	}
}