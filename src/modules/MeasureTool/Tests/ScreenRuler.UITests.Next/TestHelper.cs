// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

/// <summary>
/// Shared helpers for the Screen Ruler <c>.Next</c> UI tests. Ported from the legacy
/// <c>ScreenRuler.UITests/TestHelper.cs</c> (WinAppDriver) to the winappcli harness.
/// </summary>
/// <remarks>
/// Key differences from the legacy helper:
/// <list type="bullet">
///   <item><description>The Settings tree is driven through <c>testBase.Session</c> (the Settings
///   window). The Screen Ruler toolbar buttons live in a <b>different process/window</b>
///   (<c>PowerToys.MeasureToolUI</c>), so they're found through a process-scoped
///   <see cref="Session.FromProcess(string, PowerToysModule, int)"/> session — the winappcli
///   equivalent of the legacy <c>global: true</c> Find.</description></item>
///   <item><description>Mouse / keyboard / clipboard go through the static
///   <c>MouseHelper</c> / <c>KeyboardHelper</c> / <c>ClipboardHelper</c> instead of instance
///   methods on <c>Session</c> / <c>UITestBase</c>.</description></item>
/// </list>
/// </remarks>
public static class TestHelper
{
    private static readonly string[] ShortcutSeparators = { " + ", "+", " " };

    // Button automation ids from the Measure Tool's Resources.resw.
    public const string BoundsButtonId = "Button_Bounds";
    public const string SpacingButtonName = "Button_Spacing";
    public const string HorizontalSpacingButtonName = "Button_SpacingHorizontal";
    public const string VerticalSpacingButtonName = "Button_SpacingVertical";
    public const string CloseButtonId = "Button_Close";

    // The Measure Tool UI process (the toolbar + measurement overlays). NOTE: the window TITLE is
    // "PowerToys.ScreenRuler", but the PROCESS name winappcli's -a flag needs is "PowerToys.MeasureToolUI".
    public const string ScreenRulerProcess = "PowerToys.MeasureToolUI";

    // The module's key in the global settings.json "enabled" section (note the space). Pass this to
    // the UITestBase ctor's enableModules so the runner boots ONLY this module — much faster on a
    // fresh profile (CI), where otherwise all ~30 modules start, and more isolated (no other module's
    // hotkeys/overlays interfere).
    public const string ModuleSettingsKey = "Measure Tool";

    /// <summary>Navigate to the Screen Ruler settings page, enable the toggle, and read the shortcut.</summary>
    public static Key[] InitializeTest(UITestBase testBase, string testName)
    {
        LaunchFromSetting(testBase);

        var toggleSwitch = SetScreenRulerToggle(testBase, enable: true);
        Assert.IsTrue(toggleSwitch.IsOn, $"Screen Ruler toggle switch should be ON for {testName}");

        var activationKeys = ReadActivationShortcut(testBase);
        Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
        Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

        return activationKeys;
    }

    /// <summary>Close the Screen Ruler UI (best-effort).</summary>
    public static void CleanupTest(UITestBase testBase)
    {
        CloseScreenRulerUI(testBase);
    }

    /// <summary>Navigate to the Screen Ruler (Measure Tool) settings page.</summary>
    public static void LaunchFromSetting(UITestBase testBase)
    {
        // The "System Tools" group is collapsed by default, so the Screen Ruler child item isn't in
        // the tree until the group is expanded. Expand it only when the child isn't already present.
        if (!testBase.Session.Has(By.AccessibilityId("ScreenRulerNavItem"), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), 5000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("ScreenRulerNavItem"), 5000).Click(msPostAction: 800);
    }

    /// <summary>Set the Screen Ruler toggle to the requested state.</summary>
    public static ToggleSwitch SetScreenRulerToggle(UITestBase testBase, bool enable)
    {
        var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"), 5000);
        toggleSwitch.Toggle(enable);
        toggleSwitch.WaitForProperty("ToggleState", enable ? "On" : "Off", 5000);
        return toggleSwitch;
    }

    /// <summary>Set the Screen Ruler toggle and assert it reached the requested state.</summary>
    public static void SetAndVerifyScreenRulerToggle(UITestBase testBase, bool enable, string testName)
    {
        var toggleSwitch = SetScreenRulerToggle(testBase, enable);
        Assert.AreEqual(enable, toggleSwitch.IsOn, $"Screen Ruler toggle switch should be {(enable ? "ON" : "OFF")} for {testName}");
    }

    /// <summary>Read the activation shortcut from the ShortcutControl's EditButton HelpText.</summary>
    public static Key[] ReadActivationShortcut(UITestBase testBase)
    {
        var shortcutCard = testBase.Session.Find<Element>(By.AccessibilityId("Shortcut_ScreenRuler"), 5000);
        var shortcutButton = shortcutCard.Find<Element>(By.AccessibilityId("EditButton"), 5000);
        return ParseShortcutText(shortcutButton.HelpText);
    }

