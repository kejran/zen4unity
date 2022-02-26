using System;
using System.Runtime.InteropServices;

namespace ZenGlue {

    public class ZTexture : IDisposable {

        private const uint dxt5cc = 0x35545844;
        private const uint dxt3cc = 0x33545844;
        private const uint dxt1cc = 0x31545844;
        private const uint ddscc = 0x20534444;

        [StructLayout(LayoutKind.Sequential)]
        private struct DDPIXELFORMAT {
            public uint dwSize;   // size of this structure (must be 32)
            public uint dwFlags;  // see DDPF_*
            public uint dwFourCC;
            public uint dwRGBBitCount;  // Total number of bits for RGB formats
            public uint dwRBitMask;
            public uint dwGBitMask;
            public uint dwBBitMask;
            public uint dwRGBAlphaBitMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DDCAPS2 {
            public uint dwCaps1;  // Zero or more of the DDSCAPS_* members
            public uint dwCaps2;  // Zero or more of the DDSCAPS2_* members
                                  //            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
                                  //            uint[] dwReserved;
            uint r0, r1;
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct DDSURFACEDESC2
        {
            public uint dwSize;   // size of this structure (must be 124)
            public uint dwFlags;  // combination of the DDSS_* flags
            public uint dwHeight;
            public uint dwWidth;
            public uint dwPitchOrLinearSize;
            public uint dwDepth;  // Depth of a volume texture
            public uint dwMipMapCount;
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            //uint[] dwReserved1;
            uint r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10;
            public DDPIXELFORMAT ddpfPixelFormat;
            public DDCAPS2 ddsCaps;
            uint dwReserved2;
        }

        [DllImport("zenglue", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_tex_init(IntPtr vdfs, string name);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern void zg_tex_deinit(IntPtr tex);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint zg_tex_fourcc(IntPtr tex);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ref DDSURFACEDESC2 zg_tex_header(IntPtr tex);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_tex_payload(IntPtr tex);

        [DllImport("zenglue", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr zg_tex_dds3_decompress(IntPtr tex);

        private IntPtr handle;

        public uint fourcc() {
            return zg_tex_fourcc(handle);
        }

        private void convertDXT3(byte[] data) {

        }

        private string _n;
        public ZTexture(VDFS archive, string name) {
            handle = zg_tex_init(archive.NativeHandle(), name);
            if (handle == IntPtr.Zero) throw new Exception("Failed to convert");
            _n = name;
        }

        public UnityEngine.Texture2D toUnityTexture() {
            var header = zg_tex_header(handle);
            var cc = zg_tex_fourcc(handle);
            if (cc != ddscc)
                throw new Exception("Invalid File CC " + cc);
            if ((header.ddpfPixelFormat.dwFlags & 4) == 0) 
                throw new Exception("FOURCC not specified");

            bool isUncompressed = header.ddpfPixelFormat.dwFourCC == dxt3cc;
            UnityEngine.TextureFormat format;
 
            if (isUncompressed)
                format = UnityEngine.TextureFormat.RGBA32;
            else format = header.ddpfPixelFormat.dwFourCC switch {
                dxt1cc => UnityEngine.TextureFormat.DXT1,
                dxt5cc => UnityEngine.TextureFormat.DXT5,
                _ => throw new Exception("Invalid DDS cc " + header.ddpfPixelFormat.dwFourCC.ToString())
            };

            if (header.dwMipMapCount == 0)
                ++header.dwMipMapCount; // one funky tree in the colony seems to not have any defined...

            var texture = new UnityEngine.Texture2D(
                (int)header.dwWidth,
                (int)header.dwHeight,
                format,
                isUncompressed ? 1 : (int)header.dwMipMapCount, 
                false
            );
            texture.filterMode = UnityEngine.FilterMode.Trilinear;
            if (isUncompressed)
            {
                var raw = zg_tex_dds3_decompress(handle);
                texture.LoadRawTextureData(raw, (int)(header.dwHeight * header.dwWidth * 4));
                zg_tex_deinit(raw);

            } else
            {
                bool isLinear = (header.dwFlags & 0x00080000) > 0; // never a case in g1
                uint currentSize = header.dwPitchOrLinearSize;
                if (!isLinear) currentSize *= header.dwHeight;
                uint size = 0;

                for (uint i = 0; i < header.dwMipMapCount; ++i)
                {
                    size += currentSize;
                    currentSize /= 4;
                }
                if (size == 0) throw new Exception("");
                texture.LoadRawTextureData(zg_tex_payload(handle), (int)size);
            }
            texture.Apply();
            return texture;
        }

        public void Dispose() {
            zg_tex_deinit(handle);
        }
    }
}