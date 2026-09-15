using System;
using System.Globalization;
using DnWModLoader;
using DnWModLoader.Config;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlapJump
{
    public sealed class SlapJumpMod : Mod
    {
        private const float LandingGraceSeconds = 0.2f;
        // OOB protection
        private const float MaxFlightSeconds = 10f;
        private const float ShakeAmplitude = 0.3f;
        private const float ShakeDurationSeconds = 0.3f;
        private const float FullSoundSpeed = 16f;
        private const float FullSoundExtraVolume = 0.8f;
        private const float FullSoundPitch = 0.75f;

        internal static SlapJumpMod Instance { get; private set; }

        internal static bool InFlight { get; private set; }

        internal bool Enabled { get { return _enabled.Value; } }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _power;
        private ConfigEntry<float> _upwardBias;
        private ConfigEntry<bool> _stackSpeed;
        private ConfigEntry<bool> _midAirSlaps;
        private ConfigEntry<bool> _dragons;
        private ConfigEntry<float> _cooldown;

        private LocomotionController _locomotion;
        private MassSpringController _body;
        private float _launchedAt = float.NegativeInfinity;

        public override void OnInitialize()
        {
            Instance = this;
            Config.DescribeSection("Launch", "Slap Jump", "Slap things so hard you can launch yourself!");
            _enabled = Config.Bind("Launch", "Enabled", true, "Toggle the mod on or off");
            _power = Config.Bind("Launch", "Power", 8f, "Launch speed", ConfigMeta.Range(1, 25, 0.5));
            _upwardBias = Config.Bind("Launch", "UpwardBias", 0.35f, "Changes how vertical the launch is", ConfigMeta.Range(0, 1, 0.05));
            _stackSpeed = Config.Bind("Launch", "StackSpeed", false, "Allow consecutive launches to add their speed");
            _midAirSlaps = Config.Bind("Launch", "MidAirSlaps", true, "Allow launches while airborne, e.g. to slap your way up a wall");
            _dragons = Config.Bind("Launch", "Dragons", true, "Whether slapping a dragon launches");
            _cooldown = Config.Bind("Launch", "Cooldown", 0.2f, "Launch cooldown", new ConfigMeta { Min = 0, Max = 2, Step = 0.05, Advanced = true });
            Logger.Info("Initialized.");
        }

        public override void OnGameStarted()
        {
            PlapperHand.PlapEvent += OnPlap;
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _locomotion = null;
            _body = null;
            InFlight = false;
        }

        public override void OnFixedUpdate()
        {
            if (!InFlight) return;
            float flightTime = Time.time - _launchedAt;
            if (_locomotion == null || flightTime > MaxFlightSeconds || (flightTime > LandingGraceSeconds && _locomotion.IsGrounded)) InFlight = false;
        }

        private void OnPlap(Collider collider, Vector3 position, Vector3 normal)
        {
            try
            {
                if (CanLaunch(collider)) Launch(collider, normal);
            }
            catch (Exception e)
            {
                Logger.Exception(e, "Slap handling failed");
            }
        }

        private bool CanLaunch(Collider collider)
        {
            if (!_enabled.Value || collider == null || !TryFindPlayer()) return false;
            // Debounce
            if (!MenuManager.actions.Player.Plap.IsPressed()) return false;
            if (_locomotion.HasCutscene || (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)) return false;
            if (Time.time - _launchedAt < _cooldown.Value) return false;
            if (!_midAirSlaps.Value && !_locomotion.IsGrounded) return false;
            if (!_dragons.Value && collider.GetComponentInParent<WalkNWashDragonDescriptor>() != null) return false;
            return true;
        }

        private void Launch(Collider collider, Vector3 normal)
        {
            Vector3 direction = Vector3.Lerp(normal.normalized, Vector3.up, _upwardBias.Value);
            // Ceiling slap
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;

            float power = _power.Value;
            float boost = _stackSpeed.Value ? power : Mathf.Max(0f, power - Vector3.Dot(_locomotion.SurfaceVelocity, direction));
            if (boost < 0.01f) return;

            _body.ApplyVelocityDelta(direction * boost);
            _launchedAt = Time.time;
            InFlight = true;
            OrbitCameraShake.ApplyShake(ShakeAmplitude, ShakeDurationSeconds);
            float soundStrength = Mathf.Clamp01(boost / FullSoundSpeed);
            PlapSound.PlayLaunch(Mathf.Lerp(1f, FullSoundPitch, soundStrength), FullSoundExtraVolume * soundStrength);
            Logger.Debug("Launched off " + collider.name + " at " + boost.ToString("0.0", CultureInfo.InvariantCulture) + " m/s toward " + direction);
        }

        private bool TryFindPlayer()
        {
            if (_locomotion != null && _body != null) return true;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player == null) return false;
            _locomotion = player.GetComponent<LocomotionController>();
            _body = player.GetComponent<MassSpringController>();
            return _locomotion != null && _body != null;
        }
    }

    // Skip the default slowdown
    [HarmonyPatch(typeof(InteractingMovementMode), nameof(InteractingMovementMode.IsReady))]
    internal static class InteractingMovementMode_IsReady_Patch
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && SlapJumpMod.InFlight) __result = false;
        }
    }
}
