using System;
using UnityEngine;

namespace zen4unity.Vob 
{
    
    [RequireComponent(typeof(Interactable))]
    public class Container : MonoBehaviour
    {
        [Serializable]
        public struct Stack 
        {
            public string id;
            public uint count;

            public override string ToString()
            {
                return id + " (" + count + ")";
            }
        }

        public Stack[] items;

    }
}