    /// <summary>Parse "Win + Ctrl + Shift + M" into a Key chord (note: "win" maps to <see cref="Key.LWin"/>).</summary>
    public static Key[] ParseShortcutText(string shortcutText)
    {
        if (string.IsNullOrEmpty(shortcutText))
        {
            return new[] { Key.LWin, Key.Ctrl, Key.Shift, Key.M };
        }

        var keys = new List<Key>();
        foreach (var part in shortcutText.Split(ShortcutSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanPart = part.Trim().ToLowerInvariant();
            Key? key = cleanPart switch
            {
                "win" or "windows" => Key.LWin,
                "ctrl" or "control" => Key.Ctrl,
                "shift" => Key.Shift,
                "alt" => Key.Alt,
                _ when cleanPart.Length == 1 && cleanPart[0] >= 'a' && cleanPart[0] <= 'z' =>
                    (Key)Enum.Parse(typeof(Key), cleanPart.ToUpperInvariant()),
                _ => null,
            };

            if (key.HasValue)
            {
                keys.Add(key.Value);
            }
        }

        return keys.Count > 0 ? keys.ToArray() : new[] { Key.LWin, Key.Ctrl, Key.Shift, Key.M };
    }

    /// <summary>True when at least one Measure Tool window is open.</summary>
    public static bool IsScreenRulerUIOpen(UITestBase testBase) =>
        WindowsFinder.ListByApp(ScreenRulerProcess).Count > 0;

    /// <summary>Poll until the Measure Tool UI reaches the requested presence.</summary>
    public static bool WaitForScreenRulerUIState(UITestBase testBase, bool shouldBeOpen, int timeoutMs = 5000, int pollingIntervalMs = 100)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < endTime)
        {
            if (IsScreenRulerUIOpen(testBase) == shouldBeOpen)
            {
                return true;
            }

            Thread.Sleep(pollingIntervalMs);
        }

