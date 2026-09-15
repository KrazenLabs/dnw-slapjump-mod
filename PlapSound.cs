using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace SlapJump
{
    // Launching plays the slap sound with extra effects
    internal static class PlapSound
    {
        private struct HeldSound
        {
            public AudioResource Resource;
            public Vector3 Point;
        }

        private static readonly List<HeldSound> Held = new List<HeldSound>();
        private static bool _holding;

        public static void BeginHandUpdate()
        {
            _holding = true;
        }

        public static bool TryHold(AudioResource resource, Vector3 point)
        {
            if (!_holding) return false;
            Held.Add(new HeldSound { Resource = resource, Point = point });
            return true;
        }

        public static void PlayLaunch(float pitch, float extraVolume)
        {
            foreach (var sound in TakeHeld()) SoundPlayer.Play(sound.Resource, sound.Point, pitch, extraVolume);
        }

        public static void EndHandUpdate()
        {
            _holding = false;
            foreach (var sound in TakeHeld()) AudioHelper.PlaySFXAtPoint(sound.Resource, sound.Point);
        }

        private static HeldSound[] TakeHeld()
        {
            var sounds = Held.ToArray();
            Held.Clear();
            return sounds;
        }
    }

    [HarmonyPatch(typeof(PlapperHand), "UpdatePlapper")]
    internal static class PlapperHand_UpdatePlapper_Patch
    {
        private static void Prefix()
        {
            var mod = SlapJumpMod.Instance;
            if (mod != null && mod.Enabled) PlapSound.BeginHandUpdate();
        }

        private static void Finalizer()
        {
            PlapSound.EndHandUpdate();
        }
    }

    [HarmonyPatch(typeof(AudioHelper), nameof(AudioHelper.PlaySFXAtPoint))]
    internal static class AudioHelper_PlaySFXAtPoint_Patch
    {
        private static bool Prefix(AudioResource resource, Vector3 point)
        {
            return !PlapSound.TryHold(resource, point);
        }
    }
}
