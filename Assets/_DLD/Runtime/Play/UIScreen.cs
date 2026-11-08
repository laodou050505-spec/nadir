using UnityEngine;

namespace DLD.Play
{
    /// <summary>一屏界面：统一开合。元素的先后顺序写在各自的 delay 里</summary>
    public class UIScreen : MonoBehaviour
    {
        UIItem[] items;
        public bool Open { get; private set; }
        void Collect() { items ??= GetComponentsInChildren<UIItem>(true); }

        /// <param name="extra">整屏提前多少秒（负数）：翻页时不用再等片头的节奏</param>
        public void Show(float extra = 0)
        {
            Collect(); Open = true; gameObject.SetActive(true);
            foreach (var i in items) i.Show(true, extra);
        }

        public void Hide() { Collect(); Open = false; foreach (var i in items) i.Show(false); }
        public void HideNow() { Hide(); foreach (var i in items) i.Snap(); gameObject.SetActive(false); }

        public void Tick(float dt)
        {
            if (!gameObject.activeSelf) return;
            bool gone = true;
            foreach (var i in items) { i.Tick(dt); gone &= i.Gone; }
            if (gone && !Open) gameObject.SetActive(false);
        }
        public bool Gone => !gameObject.activeSelf;
    }
}
