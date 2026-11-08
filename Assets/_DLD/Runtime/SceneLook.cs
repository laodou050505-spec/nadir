using UnityEngine;

namespace DLD
{
    /// <summary>场景全局外观：亮度、雾、虚拟灯、竖井参数。编辑器里也生效，方便截图。</summary>
    [ExecuteAlways]
    public class SceneLook : MonoBehaviour
    {
        [Range(0, 1)] public float level = 1;
        public Color fogColor = new Color(0.012f, 0.022f, 0.024f);
        public float fogDensity = 0.05f;
        public Vector3 lampA = new Vector3(0, 2.8f, 0), lampB = new Vector3(0, 2.8f, 0);
        public Color lampColorA = new Color(0.62f, 0.8f, 0.76f), lampColorB = Color.black;
        public float lampPowerA = 3.2f, lampPowerB;
        /// <summary>灯管面的闪烁：0 = 全灭，1 = 正常亮度</summary>
        [HideInInspector] public float lampFlicker = 1;
        public float shaftTop, floorHeight = 3f, lampRadius = 1.75f;
        [Range(0, 1)] public float shaftRed;
        public float eyeY = -120;
        public Texture2D ceilMap, noise, wear;
        public float ceilHeight = 4.2f, ceilHalf = 32f;

        static readonly Vector4[] pos = new Vector4[2], col = new Vector4[2];

        void OnEnable() { Shader.SetGlobalFloat("_DLDTile", .75f); Apply(); }
        void Update() => Apply();

        public void Apply()
        {
            Shader.SetGlobalFloat("_DLDLevel", level);
            Shader.SetGlobalColor("_DLDFogColor", fogColor);
            Shader.SetGlobalFloat("_DLDFogDensity", fogDensity);
            pos[0] = new Vector4(lampA.x, lampA.y, lampA.z, lampPowerA * lampFlicker);
            pos[1] = new Vector4(lampB.x, lampB.y, lampB.z, lampPowerB);
            col[0] = lampColorA; col[1] = lampColorB;
            Shader.SetGlobalVectorArray("_DLDLampPos", pos);
            Shader.SetGlobalVectorArray("_DLDLampCol", col);
            Shader.SetGlobalVector("_DLDShaft", new Vector4(shaftTop, floorHeight, lampRadius, shaftRed));
            Shader.SetGlobalFloat("_DLDEyeY", eyeY);
            if (ceilMap) Shader.SetGlobalTexture("_DLDCeilMap", ceilMap);
            if (noise) Shader.SetGlobalTexture("_DLDNoise", noise);
            if (wear) Shader.SetGlobalTexture("_DLDWear", wear);
            Shader.SetGlobalVector("_DLDCeil", new Vector4(ceilHeight, ceilHalf));
        }
    }
}
