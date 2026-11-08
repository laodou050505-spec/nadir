using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>输入来源：真机手眼、编辑器键鼠、自动机器人共用一个接口</summary>
    public abstract class InputSource : MonoBehaviour
    {
        public abstract PlayerInput Poll(float dt);
        public virtual bool EyeTracked => false;
        public virtual bool HandsTracked => false;
        public static V3 ToV(Vector3 v) => new V3(v.x, v.y, v.z);
        public static Vector3 ToU(V3 v) => new Vector3((float)v.x, (float)v.y, (float)v.z);
    }
}
