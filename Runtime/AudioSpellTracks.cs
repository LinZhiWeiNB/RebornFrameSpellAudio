using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebornFrame.Spell.Audio
{
    /// <summary>Unity 音效轨道种类，作者 0、本地编号 3。</summary>
    public static class AudioSpellTracks
    {
        /// <summary>注册给技能包的轨道种类。</summary>
        public static readonly SpellPresentationTrackId TypeId = SpellPresentationTrackId.Combine(0, 3);

        private static readonly Dictionary<string, AudioClip> Clips = new();
        private static readonly Dictionary<ulong, Transform> Anchors = new();

        /// <summary>登记单位身上的发声位置。空间音效会用到。</summary>
        /// <param name="stableId">单位稳定 ID。</param>
        /// <param name="anchor">发声变换。</param>
        public static void BindAnchor(ulong stableId, Transform anchor)
        {
            if (stableId == 0) throw new ArgumentException("稳定 ID 不能为 0。", nameof(stableId));

            if (anchor == null) Anchors.Remove(stableId);
            else Anchors[stableId] = anchor;
        }

        /// <summary>
        ///     预先登记音效。预备时优先使用这里的片段，不再从 Resources 加载。
        /// </summary>
        /// <param name="key">与轨道参数里的 clip_key 相同的键。</param>
        /// <param name="clip">音频片段。</param>
        public static void RegisterClip(string key, AudioClip clip)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("音效键不能为空。", nameof(key));

            if (clip == null) throw new ArgumentNullException(nameof(clip));

            Clips[key] = clip;
        }

        /// <summary>向技能包注册音效轨道。重复调用没有效果。</summary>
        public static void EnsureRegistered()
        {
            if (SpellPresentation.IsRegistered(TypeId)) return;

            SpellPresentation.Register(TypeId, new Factory());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad()
        {
            EnsureRegistered();
        }

        private sealed class Factory : ISpellPresentationTrackFactory
        {
            public void Prepare(ReadOnlySpan<byte> parameters)
            {
                var config = AudioSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (string.IsNullOrEmpty(config.ClipKey)) throw new ArgumentException("音效轨道没有片段键。");

                if (Clips.ContainsKey(config.ClipKey)) return;

                var clip = Resources.Load<AudioClip>(config.ClipKey);
                if (clip == null) throw new ArgumentException($"音效 {config.ClipKey} 不存在。");

                Clips[config.ClipKey] = clip;
            }

            public ISpellPresentationTrack Create(ReadOnlySpan<byte> parameters, in SpellPresentationContext context)
            {
                var config = AudioSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (!Clips.TryGetValue(config.ClipKey, out var clip))
                    throw new InvalidOperationException($"音效 {config.ClipKey} 还没有预备。");

                Anchors.TryGetValue(context.OwnerStableId, out var anchor);
                var host = new GameObject("SpellAudio");
                host.SetActive(false);
                var source = host.AddComponent<AudioSource>();
                source.clip = clip;
                source.volume = config.Volume <= 0f ? 1f : config.Volume;
                source.spatialBlend = config.Spatial ? 1f : 0f;
                if (anchor != null) host.transform.position = anchor.position;

                return new Track(host, source);
            }
        }

        private sealed class Track : ISpellPresentationTrack
        {
            private readonly GameObject _host;
            private readonly AudioSource _source;

            public Track(GameObject host, AudioSource source)
            {
                _host = host;
                _source = source;
            }

            public void Play()
            {
                if (_host == null) return;

                _host.SetActive(true);
                _source.Play();
            }

            public void Stop(SpellEndReason reason)
            {
                if (_host == null) return;

                _source.Stop();
                if (Application.isPlaying) UnityEngine.Object.Destroy(_host);
                else UnityEngine.Object.DestroyImmediate(_host);
            }
        }
    }
}
