using UnityEngine;

namespace zen4unity {

    class WaynetRenderer: MonoBehaviour {

        public ZenGlue.Waynet waynet;
        public bool enablePreview;

        void OnDrawGizmos() {
            if (enablePreview && waynet != null) {
                Gizmos.color = new Color(1, 1, 1, 0.2f);
                foreach (var e in waynet.edges)
                    Gizmos.DrawLine(waynet.nodes[e.n1].position, waynet.nodes[e.n2].position);
            }
        }   
    }
}
