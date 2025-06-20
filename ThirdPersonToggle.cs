using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Zorro.Core;
using Zorro.Settings;
using Zorro.ControllerSupport;
using Photon.Pun;
using System.Linq;

namespace Evaisa.ThirdPersonToggle
{
    [BepInPlugin(GUID, ModName, Version)]
    public class ThirdPersonToggle : BaseUnityPlugin
    {
        public const string GUID = "evaisa.ThirdPersonToggle";
        public const string ModName = "ThirdPersonToggle";
        public const string Version = "1.0.10";

        private ConfigEntry<KeyCode> toggleKey;
        private bool thirdPersonEnabled;
        private MouseSensitivitySetting mouseSensSetting;
        private ControllerSensitivitySetting controllerSensSetting;
        private bool settingsInitialized;

        private readonly float height = 1.5f;
        private readonly float defaultDistance = 3f;
        private float currentDistance;
        private readonly float minDistance = 1f;
        private readonly float maxDistance = 10f;
        private readonly float zoomSpeed = 1f;
        private readonly float lerpRate = 5f;
        private readonly float turnSpeed = 720f;
        private readonly float clipRadius = 0.2f;
        private readonly float clipBuffer = 0.1f;
        private readonly LayerMask clipMask = LayerMask.GetMask("Terrain", "Map");

        void Awake()
        {
            toggleKey = Config.Bind("Camera", "ToggleKey", KeyCode.V, "Key to toggle third-person camera");
            currentDistance = defaultDistance;
            On.MainCameraMovement.LateUpdate += MainCameraMovement_LateUpdate;
            On.MainCameraMovement.CharacterCam += MainCameraMovement_CharacterCam;
            On.CharacterClimbing.TryToStartWallClimb += CharacterClimbing_TryToStartWallClimb;
        }

        private void EnsureSettings()
        {
            if (!settingsInitialized && GameHandler.Instance != null && GameHandler.Instance.SettingsHandler != null)
            {
                mouseSensSetting = GameHandler.Instance.SettingsHandler.GetSetting<MouseSensitivitySetting>();
                controllerSensSetting = GameHandler.Instance.SettingsHandler.GetSetting<ControllerSensitivitySetting>();
                settingsInitialized = true;
            }
        }

        private void MainCameraMovement_LateUpdate(On.MainCameraMovement.orig_LateUpdate orig, MainCameraMovement self)
        {
            if (Input.GetKeyDown(toggleKey.Value))
                thirdPersonEnabled = !thirdPersonEnabled;

            if (thirdPersonEnabled)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                    currentDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }

            orig(self);
        }

        private void MainCameraMovement_CharacterCam(On.MainCameraMovement.orig_CharacterCam orig, MainCameraMovement self)
        {
            if (thirdPersonEnabled && Character.localCharacter != null)
            {
                EnsureSettings();
                var camComp = self.GetComponent<MainCamera>();
                camComp.cam.fieldOfView = self.GetFov();

                Transform torso = Character.localCharacter.GetBodypart(BodypartType.Torso).transform;
                Vector3 lookDir = Character.localCharacter.data.lookDirection;
                if (lookDir == Vector3.zero)
                    lookDir = torso.forward;

                Vector3 desiredPosition = torso.position + Vector3.up * height - lookDir.normalized * currentDistance;
                Vector3 dir = (desiredPosition - torso.position).normalized;
                float maxDist = Vector3.Distance(desiredPosition, torso.position);
                if (Physics.SphereCast(torso.position, clipRadius, dir, out RaycastHit hit, maxDist, clipMask))
                    desiredPosition = hit.point - dir * clipBuffer;

                self.transform.position = Vector3.Lerp(self.transform.position, desiredPosition, Time.deltaTime * lerpRate);

                Quaternion desiredRotation = Quaternion.LookRotation(torso.position - self.transform.position);
                float sens = settingsInitialized
                    ? (InputHandler.GetCurrentUsedInputScheme() == InputScheme.Gamepad
                        ? controllerSensSetting.Value
                        : mouseSensSetting.Value)
                    : 1f;

                self.transform.rotation = Quaternion.RotateTowards(self.transform.rotation, desiredRotation, turnSpeed * sens * Time.deltaTime);
                return;
            }

            orig(self);
        }

        private void CharacterClimbing_TryToStartWallClimb(On.CharacterClimbing.orig_TryToStartWallClimb orig, CharacterClimbing self, bool forceAttempt, Vector3 overide, bool botGrab)
        {
            Transform torso = self.character.GetBodypart(BodypartType.Torso).transform;
            var cam = MainCamera.instance.transform;
            Vector3 oldPos = cam.position;
            cam.position = torso.position;
            orig(self, forceAttempt, overide, botGrab);
            cam.position = oldPos;
        }
    }

    public static class EnumerableExtensions
    {
        public static void ForEachTry<T>(this IEnumerable<T> list, Action<T> action, IDictionary<T, Exception> exceptions = null)
        {
            list.ToList().ForEach(element =>
            {
                try { action(element); }
                catch (Exception e) { exceptions?.Add(element, e); }
            });
        }
    }
}
