using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue
{
    public class ZMeshLib : IDisposable
    {

        public class Node
        {
            public Node[] children;
            public Matrix4x4 transform;
            public string name;
            public uint index;
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_meshlib_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_meshlib_deinit(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_meshlib_attached_count(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_meshlib_attached_name_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_meshlib_attached_mesh_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_meshlib_node_count(IntPtr lib);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_meshlib_node_name_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_meshlib_node_parent_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref mat4x4 zg_meshlib_node_transform_get(IntPtr lib, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_meshlib_root_transform(IntPtr lib, ref float3 pos);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_skinnedmesh_init(IntPtr lib);
        

        private IntPtr handle;

        public ZMeshLib(VDFS archive, string name)
        {
            handle = zg_meshlib_init(archive.NativeHandle(), name);
        }
    
        public bool hasNodes() 
        {
            return zg_meshlib_node_count(handle) > 0;
        }

        // public bool hasSkin() 
        // {
        //     return zg_meshlib_(handle) > 0;
        // }

        public bool hasAttachments() 
        {
            return zg_meshlib_attached_count(handle) > 0;
        }

        public Tuple<string, ZMesh>[] Attached()
        {
            var count = zg_meshlib_attached_count(handle);
            var result = new Tuple<string, ZMesh>[count];
            for (uint i = 0; i < count; ++i)
            {
                var mesh_p = zg_meshlib_attached_mesh_get(handle, i);
                var name_p = zg_meshlib_attached_name_get(handle, i);
                result[i] = Tuple.Create(Marshal.PtrToStringAnsi(name_p), new ZMesh(mesh_p));
            }
            return result;
        }

        public struct NodeInfo {
            public Node[] asArray;
            public Node[] asTree;
        }

        public NodeInfo Nodes()
        {
            var count = zg_meshlib_node_count(handle);
            var allNodes = new Node[count];
            var rootNodes = new List<Node>();
            var parents = new uint[count];

            for (uint i = 0; i < count; ++i)
            {
                var node = new Node();
                allNodes[i] = node;
                node.index = i;
                node.transform = zg_meshlib_node_transform_get(handle, i).toUnity();
                var name_p = zg_meshlib_node_name_get(handle, i);
                node.name = Marshal.PtrToStringAnsi(name_p);
                parents[i] = zg_meshlib_node_parent_get(handle, i);
            }

            for (uint i = 0; i < count; ++i)
            {
                var children = new List<Node>();
                for (uint c = 0; c < count; ++c)
                    if (parents[c] == i)
                        children.Add(allNodes[c]);
                allNodes[i].children = children.ToArray();
            }

            for (uint i = 0; i < count; ++i)
                if (parents[i] == 0xffff)
                    rootNodes.Add(allNodes[i]);

            var result = new NodeInfo();
            result.asTree = rootNodes.ToArray();
            result.asArray = allNodes;
            return result;
        }

        public float3 RootTransform() 
        {
            var result = new float3();
            zg_meshlib_root_transform(handle, ref result);
            return result;
        }

        public ZSkinnedMesh SkinnedMesh() 
        {
            return new ZSkinnedMesh(zg_skinnedmesh_init(handle));
        }

        public void Dispose()
        {
            zg_meshlib_deinit(handle);
        }
    }

}
