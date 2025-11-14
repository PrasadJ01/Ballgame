using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Finds a UI Button named playButtonName (searches children first, then all Buttons in scene),
/// and wires it to call MainMenuController.PlayFirstLevel() on the same GameObject.
/// Place this component on the same GameObject that has MainMenuController (e.g. MainMenuManager).
/// </summary>
[RequireComponent(typeof(Transform))]
public class AutoWirePlayButtonFixed : MonoBehaviour
{
    [Tooltip("Exact name of the Play button GameObject")]
    public string playButtonName = "Play";

    void Start()
    {
        // Find play button as child first
        Transform child = transform.Find(playButtonName);
        Button found = null;

        if (child != null)
            found = child.GetComponent<Button>();

        // If not found as child, search all Buttons in scene
        if (found == null)
        {
            var all = FindObjectsOfType<Button>();
            foreach (var b in all)
            {
                if (b.gameObject.name == playButtonName)
                {
                    found = b;
                    break;
                }
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"[AutoWirePlayButtonFixed] Could not find a Button named '{playButtonName}' in scene. Check name or assign manually.");
            return;
        }

        // Ensure the MainMenuController exists on this GameObject
        var controller = GetComponent<MainMenuController>();
        if (controller == null)
        {
            Debug.LogError("[AutoWirePlayButtonFixed] MainMenuController not found on same GameObject. Attach MainMenuController to this GameObject.");
            return;
        }

        // Wire listener (avoid duplicates)
        found.onClick.RemoveListener(controller.PlayFirstLevel);
        found.onClick.AddListener(controller.PlayFirstLevel);

        Debug.Log($"[AutoWirePlayButtonFixed] Wired Play button '{found.gameObject.name}' to MainMenuController.PlayFirstLevel()");
    }
}
