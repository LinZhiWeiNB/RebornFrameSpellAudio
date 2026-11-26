using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace RebornFrame.Spell.Audio.Tests
{
    public class AudioSpellTrackTests
    {
        private readonly List<Object> _objects = new();
        private SpellStartup _startup;
        private World _world;

        [TearDown]
        public void TearDown()
        {
            _startup?.Dispose();
            if (_world is { IsCreated: true }) _world.Dispose();
            SpellPresentation.Reset();
            for (var index = 0; index < _objects.Count; index++)
                if (_objects[index] != null)
                    Object.DestroyImmediate(_objects[index]);
        }

        [Test]
        public void Play_UsesPreparedClip()
        {
            var clip = AudioClip.Create("hit", 8, 1, 44100, false);
            _objects.Add(clip);
            AudioSpellTracks.RegisterClip("hit", clip);
            AudioSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);

            _world = new World(nameof(Play_UsesPreparedClip));
            _startup = new SpellStartup();
            var spell = SpellId.Combine(1, 1);
            var data = new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Active,
                        DurationFrames = 4,
                        PresentationTracks =
                        {
                            new SpellPresentationTrack
                            {
                                TypeId = AudioSpellTracks.TypeId.Value,
                                Parameters = new AudioSpellTrackConfig { ClipKey = "hit", Volume = 0.5f }.ToByteString()
                            }
                        }
                    }
                }
            };
            _startup.AddModuleData(data);
            _startup.Start(_world);
            var unit = _world.EntityManager.CreateEntity();
            _startup.InitializeUnit(unit, 1);
            Assert.IsTrue(SpellAccess.TryBeginCast(_world.EntityManager, spell, 1));
            _startup.Tick(0);

            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            AudioSource playing = null;
            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index].gameObject.name != "SpellAudio") continue;

                _objects.Add(sources[index].gameObject);
                playing = sources[index];
            }

            Assert.IsNotNull(playing);
            Assert.AreEqual(clip, playing.clip);
            Assert.AreEqual(0.5f, playing.volume);
            Assert.IsTrue(playing.gameObject.activeSelf);
        }
    }
}
