using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One place for player input: keyboard plus any standard gamepad (Xbox, PlayStation and similar).
/// Stick pushes count once per push, like a key press.
/// </summary>
public static class GameInput
{
    private const float PushThreshold = 0.6f;
    private const float ReleaseThreshold = 0.3f;

    private static int s_frame = -1;
    private static int s_stickX, s_stickY;
    private static bool s_left, s_right, s_up, s_down;

    /// <summary>True if the most recent input came from a gamepad (used for on-screen prompts).</summary>
    public static bool UsingGamepad { get; private set; }

    private static void Refresh()
    {
        if (s_frame == Time.frameCount) return;
        s_frame = Time.frameCount;
        s_left = s_right = s_up = s_down = false;

        if (Input.anyKeyDown) UsingGamepad = false;

        var pad = Gamepad.current;
        if (pad == null)
        {
            UsingGamepad = false;
            s_stickX = s_stickY = 0;
            return;
        }

        Vector2 stick = pad.leftStick.ReadValue();
        ReadAxis(stick.x, ref s_stickX, ref s_right, ref s_left);
        ReadAxis(stick.y, ref s_stickY, ref s_up, ref s_down);

        if (pad.dpad.left.wasPressedThisFrame) s_left = true;
        if (pad.dpad.right.wasPressedThisFrame) s_right = true;
        if (pad.dpad.up.wasPressedThisFrame) s_up = true;
        if (pad.dpad.down.wasPressedThisFrame) s_down = true;

        if (s_left || s_right || s_up || s_down || AnyPadButton(pad)) UsingGamepad = true;
    }

    private static void ReadAxis(float value, ref int state, ref bool positive, ref bool negative)
    {
        int next = state;
        if (value >= PushThreshold) next = 1;
        else if (value <= -PushThreshold) next = -1;
        else if (Mathf.Abs(value) < ReleaseThreshold) next = 0;

        if (next != state)
        {
            if (next > 0) positive = true;
            else if (next < 0) negative = true;
        }
        state = next;
    }

    private static bool AnyPadButton(Gamepad pad) =>
        pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
        pad.buttonNorth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
        pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame ||
        pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame;

    public static bool MoveLeft
    {
        get { Refresh(); return s_left || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow); }
    }

    public static bool MoveRight
    {
        get { Refresh(); return s_right || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow); }
    }

    public static bool Jump
    {
        get
        {
            Refresh();
            var pad = Gamepad.current;
            return s_up || (pad != null && pad.buttonSouth.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        }
    }

    public static bool Dodge
    {
        get
        {
            Refresh();
            var pad = Gamepad.current;
            return s_down || (pad != null && pad.buttonEast.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        }
    }

    public static bool DistractPad
    {
        get
        {
            Refresh();
            var pad = Gamepad.current;
            return pad != null && (pad.buttonWest.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame);
        }
    }

    public static bool PausePad
    {
        get { Refresh(); var pad = Gamepad.current; return pad != null && pad.startButton.wasPressedThisFrame; }
    }

    public static bool BackPad
    {
        get { Refresh(); var pad = Gamepad.current; return pad != null && pad.buttonEast.wasPressedThisFrame; }
    }

    public static bool AnyPadPressed
    {
        get { Refresh(); var pad = Gamepad.current; return pad != null && AnyPadButton(pad); }
    }

    /// <summary>Radio reply on the gamepad: 0, 1 or 2, or -1 for none.</summary>
    public static int ReplyPad
    {
        get
        {
            Refresh();
            var pad = Gamepad.current;
            if (pad == null) return -1;
            if (pad.leftShoulder.wasPressedThisFrame) return 0;
            if (pad.buttonNorth.wasPressedThisFrame) return 1;
            if (pad.rightShoulder.wasPressedThisFrame) return 2;
            return -1;
        }
    }
}

/// <summary>Keeps a menu item selected for gamepad players, even after the mouse clicks empty space or a screen changes.</summary>
public class GamepadMenuFocus : MonoBehaviour
{
    private void Update()
    {
        var events = EventSystem.current;
        if (events == null || Gamepad.current == null || !GameInput.UsingGamepad) return;

        var current = events.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        Selectable best = null;
        foreach (var s in Selectable.allSelectablesArray)
        {
            if (s == null || !s.IsInteractable() || !s.isActiveAndEnabled) continue;
            if (best == null || s.transform.position.y > best.transform.position.y) best = s;
        }
        if (best != null) best.Select();
    }
}
