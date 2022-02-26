using System;
using System.Runtime.InteropServices;

namespace ZenGlue
{
    public class VOB {
        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vobs_count(IntPtr vobarray);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vobs_get(IntPtr vobarray, uint index);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vob_visual(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vob_name(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vob_type(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_vob_children(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref ZMesh.float3 zg_vob_position(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref ZMesh.mat3x3 zg_vob_rotation(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref ZMesh.mat4x4 zg_vob_transform(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref uint zg_vob_show(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_vob_light_color(IntPtr vob);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern float zg_vob_light_range(IntPtr vob);

        public enum Type {
            Vob,
            VobLevelCompo,
            Item,
            MOB,
            MobInter,
            MobDoor,
            MobBed,
            MobFire,
            MobLadder,
            MobSwitch,
            MobWheel,
            MobContainer,
            VobLight,
            VobSound,
            VobSoundDaytime,
            ZoneMusic,
            ZoneMusicDefault,
            MessageFilter,
            CodeMaster,
            Trigger,
            TriggerList,
            TriggerScript,
            TriggerChangeLevel,
            TriggerWorldStart,
            Mover,
            VobStartpoint,
            VobSpot,
            PFXControler,
            TouchDamage
        };

        internal static VOB[] constructVOBArray(IntPtr ptr) {
            var count = zg_vobs_count(ptr);
            var result = new VOB[count];
            for (uint i = 0; i < count; ++i)
                result[i] = new VOB(zg_vobs_get(ptr, i));
            return result;
        }

        private IntPtr handle;
        public VOB(IntPtr ptr) { handle = ptr; }

        public string name() {
            var name_p = zg_vob_name(handle);
            return Marshal.PtrToStringAnsi(name_p);
        }

        public string visual() {
            var name_p = zg_vob_visual(handle);
            return Marshal.PtrToStringAnsi(name_p);
        }

        public VOB[] children() {
            var arr = zg_vob_children(handle);
            return constructVOBArray(arr);
        }

        public Type type() {
            return (Type)zg_vob_type(handle);
        }

        public UnityEngine.Vector3 position() {
            return zg_vob_position(handle).toUnityAbsolute();
        }

        public UnityEngine.Quaternion rotation() {
            return zg_vob_rotation(handle).toUnity();
        }

        public UnityEngine.Matrix4x4 transform()
        {
            return zg_vob_transform(handle).toUnity();
        }

        public bool show() {
            return zg_vob_show(handle) > 0; 
        }

        public UnityEngine.Color lightColor() {
            var c = zg_vob_light_color(handle);
            return Common.uintToColor(c);
        }
        public float lightRange() {
            return zg_vob_light_range(handle) * 0.01f;
        }

    }
}