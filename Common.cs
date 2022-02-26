using System.Collections;
using System.Collections.Generic;
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
}