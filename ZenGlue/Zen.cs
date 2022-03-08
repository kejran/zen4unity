using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZenGlue {

    [Serializable]
    public class Waynet 
    {
        [Serializable]
        public class Node 
        {
            public Vector3 position;
            public Vector3 direction;
            public string name;
        }
        
        [Serializable]
        public class Edge 
        {
            public uint n1;
            public uint n2;
        }

        public Node[] nodes;
        public Edge[] edges;
    }

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


        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_zen_waynet_node_count(IntPtr zendata);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_zen_waynet_node_name_get(IntPtr zendata, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_zen_waynet_node_position_get(IntPtr zendata, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref float3 zg_zen_waynet_node_direction_get(IntPtr zendata, uint index);
        
        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_zen_waynet_edge_count(IntPtr zendata);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_zen_waynet_edge_1_get(IntPtr zendata, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_zen_waynet_edge_2_get(IntPtr zendata, uint index);


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
            if (ptr != IntPtr.Zero)
                return new ZMesh(ptr);
            else return null;
        }

        public class WorldData
        {
            IntPtr handle;
            public WorldData(IntPtr ptr) {
                handle = ptr;
            }
            public ZVOB[] vobs() {
                var vobArray = zg_zen_data_vobs(handle);
                return ZVOB.constructVOBArray(vobArray);
            }

            public Waynet waynet() {
                var result = new Waynet();
                var ncount = zg_zen_waynet_node_count(handle);

                result.nodes = new Waynet.Node[ncount];
                for (uint i = 0; i < ncount; ++i) {
                    var n = new Waynet.Node();
                    n.name = Marshal.PtrToStringAnsi(zg_zen_waynet_node_name_get(handle, i));
                    n.position = zg_zen_waynet_node_position_get(handle, i).toUnityAbsolute();
                    n.direction = zg_zen_waynet_node_direction_get(handle, i).toUnityRelative();
                    result.nodes[i] = n;
                }
                
                var ecount = zg_zen_waynet_edge_count(handle);
                result.edges = new Waynet.Edge[ecount];
                for (uint i = 0; i < ecount; ++i) {
                    var e = new Waynet.Edge();
                    e.n1 = zg_zen_waynet_edge_1_get(handle, i);
                    e.n2 = zg_zen_waynet_edge_2_get(handle, i);
                    result.edges[i] = e;
                }

                return result;
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