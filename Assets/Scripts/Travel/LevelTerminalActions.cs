using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Travel
{
    // Bind the future terminal's locally operated buttons to these UnityEvent methods.
    // The component intentionally creates no terminal art or UI.
    public sealed class LevelTerminalActions : MonoBehaviour
    {
        public BoxCollider safeEntryArea;

        [ContextMenu("Travel/Return to Hub computer")]
        public void ReturnToHub() => Request(SectorDestinations.Hub);

        [ContextMenu("Travel/Return to Level 1 safe room")]
        public void ReturnToLevelOne() => Request(SectorDestinations.LevelOne);

        private void Request(string destination)
        {
            if (!Application.isPlaying) return;
            var travel = SectorTravelService.I;
            var player = GorillaLocomotion.Player.Instance;
            var scene = gameObject.scene;
            var context = SectorScene.Find(scene);
            if (travel == null || travel.IsBusy || player == null || player.headCollider == null ||
                scene != SceneManager.GetActiveScene() || context == null ||
                context.sector != SectorId.Conditioning || travel.CurrentSector != context.sector ||
                safeEntryArea == null || !safeEntryArea.enabled || !safeEntryArea.gameObject.activeInHierarchy ||
                safeEntryArea.gameObject.scene != scene) return;

            // Local box coordinates also work if the terminal's room is rotated later.
            Vector3 head = safeEntryArea.transform.InverseTransformPoint(
                player.headCollider.transform.TransformPoint(player.headCollider.center));
            if (!new Bounds(safeEntryArea.center, safeEntryArea.size).Contains(head))
            {
                Debug.LogWarning("Return to the safe entry room to use this terminal.", this);
                return;
            }
            travel.TravelTo(destination, ArrivalRoute.Terminal);
        }
    }
}