        return false;
    }

    public static bool WaitForScreenRulerUI(UITestBase testBase, int timeoutMs = 5000) =>
        WaitForScreenRulerUIState(testBase, shouldBeOpen: true, timeoutMs);

    public static bool WaitForScreenRulerUIToDisappear(UITestBase testBase, int timeoutMs = 5000) =>
        WaitForScreenRulerUIState(testBase, shouldBeOpen: false, timeoutMs);

    /// <summary>Close the Measure Tool UI if it's open (best-effort, tolerant).</summary>
    public static void CloseScreenRulerUI(UITestBase testBase)
    {
        if (!IsScreenRulerUIOpen(testBase))
        {
            return;
        }

        // Prefer the toolbar's Close button; fall back to WM_CLOSE on every Measure Tool window.
        try
        {
            var ruler = Session.FromProcess(ScreenRulerProcess, PowerToysModule.ScreenRuler, timeoutMS: 2000);
            if (ruler.Has(By.AccessibilityId(CloseButtonId), 1000))
            {
                ruler.Find<Element>(By.AccessibilityId(CloseButtonId), 2000).Click();
            }
        }
        catch
        {
            // Ignore — fall through to the tolerant WM_CLOSE.
        }

        WindowControl.TryCloseByApp(ScreenRulerProcess);
    }

    /// <summary>Clear the clipboard (STA handled inside the helper).</summary>
    public static void ClearClipboard() => ClipboardHelper.Clear();

    /// <summary>Read the clipboard text.</summary>
    public static string GetClipboardText() => ClipboardHelper.GetText();

    /// <summary>Validate clipboard content holds a valid spacing measurement for the given tool.</summary>
    public static bool ValidateSpacingClipboardContent(string clipboardText, string spacingType)
    {
        if (string.IsNullOrEmpty(clipboardText))
        {
            return false;
        }

        return spacingType switch
        {
            "Spacing" => Regex.IsMatch(clipboardText, @"\d+\s*[x×]\s*\d+"),
            "Horizontal Spacing" or "Vertical Spacing" => Regex.IsMatch(clipboardText, @"^\d+$"),
            _ => false,
        };
    }

    /// <summary>
    /// Send the activation chord, retrying until the Measure Tool UI appears. The runner arms its
    /// keyboard hook asynchronously after the module is enabled, so the first chord can be lost
    /// (skill Recipe 4). An initial settle gives the just-enabled module time to register its
    /// hotkey before the first send. Returns true once a Measure Tool window is visible.
    /// </summary>
    public static bool SendShortcutUntilVisible(UITestBase testBase, Key[] activationKeys, int attempts = 5, int perAttemptMs = 3000)
    {
        // Let the runner finish wiring the global hotkey after the module was just toggled on.
        Thread.Sleep(1500);

        for (int i = 0; i < attempts; i++)
        {
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitForScreenRulerUI(testBase, perAttemptMs))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Activate Screen Ruler via the shortcut and wait for the toolbar window.</summary>
    public static Session ActivateScreenRuler(UITestBase testBase, Key[] activationKeys, string testName)
    {
        ClearClipboard();

        // Park the cursor on the primary-monitor centre so the Measure Tool initialises tracking at a
        // predictable on-screen spot before activation (the cursor can otherwise be anywhere).
        var (cx, cy) = ScreenCenter();
        MouseHelper.MoveTo(cx, cy);
        Thread.Sleep(200);

        Assert.IsTrue(
            SendShortcutUntilVisible(testBase, activationKeys),
            $"ScreenRulerUI should appear after pressing activation shortcut for {testName}: {string.Join(" + ", activationKeys)}");

        // Process-scoped session so the toolbar buttons resolve regardless of which Measure Tool
        // window owns them (the winappcli equivalent of the legacy global Find).
        return Session.FromProcess(ScreenRulerProcess, PowerToysModule.ScreenRuler, timeoutMS: 5000);
    }

    /// <summary>Run a spacing-tool measurement (move + click) and validate the clipboard.</summary>
    public static void PerformSpacingToolTest(UITestBase testBase, string buttonId, string testName) =>
        RunMeasurementTest(
            testBase,
            buttonId,
            testName,
            PerformSpacingMeasurement,
            clip => ValidateSpacingClipboardContent(clip, testName));

    /// <summary>
    /// Shared driver for every Screen Ruler measurement test (Bounds + the three spacing tools).
    /// Activates the ruler, selects the tool, WARMS the Measure Tool up (so a cold GPU/DWM/overlay
    /// first frame on a slow CI leg doesn't lose the race), then retries the measurement gesture IN
    /// PLACE — keeping the one overlay/capture session alive so the warm-up accumulates. Closing and
    /// reopening the ruler between tries would restart <c>MeasureToolUI</c> and reset the cold-start on
    /// every attempt, fighting the warm-up. The per-tool gesture and clipboard validation are supplied
    /// by the caller; activation, warm-up, the retry loop, the asserts and teardown are common. The
    /// spacing tools rely on screen-capture edge detection (the click only copies when a captured frame
    /// has set <c>measuredEdges</c>); Bounds is a free-form drag that only needs the overlay tracking —
    /// both benefit from the same warm-up + retry. Three attempts rides out a cold-start first frame
    /// without masking a genuine failure.
    /// </summary>
    /// <param name="toolButtonId">Toolbar button automation id that selects the tool.</param>
    /// <param name="testName">Human label used in messages (and the spacing validator).</param>
    /// <param name="measure">Performs the measurement gesture for the given 0-based attempt index.</param>
    /// <param name="isValid">Validates the clipboard holds the expected measurement for this tool.</param>
    private static void RunMeasurementTest(
        UITestBase testBase,
        string toolButtonId,
        string testName,
        Action<int> measure,
        Func<string, bool> isValid)
    {
        const int attempts = 3;

        var activationKeys = ReadActivationShortcut(testBase);
        var ruler = ActivateScreenRuler(testBase, activationKeys, testName);
        SelectTool(ruler, toolButtonId, testName);
        WarmUpMeasureTool();

        string clipboardText = string.Empty;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            // The ruler only vanishes if a stray click dismissed it; re-arm defensively. The common
            // path keeps the ONE overlay/capture session alive so the warm-up accumulates across tries.
            if (!IsScreenRulerUIOpen(testBase))
            {
                ruler = ActivateScreenRuler(testBase, activationKeys, testName);
                SelectTool(ruler, toolButtonId, testName);
                WarmUpMeasureTool();
            }

            ClearClipboard();
            measure(attempt - 1);

            clipboardText = GetClipboardText();
            testBase.TestContext.WriteLine(
                $"{testName}: attempt {attempt}/{attempts} clipboard='{clipboardText}' (len={clipboardText.Length}).");
            if (!string.IsNullOrEmpty(clipboardText) && isValid(clipboardText))
            {
                break;
            }

            // Stay in the same session and let the overlay/capture settle before retrying (growing back-off).
            Thread.Sleep(300 + (attempt * 250));
        }

        Assert.IsFalse(
            string.IsNullOrEmpty(clipboardText),
            $"{testName}: clipboard was COMPLETELY EMPTY after {attempts} attempts. The Measure Tool produced no " +
            "measurement even after warming up — its overlay/screen-capture never became ready in this session " +
            "(typical of a cold GPU/DWM/WGC start on a slow CI leg).");
        Assert.IsTrue(
            isValid(clipboardText),
            $"{testName}: clipboard should contain a valid {testName} measurement, but contained: '{clipboardText}'");

        CloseScreenRulerUI(testBase);
        Assert.IsTrue(
            WaitForScreenRulerUIToDisappear(testBase, 2000),
            $"{testName}: ScreenRulerUI should close after calling CloseScreenRulerUI");
    }

    /// <summary>Select a tool on the Measure Tool toolbar by its automation id.</summary>
    private static void SelectTool(Session ruler, string buttonId, string testName)
    {
        var toolButton = ruler.Find<Element>(By.AccessibilityId(buttonId), 15000);
        Assert.IsNotNull(toolButton, $"{testName} button should be found");
        toolButton.Click(msPostAction: 500);
    }

    /// <summary>
    /// Warm the Measure Tool up before the first measuring gesture: move the cursor around the
    /// primary-monitor centre WITHOUT committing, so the overlay starts tracking and the WGC frame pool
    /// (used by the spacing tools' edge detection) starts delivering frames. The first capture session
    /// in an OS session pays a GPU/DWM cold-start that the slower CI legs (Win10) can otherwise lose the
    /// race against — warming up here removes that race for both the Bounds and spacing gestures.
    /// </summary>
    private static void WarmUpMeasureTool()
    {
        var (cx, cy) = ScreenCenter();
        (int Dx, int Dy)[] path = { (-120, -80), (120, 80), (-80, 60), (0, 0) };
        foreach (var (dx, dy) in path)
        {
            MouseHelper.MoveTo(cx + dx, cy + dy);
            Thread.Sleep(300);
        }
    }

    /// <summary>Run a bounds-tool measurement (drag a 100x100 box) and validate the clipboard output.</summary>
    public static void PerformBoundsToolTest(UITestBase testBase) =>
        RunMeasurementTest(
            testBase,
            BoundsButtonId,
            "Bounds",
            PerformBoundsMeasurement,
            clip => clip.Contains("100 × 100") || clip.Contains("100 x 100"));

    /// <summary>Drag a 100x100 box (varied origin per attempt), then right-click to commit the measurement.</summary>
    private static void PerformBoundsMeasurement(int attemptIndex = 0)
    {
        var (cx, cy) = ScreenCenter();

        // Vary the box origin per attempt so a bad spot on one try is avoided on the next.
        (int Dx, int Dy)[] spots = { (0, 0), (-180, -120), (180, 120), (-180, 120) };
        var (dx, dy) = spots[attemptIndex % spots.Length];
        int startX = (cx + dx) - 50;
        int startY = (cy + dy) - 50;

        // Move to the start first so the overlay is tracking the cursor before the drag. The 99px
        // delta measures 100x100 inclusive once the host is per-monitor DPI aware (app.manifest).
        MouseHelper.MoveTo(startX, startY);
        Thread.Sleep(300);
        MouseHelper.Drag(startX, startY, startX + 99, startY + 99, steps: 16);
        Thread.Sleep(400);

        // Right-click to dismiss the selection (commits the measurement to the clipboard).
        MouseHelper.RightClick();
        Thread.Sleep(300);
    }

    /// <summary>Spacing gesture: move to a varied spot, left-click to capture, right-click to dismiss.</summary>
    private static void PerformSpacingMeasurement(int attemptIndex = 0)
    {
        var (cx, cy) = ScreenCenter();

        // Vary the spot per attempt so a no-edge / uncapturable area on one try is avoided on the next.
        (int Dx, int Dy)[] spots = { (0, 0), (-220, -140), (220, 140), (-220, 140) };
        var (dx, dy) = spots[attemptIndex % spots.Length];
        int tx = cx + dx;
        int ty = cy + dy;

        // Move in two steps so the Measure Tool registers cursor MOVEMENT (a single SetCursorPos can
        // land without a tracked move, leaving the measurement empty), then click to capture.
        MouseHelper.MoveTo(tx - 60, ty - 60);
        Thread.Sleep(200);
        MouseHelper.MoveTo(tx, ty);
        Thread.Sleep(400);
        MouseHelper.LeftClick();
        Thread.Sleep(500);
        MouseHelper.RightClick();
        Thread.Sleep(400);
    }

    /// <summary>
    /// Primary-monitor centre in PHYSICAL pixels. Correct only when the test host is per-monitor
    /// DPI aware (see the project's app.manifest); otherwise the size is virtualized by the display
    /// scale factor.
    /// </summary>
    private static (int X, int Y) ScreenCenter()
    {
        var size = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        return (size.Width / 2, size.Height / 2);
    }
}
