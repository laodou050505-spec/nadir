using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>Play 模式自动测试：机器人按规则玩，相机跟着它走</summary>
    public class BotInput : InputSource
    {
        public Camera cam; public GameRoot root;
        BotBrain bot; Game bound; PlayerInput last;
        public override PlayerInput Poll(float dt)
        {
            if (root.game == null) return default;
            if (bound != root.game || bot == null || root.game.time < .01) { bound = root.game; bot = new BotBrain(root.game, BotBrain.Normal, 7); }
            last = bot.Step(dt);
            bot.After();
            cam.transform.localPosition = ToU(last.headPos);
            var f = ToU(last.headFwd);
            if (f.sqrMagnitude > .01f) cam.transform.rotation = Quaternion.LookRotation(f);
            return last;
        }
    }
}
