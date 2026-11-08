using UnityEngine;
using UnityEngine.XR;

namespace DLD.Play
{
    /// <summary>选输入来源：头显上用手眼，编辑器用键鼠；自动测试时切机器人</summary>
    public class InputSwitch : MonoBehaviour
    {
        public GameRoot root; public EditorInput editor; public PicoInput pico; public BotInput bot;
        public static bool UseBot;
        void Awake()
        {
            bool device = !Application.isEditor;
#if UNITY_EDITOR
            UseBot = UnityEditor.EditorPrefs.GetBool("DLD_BotPlay", false);
#endif
            editor.enabled = !device; pico.enabled = device; bot.enabled = UseBot;
            root.input = UseBot ? bot : device ? pico : editor;
        }
    }
}
