using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 声音导演：事件 → 带方位的音效；再加几条环境循环：底噪、灯管嗡鸣、井底的风（破洞越多越响）、传送门低鸣、心跳。
    /// 恐惧主要靠声音，但绝不突然：所有响的声音都有铺垫，音量在合成时就压住了
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public AudioClip[] clips; public Transform[] humPoints;
        readonly Dictionary<string, List<AudioClip>> map = new();
        readonly List<AudioSource> pool = new(); int next;
        AudioSource tear, ambA, ambB, wind, abyss, portal, tentacle, box, drone, strings, choir, high, pulse; readonly List<AudioSource> hums = new();
        float tension, boxNext = 35;
        readonly List<(float at, string name, Vector3 pos, float gain)> later = new();
        float time, heartNext; string ambMood;
#if UNITY_EDITOR
        public static readonly Dictionary<string, int> Count = new();
#endif

        void Awake()
        {
            foreach (var c in clips)
            {
                var key = c.name.TrimEnd('0', '1', '2', '3');
                if (!map.TryGetValue(key, out var l)) map[key] = l = new();
                l.Add(c);
            }
            for (int i = 0; i < 28; i++) pool.Add(Src("sfx" + i, null, false, 1));
            ambA = Src("ambA", null, true, 0); ambB = Src("ambB", null, true, 0); wind = Src("wind", Get("wind"), true, 0);
            abyss = Src("abyss", Get("abyss_loop"), true, 1); abyss.minDistance = 4; abyss.maxDistance = 30; abyss.transform.position = Vector3.down * .5f;
            drone = Stem("m_drone"); strings = Stem("m_strings"); choir = Stem("m_choir"); high = Stem("m_high"); pulse = Stem("m_pulse");
            box = Src("musicbox", Get("musicbox"), false, 1); box.minDistance = 5; box.maxDistance = 45;
            portal = Src("portal", Get("portal_loop"), true, 1); tear = Src("tear", Get("tear_loop"), true, 1); tentacle = Src("tentacle", Get("tentacle_loop"), true, 1);
            if (humPoints != null) foreach (var p in humPoints)
                {
                    var h = Src("hum", Get("hum_loop"), true, 1); h.transform.position = p.position; h.minDistance = 1; h.maxDistance = 7; h.volume = .1f;
                    h.time = Random.value * 5; h.Play(); hums.Add(h);
                }
        }

        AudioSource Src(string n, AudioClip c, bool looped, float spatial)
        {
            var g = new GameObject(n); g.transform.SetParent(transform, false);
            var a = g.AddComponent<AudioSource>(); a.clip = c; a.loop = looped; a.playOnAwake = false; a.spatialBlend = spatial; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = .8f; a.maxDistance = 28; a.dopplerLevel = 0;
            return a;
        }

        AudioSource Stem(string n) { var a = Src(n, Get(n), true, 0); a.volume = 0; a.time = 0; a.Play(); return a; }

        AudioClip Get(string name) { if (map.TryGetValue(name, out var l) && l.Count > 0) return l[Random.Range(0, l.Count)]; return null; }

        public void Play(string name, Vector3? pos = null, float gain = 1, float pitchVar = .06f, float pitch = 1, float offset = 0, float maxDur = 0, float minD = .8f)
        {
            var c = Get(name); if (!c) { Debug.LogWarning("缺音效 " + name); return; }
#if UNITY_EDITOR
            Count.TryGetValue(name, out int cn); Count[name] = cn + 1;
#endif
            var a = pool[next]; next = (next + 1) % pool.Count;
            a.Stop(); a.clip = c; a.volume = gain; a.pitch = pitch * (1 + Random.Range(-pitchVar, pitchVar));
            a.ignoreListenerPause = AudioListener.pause; a.spatialBlend = pos.HasValue ? 1 : 0; a.minDistance = minD; if (pos.HasValue) a.transform.position = pos.Value;
            a.time = Mathf.Min(offset, c.length - .05f);
            a.Play();
            if (maxDur > 0) a.SetScheduledEndTime(AudioSettings.dspTime + maxDur);
        }

        static Vector3 P(Tile t) => new Vector3((float)t.x, 0, (float)t.z);
        static Vector3 U(V3 v) => InputSource.ToU(v);

        /// <summary>菜单里只有贴着胸口的低频和风声</summary>
        /// <summary>结局：医院里所有的声音都慢慢退掉，只剩一点室外的风</summary>
        public void Ending()
        {
            tear.Stop(); portal.Stop(); tentacle.Stop();
            foreach (var h in hums) h.Stop();
        }

        public void TickEnding(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * .6f);
            foreach (var s in new[] { drone, strings, choir, high, pulse, abyss, ambA, ambB }) s.volume = Mathf.Lerp(s.volume, 0, k);
            wind.volume = Mathf.Lerp(wind.volume, 0, k);      // 医院的风声也退掉，只留结局自己的鸟叫和微风
        }

        public void TickMenu(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * .8f);
            drone.volume = Mathf.Lerp(drone.volume, .42f, k); strings.volume = Mathf.Lerp(strings.volume, .1f, k);
            choir.volume = Mathf.Lerp(choir.volume, 0, k); high.volume = Mathf.Lerp(high.volume, 0, k); pulse.volume = Mathf.Lerp(pulse.volume, 0, k);
            abyss.volume = Mathf.Lerp(abyss.volume, 0, k); tear.volume = 0; portal.volume = 0; tentacle.volume = 0;
            if (!wind.isPlaying) wind.Play();
            wind.volume = Mathf.Lerp(wind.volume, .1f, k);
        }

        public void Layer(Game g)
        {
            bool horror = g.mood == "horror";
            if (ambMood == g.mood) return;
            ambMood = g.mood;
            ambA.clip = Get(horror ? "bleak" : "void_dread"); ambB.clip = Get(horror ? "atmos_sub" : "harmonic");
            ambA.volume = 0; ambB.volume = 0; ambA.Play(); ambB.Play(); wind.Play(); abyss.Play();
        }

        public void On(GameEvent e, Game g)
        {
            switch (e.type)
            {
                case "crack": Play("crack", P(e.tile), .8f); Play("creak", P(e.tile), .4f, .2f, 1.2f, Random.value * 1.4f, .7f); break;
                case "fall":
                    Play("fall", P(e.tile), .85f); Play("rocks", P(e.tile) + Vector3.down, .4f, .1f, .8f, Random.value * 15, 1.3f);
                    later.Add((time + 3.2f + Random.value * 1.6f, "fall_echo", P(e.tile) + Vector3.down * 12, .5f));   // 好几秒后才传来回响：这口井到底有多深
                    break;
                case "rise": Play("rise", P(e.tile), .5f); break;
                case "restore": Play("restore", P(e.tile), .6f); break;
                case "stayWarn": Play("stay", P(e.tile), .6f); break;
                case "shardSpawn": Play("shard_spawn", U(e.shard.pos), .6f); break;
                case "shard": Play("shard_get", U(e.shard.pos), .8f); break;
                case "shardFade": Play("shard_fade", U(e.shard.pos), .6f); break;
                case "shardLock": Play("gaze_lock", U(e.shard.pos), .5f); break;
                case "doorSpawn":
                    Play("tear_crack", P(e.tile), .8f, 0);
                    portal.transform.position = P(e.tile) + Vector3.up * 1.2f; portal.volume = 0; portal.Play();
                    break;
                case "doorPull": tear.transform.position = portal.transform.position; tear.volume = 0; tear.Play(); break;
                case "doorReady": tear.Stop(); Play("portal_open", P(e.tile), .9f, 0); break;
                case "clear": Play("teleport", null, .85f, 0); portal.Stop(); break;
                case "handSpawn": Play("tentacle_omen", U(e.hand.anchor), .95f, .04f, 1, 0, 0, 4); break;     // 预警要听得清，不能悄悄冒出来
                case "handPhase":
                    if (e.phase == "rise") Play("tentacle_rise", U(e.hand.anchor), .95f, .05f, 1, 0, 0, 4);
                    else if (e.phase == "reach") Play("tentacle_lunge", U(e.hand.pos), .7f, .05f, 1, 0, 0, 3);
                    else if (e.phase == "recoil") Play("tentacle_back", U(e.hand.anchor), .7f, .08f, 1, 0, 0, 3);
                    break;
                case "grab": Play("grab", U(e.hand.pos), .9f, 0, 1, 0, 0, 3); break;
                case "swat": Play("cast", U(e.hand.pos), e.killed ? .6f : .42f, .05f, 1.25f, 0, 0, 3); Play(e.killed ? "swat_kill" : "swat", U(e.hand.pos), .85f, .06f, 1, 0, 0, 3); if (e.killed) Play("swat", U(e.hand.pos), .7f, .06f, 1, 0, 0, 3); break;
                case "eye":
                    if (e.phase == "warn") { Play("eye_warn", null, .75f, 0); Play("swell", null, .6f, 0); }
                    else if (e.phase == "open") Play("eye_open", null, .7f, 0);
                    else if (e.phase == "closing") Play("eye_close", null, .5f);
                    break;
                case "seenStart": Play("seen", null, .4f); break;
                case "spotted": Play("spotted", null, .75f, 0); break;
                case "fall_player": Play("fall_me", null, .6f, 0); if (g.fallSeq > 2.3) Play(g.fallSeq > 3.5 ? "fall_long" : "fall_mid", null, .5f, 0); break;
                case "wake": Play("wake", null, .5f, 0); break;
                case "lost": Play("lost", null, .6f, 0); break;
                case "oob": Play("oob", null, .5f, 0); break;
                case "eyeSwat": Play("cast", null, .7f, .04f); Play("eye_recoil", null, .85f, .05f); break;
                case "scare": Scare(e); break;
            }
        }

        // ───────── 黑影的脚步：走动时一直听得到，这才是回头的理由 ─────────
        class FigSound { public float x, z, next; public bool warned; }
        readonly Dictionary<int, FigSound> figs = new();
        void FigureSteps(float dt, Game g, Vector3 head)
        {
            foreach (var f in g.scares.effigies)
            {
                if (!f.placed) continue;
                if (!figs.TryGetValue(f.id, out var s)) { figs[f.id] = s = new FigSound { x = (float)f.x, z = (float)f.z, next = Random.value * 1.5f }; }
                float dx = (float)f.x - s.x, dz = (float)f.z - s.z, moved = Mathf.Sqrt(dx * dx + dz * dz); s.x = (float)f.x; s.z = (float)f.z;
                var pos = new Vector3((float)f.x, .1f, (float)f.z); float dist = Vector3.Distance(pos, new Vector3(head.x, .1f, head.z));
                float speed = moved / Mathf.Max(dt, .001f);
                // 走进 6 米之内：先有一声很低的气息，告诉玩家它来了；走远了才会再响
                if (dist < 6 && !s.warned) { s.warned = true; Play("fig_near", pos + Vector3.up * 1.2f, .85f, .06f, 1, 0, 0, 3.5f); }
                else if (dist > 7.5f) s.warned = false;
                s.next -= dt;
                if (speed < .004f || speed > 1.5f || dist > 14 || s.next > 0) continue;
                // 在走廊里走得快，脚步密；挤到身边时几乎不动，隔很久才拖一步
                s.next = speed > .12f ? 1.15f + Random.value * .25f : 2.6f + Random.value * 1.1f;
                Play("fig_step", pos, Mathf.Lerp(.95f, .55f, dist / 14), .06f, 1, 0, 0, 3.2f);
            }
        }

        void Scare(GameEvent e)
        {
            var p = U(e.pos);
            switch (e.kind)
            {
                case "omen":
                    if (e.what == "whisper") Play("omen_whisper", p, .5f);
                    else if (e.what == "knock") Play("omen_knock", p, .5f);
                    else if (e.what == "beep") Play("omen_beep", p, .4f);
                    else if (e.what == "blackout") { Play("omen_black", null, .5f, 0); Play("swell", null, .4f, 0, 1.15f); }
                    else if (e.what == "footprints") for (int i = 0; i < 3; i++) later.Add((time + i * 1.1f, "footsteps", p, .15f + i * .08f));
                    break;
                case "whisper": Play("whisper", p, .6f); Play("presence", p, .25f, .1f, .8f); break;
                case "knock": Play("knock", p, .7f); break;
                case "beep": Play("beep", p, .5f, 0); break;
                case "step": Play("footsteps", p, .45f, .05f, .75f, 0, .32f); break;
                case "stepsEnd": Play("steps_end", p, .5f); break;
                case "rustle": break;     // 由 FigureSteps 统一负责
                case "blackout": Play("blackout", null, .5f, 0); break;
                case "push": Play("cast", p, .85f, .04f, 1, 0, 0, 3); Play("figure_push", p, .6f, .06f, 1, 0, 0, 3); break;
            }
        }

        /// <summary>
        /// 配乐按危险程度一层层叠上去：平时只有贴着胸口的低频，
        /// 黑影挤近、层数变深、眼睛要睁开时，弦乐、合唱、尖细泛音和脉动才进来。音量都是慢慢涨的，不会突然
        /// </summary>
        void Music(float dt, Game g, Vector3 head)
        {
            float nearest = 9;
            foreach (var f in g.scares.effigies)
                if (f.placed) nearest = Mathf.Min(nearest, Mathf.Sqrt((float)((f.x - g.feet.x) * (f.x - g.feet.x) + (f.z - g.feet.z) * (f.z - g.feet.z))));
            float t = .16f + g.layerIdx * .055f + (g.mood == "horror" ? .12f : 0);
            t += Mathf.Clamp01(1 - (nearest - 1) / 3.2f) * .38f;
            t += g.eye.phase == "open" ? .4f : g.eye.phase == "warn" ? (float)(g.eye.t / g.eye.dur) * .3f : 0;
            if (g.grabbed != null) t += .3f;
            if (g.scares.black > 0) t += .3f;
            if (g.standing == null) t += .2f; else if (g.standing.state == TileState.Crack) t += .12f;
            if (g.door != null) t -= .2f;                       // 门开了：配乐让出来，只留一点希望
            tension = Mathf.Lerp(tension, Mathf.Clamp01(t), 1 - Mathf.Exp(-dt * (t > tension ? .7f : .3f)));
            float S(float a, float b) => Mathf.SmoothStep(0, 1, (tension - a) / (b - a));
            drone.volume = .5f + .45f * tension;
            strings.volume = S(.12f, .55f) * .85f;
            choir.volume = S(.28f, .72f) * .8f;
            high.volume = S(.5f, .95f) * .5f;
            pulse.volume = S(.42f, .95f) * .7f;
            // 走调的八音盒：隔一阵从很远的走廊传来
            if (time >= boxNext)
            {
                boxNext = time + Random.Range(55f, 110f);
                if (g.eye.phase == "closed" && g.grabbed == null)
                {
                    var rot = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);
                    box.transform.position = rot * new Vector3(0, 1.2f, 18); box.volume = .6f; box.pitch = Random.Range(.94f, 1f); box.Play();
                }
            }
        }

        public void Tick(float dt, Game g, Vector3 head, Vector3 fwd)
        {
            FigureSteps(dt, g, head);
            time += dt;
            for (int i = later.Count - 1; i >= 0; i--)
                if (later[i].at <= time) { var l = later[i]; Play(l.name, l.pos, l.gain, .05f, l.name == "footsteps" ? .7f : 1, 0, l.name == "footsteps" ? .32f : 0); later.RemoveAt(i); }

            float K(float rate) => 1 - Mathf.Exp(-dt * rate);
            ambA.volume = Mathf.Lerp(ambA.volume, .2f, K(.5f));
            ambB.volume = Mathf.Lerp(ambB.volume, .16f, K(.5f));
            Music(dt, g, head);
            wind.volume = Mathf.Lerp(wind.volume, .1f, K(.5f));
            // 破洞越多，井底的风越响；眼睛睁开时风停，只剩低鸣
            int holes = 0; foreach (var t in g.floor.tiles) if (t.state == TileState.Hole || t.state == TileState.Fall) holes++;
            float ab = Mathf.Lerp(.12f, .85f, Mathf.Clamp01(holes / 10f)) * (g.eye.phase == "open" ? .35f : 1);
            abyss.volume = Mathf.Lerp(abyss.volume, ab, K(1.2f));
            // 灯跟着全场亮度走，熄灯时嗡鸣也断
            float lamp = Mathf.Clamp01(Shader.GetGlobalFloat("_DLDLevel") * 1.4f);
            foreach (var h in hums) h.volume = .1f * lamp;
            portal.volume = Mathf.Lerp(portal.volume, g.door != null ? (.15f + (float)g.door.open * .5f) : 0, K(1.5f));
            tear.volume = Mathf.Lerp(tear.volume, g.door != null && g.door.pulling ? .7f : 0, K(5));
            // 触手在你附近时，有干纤维摩擦的声音跟着它
            AbyssHand near = null; float bd = 9;
            foreach (var h in g.hands) if (h.phase != "omen") { float d = (U(h.pos) - head).magnitude; if (d < bd) { bd = d; near = h; } }
            if (near != null) { tentacle.transform.position = U(near.pos); if (!tentacle.isPlaying) tentacle.Play(); tentacle.minDistance = 3.5f; tentacle.volume = Mathf.Lerp(tentacle.volume, .85f, K(3)); }
            else { tentacle.volume = Mathf.Lerp(tentacle.volume, 0, K(4)); if (tentacle.volume < .01f && tentacle.isPlaying) tentacle.Stop(); }
            // 心跳：脚下危险、被盯着、被抓住时加快
            float danger = g.grabbed != null ? 1 : g.eye.phase == "open" ? .8f : g.eye.phase == "warn" ? .55f : g.standing == null ? .9f : g.standing.state == TileState.Crack ? .7f : g.scares.black > 0 ? .7f : 0;
            if (danger > 0 && time >= heartNext) { Play("heart", null, .35f + danger * .45f, 0); heartNext = time + 60f / Mathf.Lerp(62, 118, danger); }
        }
    }
}
