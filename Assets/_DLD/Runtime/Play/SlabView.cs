using UnityEngine;

namespace DLD.Play
{
    /// <summary>一块地砖的表现部件：整块 + 预先切好的碎块。场景搭建时写入，运行时由 TileField 驱动</summary>
    public class SlabView : MonoBehaviour
    {
        public MeshRenderer whole;
        public MeshRenderer[] pieces;
        public Vector2[] centers;
        public Texture2D crack;
    }
}
