using UnityEngine;

namespace GameUp.SDK
{
    [CreateAssetMenu(fileName = "RemoteExtraData", menuName = "Data/Remote/ExtraData", order = 0)]
    public class RemoteExtraData : ScriptableObject
    {
        public int wave_start_show_inters = 3;
    }
}