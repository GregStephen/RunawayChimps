using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Zombie Crawl facing the direction the Crawler actually travels without touching
/// the imported animation skeleton. The previous visual anchor used a 180-degree yaw that
/// runtime testing showed was backwards. The cleaned baseline also rotates a long rigid
/// Zombie as one object, so hard NavMesh turns can otherwise whip the body across the vent.
///
/// This component corrects only the complete visual anchor: it derives a stable heading
/// from a short history of gameplay-root movement and turns toward it at a bounded rate.
/// No Zombie bone position/rotation is ever modified here.
/// </summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class CrawlerVisualHeadingStabilizer : MonoBehaviour
{
    private const string LevelOneSceneName = "Level1_Containment";
    private const int MaximumWaitFrames = 180;

    [Header("Rigid visual turn smoothing")]
    [SerializeField, Min(0.2f)] private float headingLookbackDistance = 0.9f;
    [SerializeField, Min(30f)] private float maximumTurnDegreesPerSecond = 150f;
    [SerializeField, Min(0.01f)] private float sampleSpacing = 0.05f;
    [SerializeField, Min(0.5f)] private float historyDistance = 3f;
    [SerializeField, Min(0.25f)] private float discontinuityDistance = 1.25f;

    private readonly List<Vector3> pathHistory = new List<Vector3>(64);

    private CrawlerVisualController visualController;
    private Transform visualAnchor;
    private Vector3 previousGameplayPosition;
    private int waitFrames;
    private bool configured;
    private bool warnedDiscontinuity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != LevelOneSceneName)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            MonsterNavigation[] monsters = roots[r].GetComponentsInChildren<MonsterNavigation>(true);
            for (int i = 0; i < monsters.Length; i++)
            {
                MonsterNavigation monster = monsters[i];
                if (monster == null || monster.gameObject.scene != scene)
                    continue;

                if (monster.GetComponent<CrawlerVisualHeadingStabilizer>() == null)
                    monster.gameObject.AddComponent<CrawlerVisualHeadingStabilizer>();
            }
        }
    }

    private void Awake()
    {
        visualController = GetComponent<CrawlerVisualController>();
        previousGameplayPosition = transform.position;
    }

    private void OnEnable()
    {
        previousGameplayPosition = transform.position;
        pathHistory.Clear();
        configured = false;
        waitFrames = 0;
    }

    private void LateUpdate()
    {
        if (!configured && !TryConfigure())
            return;

        Vector3 current = Flatten(transform.position);
        Vector3 previous = Flatten(previousGameplayPosition);
        float frameDistance = Vector3.Distance(previous, current);
        previousGameplayPosition = transform.position;

        // Controller handoff/network correction/scene recovery may legitimately reposition
        // the gameplay root. Do not turn that discontinuity into a giant visual whip.
        if (frameDistance >= discontinuityDistance)
        {
            ResetHeadingHistory(current, snapToGameplayRotation: true);

            if (!warnedDiscontinuity)
            {
                warnedDiscontinuity = true;
                Debug.LogWarning(
                    $"{name}: Crawler gameplay root moved {frameDistance:0.00} m in one frame; visual heading history was reset instead of sweeping the Zombie through the vent.",
                    this);
            }
            return;
        }

        AddPathSample(current);
        Vector3 lookback = FindLookbackPoint(current, headingLookbackDistance);
        Vector3 forward = current - lookback;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0025f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        visualAnchor.rotation = Quaternion.RotateTowards(
            visualAnchor.rotation,
            desiredRotation,
            maximumTurnDegreesPerSecond * Time.deltaTime);
    }

    private bool TryConfigure()
    {
        if (visualController == null)
            visualController = GetComponent<CrawlerVisualController>();

        Transform visualRoot = visualController != null ? visualController.VisualRoot : null;
        if (visualRoot == null || visualRoot.parent == null)
        {
            waitFrames++;
            if (waitFrames == MaximumWaitFrames)
            {
                Debug.LogError(
                    $"{name}: Zombie Crawl visual anchor was not available after {MaximumWaitFrames} frames; turn stabilization could not initialize.",
                    this);
            }
            return false;
        }

        visualAnchor = visualRoot.parent;
        Vector3 current = Flatten(transform.position);

        // Runtime feedback confirms the old 180-degree anchor correction made Zombie Crawl
        // travel backwards. The authored Zombie forward direction is therefore aligned to
        // gameplay-root forward here. This changes only the complete visual anchor.
        ResetHeadingHistory(current, snapToGameplayRotation: true);
        previousGameplayPosition = transform.position;
        configured = true;

        Debug.Log(
            $"{name}: Zombie Crawl visual forward was aligned to gameplay travel and rigid turn smoothing is active " +
            $"({headingLookbackDistance:0.00} m lookback, {maximumTurnDegreesPerSecond:0} deg/s max visual turn).",
            this);
        return true;
    }

    private void ResetHeadingHistory(Vector3 current, bool snapToGameplayRotation)
    {
        pathHistory.Clear();

        Vector3 gameplayForward = transform.forward;
        gameplayForward.y = 0f;
        if (gameplayForward.sqrMagnitude < 0.001f)
            gameplayForward = Vector3.forward;
        gameplayForward.Normalize();

        // Seed a short straight segment behind the current root. This gives the visual a
        // stable initial direction before enough real movement history exists.
        pathHistory.Add(current - gameplayForward * Mathf.Max(headingLookbackDistance, sampleSpacing));
        pathHistory.Add(current);

        if (snapToGameplayRotation && visualAnchor != null)
            visualAnchor.rotation = Quaternion.LookRotation(gameplayForward, Vector3.up);
    }

    private void AddPathSample(Vector3 current)
    {
        if (pathHistory.Count == 0)
        {
            ResetHeadingHistory(current, snapToGameplayRotation: false);
            return;
        }

        Vector3 last = pathHistory[pathHistory.Count - 1];
        if ((current - last).sqrMagnitude < sampleSpacing * sampleSpacing)
            return;

        pathHistory.Add(current);
        TrimHistory();
    }

    private void TrimHistory()
    {
        if (pathHistory.Count <= 2)
            return;

        float distance = 0f;
        int keepFrom = 0;

        for (int i = pathHistory.Count - 1; i > 0; i--)
        {
            distance += Vector3.Distance(pathHistory[i], pathHistory[i - 1]);
            if (distance >= historyDistance)
            {
                keepFrom = i - 1;
                break;
            }
        }

        if (keepFrom > 0)
            pathHistory.RemoveRange(0, keepFrom);

        // Defensive cap for an unexpectedly noisy transform without allowing unbounded
        // per-session growth.
        if (pathHistory.Count > 96)
            pathHistory.RemoveRange(0, pathHistory.Count - 96);
    }

    private Vector3 FindLookbackPoint(Vector3 current, float distanceBack)
    {
        if (pathHistory.Count == 0)
            return current;

        float remaining = Mathf.Max(sampleSpacing, distanceBack);
        Vector3 from = current;

        for (int i = pathHistory.Count - 1; i >= 0; i--)
        {
            Vector3 to = pathHistory[i];
            float segment = Vector3.Distance(from, to);
            if (segment <= 0.0001f)
                continue;

            if (segment >= remaining)
                return Vector3.Lerp(from, to, remaining / segment);

            remaining -= segment;
            from = to;
        }

        return pathHistory[0];
    }

    private static Vector3 Flatten(Vector3 position)
    {
        position.y = 0f;
        return position;
    }
}
