using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Zones;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RunawayChimps.Travel
{
    [DefaultExecutionOrder(-100)]
    public sealed class SectorTravelService : MonoBehaviourPunCallbacks
    {
        public static SectorTravelService I { get; private set; }
        public bool IsBusy { get; private set; }
        public SectorId CurrentSector { get; private set; }
        public XRInteractionManager InteractionManager { get; private set; }
        public Shader fadeShader;
        public float fadeSeconds = 0.3f;
        public float operationTimeout = 30f;
        public string LastError { get; private set; }

        private XROrigin origin;
        private GorillaLocomotion.Player player;
        private Rigidbody body;
        private Material fadeMaterial;
        private GameObject fadeQuad;
        private TMP_Text errorText;
        private float errorUntil;
        private float nextTravelTime;
        private bool suspended;
        private bool cancelled;
        private bool committed;
        private bool relocated;
        private Scene source;
        private string destination;
        private ArrivalRoute arrivalRoute;
        private readonly Dictionary<GameObject, bool> targetRoots = new Dictionary<GameObject, bool>();
        private bool loadingPresentation;
        private CameraClearFlags oldClearFlags;
        private Color oldBackground;
        private SectorId oldSector;
        private ZoneId oldZone;
        private SectorId pausedSector;
        private ZoneId pausedZone;
        private Vector3 oldOriginPosition;
        private Quaternion oldOriginRotation;
        private Vector3 oldBodyPosition;
        private Quaternion oldBodyRotation;
        private readonly Dictionary<GameObject, bool> sourceRoots = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Behaviour, bool> frozenBehaviours = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<Collider, bool> frozenColliders = new Dictionary<Collider, bool>();
        private bool frozen;
        private bool wasKinematic;
        private SectorId reconnectSector;
        private ZoneId reconnectZone;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            PhotonNetwork.AutomaticallySyncScene = false;
            InteractionManager = GetComponent<XRInteractionManager>();
            if (InteractionManager == null) InteractionManager = gameObject.AddComponent<XRInteractionManager>();
            SceneManager.sceneLoaded += HideDestinationWhileLoading;
        }

        public bool TravelTo(string sceneName, ArrivalRoute route = ArrivalRoute.Terminal)
        {
            if (IsBusy || suspended || Time.unscaledTime < nextTravelTime) return false;
            if (!PhotonNetwork.InRoom || (AppState.I != null && !AppState.I.IsReady)) return false;
            source = SceneManager.GetActiveScene();
            if (SectorScene.Find(source) == null || source.name == sceneName) return false;
            if (!SectorDestinations.IsSupported(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                ShowError("That destination is not available yet.");
                return false;
            }
            if (!BindRig()) return false;
            destination = sceneName;
            arrivalRoute = route;
            Begin();
            StartCoroutine(RunGuarded(Travel()));
            return true;
        }

        // A future level terminal can bind these methods directly to UnityEvents.
        [ContextMenu("Travel/Return to Hub terminal spawn")]
        public void ReturnToHub() => TravelTo(SectorDestinations.Hub, ArrivalRoute.Terminal);
        public void EnterLevelOne() => TravelTo(SectorDestinations.LevelOne, ArrivalRoute.Terminal);
        public void EnterLevelTwo() => TravelTo(SectorDestinations.LevelTwo, ArrivalRoute.Terminal);

        // Completion is local to this scene visit; never broadcast travel or a group win.
        public bool CompleteLevelOne(KeyBox objective)
        {
            if (objective == null || !objective.travelToLevelTwoOnComplete || !objective.IsComplete ||
                CurrentSector != SectorId.Containment ||
                objective.gameObject.scene != SceneManager.GetActiveScene()) return false;
            return TravelTo(SectorDestinations.LevelTwo, ArrivalRoute.Terminal);
        }

        public bool RespawnAt(Transform spawn)
        {
            if (IsBusy || suspended || !PhotonNetwork.InRoom || spawn == null || !BindRig()) return false;
            source = SceneManager.GetActiveScene();
            if (spawn.gameObject.scene != source) return false;
            destination = source.name;
            Begin();
            StartCoroutine(RunGuarded(Respawn(spawn)));
            return true;
        }

        private bool BindRig()
        {
            player = GorillaLocomotion.Player.Instance;
            origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            body = player != null ? player.GetComponent<Rigidbody>() : null;
            if (origin == null || origin.Camera == null || body == null || player.bodyCollider == null ||
                player.headCollider == null || LevelStreamService.I == null || fadeShader == null)
            {
                Debug.LogError("Sector travel requires the Bootstrap rig, loader, colliders, and fade shader.", this);
                return false;
            }
            EnsureFade();
            return true;
        }

        private void Begin()
        {
            IsBusy = true;
            committed = relocated = cancelled = false;
            LastError = null;
            if (errorText != null) errorText.text = "";
            oldSector = CurrentSector;
            oldZone = ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None;
            oldOriginPosition = origin.transform.position;
            oldOriginRotation = origin.transform.rotation;
            oldBodyPosition = player.transform.position;
            oldBodyRotation = player.transform.rotation;
            sourceRoots.Clear();
            targetRoots.Clear();
            foreach (var root in source.GetRootGameObjects()) sourceRoots[root] = root.activeSelf;
            foreach (var monster in FindObjectsOfType<SectorMonsterSync>()) monster.FlushState();
            Publish(SectorId.None, ZoneId.None);
        }

        private IEnumerator Travel()
        {
            FreezeRig();
            yield return Fade(1);
            SetSourceActive(false);
            var existingTarget = SceneManager.GetSceneByName(destination);
            if (existingTarget.isLoaded) HideDestinationWhileLoading(existingTarget, LoadSceneMode.Additive);
            yield return LevelStreamService.I.LoadForTravel("Loading", operationTimeout);
            CheckConnection();
            ShowLoadingScene(SceneManager.GetSceneByName("Loading"));
            yield return Fade(0);
            yield return LevelStreamService.I.LoadForTravel(destination, operationTimeout);
            CheckConnection();
            yield return Fade(1);
            var targetScene = SceneManager.GetSceneByName(destination);
            var target = SectorScene.Find(targetScene);
            if (target == null || target.GetArrival(arrivalRoute) == null)
                throw new InvalidOperationException("Destination has no SectorScene/arrivalSpawn.");
            foreach (var pair in targetRoots) if (pair.Key != null) pair.Key.SetActive(pair.Value);
            // Start/OnEnable registrations and physics transforms must settle before grounding.
            RebindInteractions(targetScene);
            yield return null;
            Physics.SyncTransforms();
            PlaceRig(target.GetArrival(arrivalRoute), targetScene);
            if (!SceneManager.SetActiveScene(targetScene))
                throw new InvalidOperationException("Could not set the destination active.");
            committed = true;
            RestoreCamera();
            PlayerInventory.LocalInventory?.collectedCards.Clear();
            Publish(target.sector, target.entryZone);
            yield return LevelStreamService.I.UnloadForTravel("Loading", operationTimeout);
            yield return LevelStreamService.I.UnloadForTravel(source.name, operationTimeout);
            yield return Fade(0);
        }

        private IEnumerator Respawn(Transform spawn)
        {
            FreezeRig();
            yield return Fade(1);
            CheckConnection();
            PlaceRig(spawn, source);
            committed = true;
            var context = SectorScene.Find(source);
            Publish(oldSector, context != null ? context.entryZone : oldZone);
            yield return Fade(0);
        }

        // Flatten nested enumerators so exceptions in loading/grounding restore the same rig state.
        // Unity does not propagate child coroutine exceptions to a parent's try/catch.
        private IEnumerator RunGuarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            Exception failure = null;
            while (stack.Count > 0)
            {
                object current = null;
                bool moved = false;
                try
                {
                    if (cancelled) throw new InvalidOperationException("Connection interrupted during travel.");
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                }
                catch (Exception ex) { failure = ex; }
                if (failure != null) break;
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (failure != null)
            {
                Debug.LogException(failure, this);
                Recover();
                yield return Fade(0);
                ShowError(committed ? "You have arrived. Previous area cleanup is still finishing." :
                    "Travel could not finish. Please try the door again.");
            }
            IsBusy = false;
            if (CurrentSector == SectorId.None && !suspended && !cancelled && PhotonNetwork.InRoom)
            {
                var context = SectorScene.Find(SceneManager.GetActiveScene());
                if (context != null) Publish(context.sector, committed ? context.entryZone : oldZone);
            }
            RestoreRig();
            SetFade(0);
            sourceRoots.Clear();
            targetRoots.Clear();
            nextTravelTime = Time.unscaledTime + 1f;
        }

        private void Recover()
        {
            if (!committed && source.isLoaded)
            {
                if (relocated && origin != null && player != null)
                {
                    origin.transform.SetPositionAndRotation(oldOriginPosition, oldOriginRotation);
                    player.transform.SetPositionAndRotation(oldBodyPosition, oldBodyRotation);
                }
                SceneManager.SetActiveScene(source);
                SetSourceActive(true);
                if (destination != source.name && LevelStreamService.I != null)
                    LevelStreamService.I.AbandonLoad(destination);
                RebindInteractions(source);
                Publish(oldSector, oldZone);
            }
            RestoreCamera();
            if (LevelStreamService.I != null)
            {
                LevelStreamService.I.AbandonLoad("Loading");
                if (committed && destination != source.name) LevelStreamService.I.AbandonLoad(source.name);
            }
            RestoreRig();
        }

        private void HideDestinationWhileLoading(Scene scene, LoadSceneMode mode)
        {
            if (!IsBusy || scene.name != destination || destination == source.name) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                targetRoots[root] = root.activeSelf;
                root.SetActive(false);
            }
        }

        private void ShowLoadingScene(Scene scene)
        {
            if (!SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Could not activate Loading.");
            oldClearFlags = origin.Camera.clearFlags;
            oldBackground = origin.Camera.backgroundColor;
            loadingPresentation = true;
            origin.Camera.clearFlags = CameraClearFlags.SolidColor;
            origin.Camera.backgroundColor = Color.black;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var debug in root.GetComponentsInChildren<LoadingDebugText>(true))
                {
                    debug.enabled = false;
                    if (debug.debugText != null) debug.debugText.text = "";
                }
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    // Camera-space UI follows both XR eyes without creating a second rig.
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = origin.Camera;
                    canvas.planeDistance = Mathf.Max(0.6f, origin.Camera.nearClipPlane + 0.1f);
                }
            }
        }

        private void RestoreCamera()
        {
            if (!loadingPresentation) return;
            if (origin != null && origin.Camera != null)
            {
                origin.Camera.clearFlags = oldClearFlags;
                origin.Camera.backgroundColor = oldBackground;
            }
            loadingPresentation = false;
        }

        private void CheckConnection()
        {
            if (!PhotonNetwork.InRoom || cancelled)
                throw new InvalidOperationException("The Photon room was left during travel.");
        }

        private void SetSourceActive(bool active)
        {
            foreach (var pair in sourceRoots)
                if (pair.Key != null) pair.Key.SetActive(active && pair.Value);
        }

        private void FreezeRig()
        {
            wasKinematic = body.isKinematic;
            frozen = true;
            frozenBehaviours.Clear();
            frozenColliders.Clear();
            // Disabling interactors releases held objects before their scene is unloaded.
            foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true)) Freeze(interactor);
            foreach (var turn in origin.GetComponentsInChildren<SmoothTurnController>(true)) Freeze(turn);
            Freeze(player);
            foreach (var collider in origin.GetComponentsInChildren<Collider>(true))
            {
                frozenColliders[collider] = collider.enabled;
                collider.enabled = false;
            }
            if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
        }

        private void Freeze(Behaviour behaviour)
        {
            frozenBehaviours[behaviour] = behaviour.enabled;
            behaviour.enabled = false;
        }

        private void RestoreRig()
        {
            if (!frozen) return;
            if (player != null) player.ResetAfterTeleport();
            Physics.SyncTransforms();
            foreach (var pair in frozenColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            if (body != null)
            {
                body.isKinematic = wasKinematic;
                if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            foreach (var pair in frozenBehaviours) if (pair.Key != null) pair.Key.enabled = pair.Value;
            frozenBehaviours.Clear();
            frozenColliders.Clear();
            frozen = false;
        }

        private void PlaceRig(Transform spawn, Scene scene)
        {
            var hits = Physics.RaycastAll(spawn.position + Vector3.up * 0.5f, Vector3.down, 6f,
                player.locomotionEnabledLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit floor = default;
            foreach (var hit in hits)
                if (hit.collider.gameObject.scene == scene && hit.normal.y > 0.65f &&
                    hit.point.y <= spawn.position.y + 0.2f && (!found || hit.distance < floor.distance))
                { floor = hit; found = true; }
            if (!found) throw new InvalidOperationException("No destination floor beneath " + spawn.name);

            relocated = true;
            Transform camera = origin.Camera.transform;
            origin.transform.RotateAround(camera.position, Vector3.up, Mathf.DeltaAngle(camera.eulerAngles.y, spawn.eulerAngles.y));
            Vector3 shift = spawn.position - camera.position;
            shift.y = 0;
            origin.transform.position += shift;
            // Match the locomotion Update orientation before measuring capsule clearance.
            player.bodyCollider.transform.eulerAngles = new Vector3(0, camera.eulerAngles.y, 0);
            GetCapsule(player.bodyCollider, out var a, out var b, out var radius);
            float bottom = Mathf.Min(a.y, b.y) - radius;
            origin.transform.position += Vector3.up * (floor.point.y + 0.04f - bottom);
            Physics.SyncTransforms();
            GetCapsule(player.bodyCollider, out a, out b, out radius);
            foreach (var collider in Physics.OverlapCapsule(a, b, radius, player.locomotionEnabledLayers, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(origin.transform) && collider.gameObject.scene == scene)
                    throw new InvalidOperationException("Arrival body overlaps " + collider.name);
            var head = player.headCollider;
            Vector3 headScale = head.transform.lossyScale;
            float headRadius = head.radius * Mathf.Max(Mathf.Abs(headScale.x), Mathf.Abs(headScale.y), Mathf.Abs(headScale.z));
            foreach (var collider in Physics.OverlapSphere(head.transform.TransformPoint(head.center), headRadius,
                         player.locomotionEnabledLayers, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(origin.transform) && collider.gameObject.scene == scene)
                    throw new InvalidOperationException("Arrival head overlaps " + collider.name);
            player.ResetAfterTeleport();
        }

        private static void GetCapsule(CapsuleCollider capsule, out Vector3 a, out Vector3 b, out float radius)
        {
            Vector3 scale = capsule.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            int axis = capsule.direction;
            Vector3 direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            radius = capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
            float half = Mathf.Max(0, capsule.height * scale[axis] * 0.5f - radius);
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            Vector3 offset = capsule.transform.TransformDirection(direction).normalized * half;
            a = center + offset;
            b = center - offset;
        }

        private void RebindInteractions(Scene scene)
        {
            if (InteractionManager == null) return;
            foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
                interactor.interactionManager = InteractionManager;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
                    interactable.interactionManager = InteractionManager;
        }

        public void NotifySceneReady(Scene scene)
        {
            if (IsBusy) return;
            var context = SectorScene.Find(scene);
            if (context != null)
            {
                Publish(context.sector, reconnectSector == context.sector ? reconnectZone : context.entryZone);
                reconnectSector = SectorId.None;
            }
        }

        private void Publish(SectorId sector, ZoneId zone)
        {
            CurrentSector = PhotonNetwork.InRoom && !suspended ? sector : SectorId.None;
            if (!PhotonNetwork.InRoom) return;
            ZoneStateService.Instance?.SetLocalZone(zone);
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                [SectorPresence.PropertyKey] = (int)CurrentSector,
                [ZoneStateService.ZonePropKey] = (int)zone
            });
        }

        public override void OnJoinedRoom()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            if (AppState.I != null && AppState.I.IsReady) NotifySceneReady(SceneManager.GetActiveScene());
            else Publish(SectorId.None, ZoneId.None);
        }

        public override void OnLeftRoom() => ClearSession();
        public override void OnDisconnected(DisconnectCause cause) => ClearSession();
        private void ClearSession()
        {
            if (CurrentSector != SectorId.None)
            {
                reconnectSector = CurrentSector;
                reconnectZone = ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None;
            }
            CurrentSector = SectorId.None;
            cancelled = IsBusy;
            ZoneStateService.Instance?.ResetSession();
        }

        private void OnApplicationPause(bool paused)
        {
            suspended = paused;
            if (paused)
            {
                pausedSector = IsBusy && !committed ? oldSector : CurrentSector;
                pausedZone = IsBusy && !committed ? oldZone :
                    ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None;
                cancelled = IsBusy;
                foreach (var monster in FindObjectsOfType<SectorMonsterSync>()) monster.FlushState();
                Publish(SectorId.None, ZoneId.None);
                if (PhotonNetwork.InRoom) PhotonNetwork.SendAllOutgoingCommands();
            }
            else if (!IsBusy)
            {
                var context = SectorScene.Find(SceneManager.GetActiveScene());
                if (context != null)
                    Publish(context.sector, pausedSector == context.sector ? pausedZone : context.entryZone);
            }
        }

        private void EnsureFade()
        {
            if (fadeQuad != null) return;
            fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeQuad.name = "Persistent VR travel fade";
            Destroy(fadeQuad.GetComponent<Collider>());
            fadeQuad.transform.SetParent(origin.Camera.transform, false);
            fadeQuad.transform.localPosition = Vector3.forward * Mathf.Max(0.3f, origin.Camera.nearClipPlane + 0.05f);
            fadeQuad.transform.localScale = Vector3.one * 10;
            fadeMaterial = new Material(fadeShader);
            var renderer = fadeQuad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = fadeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = short.MaxValue;
            SetFade(0);
            var message = new GameObject("Travel status");
            message.transform.SetParent(origin.Camera.transform, false);
            message.transform.localPosition = new Vector3(0, -0.1f, 0.65f);
            errorText = message.AddComponent<TextMeshPro>();
            errorText.fontSize = 1.2f;
            errorText.alignment = TextAlignmentOptions.Center;
            errorText.rectTransform.sizeDelta = new Vector2(0.7f, 0.3f);
            errorText.text = "";
        }

        private IEnumerator Fade(float alpha)
        {
            float start = fadeMaterial != null ? fadeMaterial.color.a : 0;
            float elapsed = 0;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(start, alpha, elapsed / Mathf.Max(0.01f, fadeSeconds)));
                yield return null;
            }
            SetFade(alpha);
        }

        private void SetFade(float alpha)
        {
            if (fadeMaterial != null) fadeMaterial.color = new Color(0, 0, 0, alpha);
            if (fadeQuad != null) fadeQuad.SetActive(alpha > 0);
        }

        private void ShowError(string message)
        {
            LastError = message;
            errorUntil = Time.unscaledTime + 6;
            if (errorText != null) errorText.text = message;
            Debug.LogWarning(message, this);
        }

        private void Update()
        {
            if (errorText != null && Time.unscaledTime > errorUntil) errorText.text = "";
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (I != this) return;
            StopAllCoroutines();
            if (IsBusy) Recover();
            SetFade(0);
            IsBusy = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HideDestinationWhileLoading;
            if (I == this) I = null;
            if (fadeMaterial != null) Destroy(fadeMaterial);
            if (fadeQuad != null) Destroy(fadeQuad);
            if (errorText != null) Destroy(errorText.gameObject);
        }
    }
}
