using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PanelNavigationTest
{
    // Edit this list to the real panel object names in your project.
    // Using exact names is more robust than fuzzy heuristics.
    readonly string[] KnownPanelNames = new[]
    {
        "adminPanel",
        "topMenuPanel",
        "loginPanel",
        "bottmNavPanel", // keep the project typo if it exists
        "homePanel",
        "bottomNavPanel" // add any other real names here
    };

    // Which of the known panels count as "navigation" panels
    readonly string[] NavNames = new[] { "topMenuPanel", "bottmNavPanel", "bottomNavPanel", "topNav", "nav" };

    [UnityTest]
    public IEnumerator Panels_Are_Either_One_Or_Allowed_Two()
    {
        yield return null;
        yield return null;

        // Find active panels by exact name match
        var activePanels = KnownPanelNames
            .Select(name => GameObject.Find(name))
            .Where(go => go != null && go.activeInHierarchy)
            .ToList();

        // If none found by exact names, fall back to previous heuristic (optional)
        if (activePanels.Count == 0)
        {
            var rects = Object.FindObjectsOfType<RectTransform>();
            activePanels = rects
                .Select(r => r.gameObject)
                .Where(g => g.activeInHierarchy && g.name.ToLower().Contains("panel"))
                .Distinct()
                .ToList();
        }

        if (activePanels.Count == 0)
        {
            var roots = Object.FindObjectsOfType<Transform>().Where(t => t.parent == null).Take(10).Select(t => t.name);
            Assert.Fail("No active UI panels found. Ensure the UI scene is loaded and KnownPanelNames contains your panels. Sample roots: " + string.Join(", ", roots));
        }

        var panelNames = activePanels.Select(p => p.name).ToList();
        bool anyNav = panelNames.Any(n => NavNames.Any(nav => n.Equals(nav, System.StringComparison.OrdinalIgnoreCase)));

        bool valid = panelNames.Count == 1 || (panelNames.Count == 2 && anyNav);

        Assert.IsTrue(valid, $"Invalid panel state. Active panels ({panelNames.Count}): {string.Join(", ", panelNames)}. " +
            "Expected either exactly 1 active panel, or exactly 2 where one is a nav/top panel.");
    }
}
