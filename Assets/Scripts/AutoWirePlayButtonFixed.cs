using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Transform))]
public class AutoWirePlayButtonFixed : MonoBehaviour
{
    [Tooltip("Exact name of the Play button GameObject (case-sensitive)")]
    public string playButtonName = "Play";

    // list of common method names we'll try to invoke on your MainMenuController
    readonly string[] candidateMethodNames = new string[] { "PlayFirstLevel", "OpenLevelSelect", "Play", "OnPlayPressed", "StartGame" };

    void Start()
    {
        // find the Button
        Button foundButton = null;

        // 1) try to find as direct child
        Transform child = transform.Find(playButtonName);
        if (child != null) foundButton = child.GetComponent<Button>();

        // 2) fallback: search all Buttons in scene by name
        if (foundButton == null)
        {
            var all = FindObjectsOfType<Button>();
            foreach (var b in all)
            {
                if (b.gameObject.name == playButtonName)
                {
                    foundButton = b;
                    break;
                }
            }
        }

        if (foundButton == null)
        {
            Debug.LogWarning($"[AutoWirePlayButtonFixed] Play Button named '{playButtonName}' not found in scene. Please check name or assign manually.");
            return;
        }

        // find controller on this same GameObject
        var controller = GetComponent<MonoBehaviour>(); // placeholder to get runtime type; we'll search for any component that has a candidate method
        MethodInfo selectedMethod = null;
        Component selectedComponent = null;

        // search all components on this GameObject for a candidate method
        var comps = GetComponents<Component>();
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();
            foreach (var mname in candidateMethodNames)
            {
                var mi = t.GetMethod(mname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    selectedMethod = mi;
                    selectedComponent = comp;
                    break;
                }
            }
            if (selectedMethod != null) break;
        }

        if (selectedMethod == null)
        {
            Debug.LogWarning("[AutoWirePlayButtonFixed] No suitable method found on this GameObject. Candidate names: " + string.Join(", ", candidateMethodNames));
            Debug.LogWarning("[AutoWirePlayButtonFixed] Attach a MainMenuController (or similar) with one of those methods, or wire the button manually in the Inspector.");
            return;
        }

        // wire the button: use lambda that calls MethodInfo.Invoke
        foundButton.onClick.RemoveAllListeners();
        foundButton.onClick.AddListener(() =>
        {
            try
            {
                selectedMethod.Invoke(selectedComponent, null);
                Debug.Log($"[AutoWirePlayButtonFixed] Invoked '{selectedMethod.Name}' on component '{selectedComponent.GetType().Name}'.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoWirePlayButtonFixed] Exception invoking method: " + ex);
            }
        });

        Debug.Log($"[AutoWirePlayButtonFixed] Wired Play button '{foundButton.gameObject.name}' to method '{selectedMethod.Name}' on component '{selectedComponent.GetType().Name}'.");
    }
}
