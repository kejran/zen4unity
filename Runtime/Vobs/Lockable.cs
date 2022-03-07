using UnityEngine;

namespace zen4unity.Vob 
{

    [RequireComponent(typeof(Interactable))]
    public class Lockable : MonoBehaviour
    {
        //public bool locked = false;
        public string keyId;
        public string lockCode;
    }

}