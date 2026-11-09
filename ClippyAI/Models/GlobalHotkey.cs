using Avalonia.Input;
using EvDevSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClippyAI.Models;

/// <summary>
/// Platform-independent representation of a configurable global hotkey
/// ("Ctrl+Alt+C"), shared by the Windows (RegisterHotKey) and the Linux
/// (evdev) implementations and by the settings UI. The canonical string form
/// is also the configuration format stored in the Configuration table
/// (keys HotkeyAskClippy, HotkeyPreviousTask, HotkeyNextTask; empty = disabled).
/// </summary>
public sealed class GlobalHotkey
{
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool Win { get; init; }

    /// <summary>Canonical (upper-case) key name, e.g. "C", "Up", "F5", "Space".</summary>
    public string KeyName { get; init; } = "";

    /// <summary>Win32 modifier flags (without MOD_NOREPEAT).</summary>
    public int Win32Modifiers =>
        (Alt ? 0x0001 : 0) | (Ctrl ? 0x0002 : 0) | (Shift ? 0x0004 : 0) | (Win ? 0x0008 : 0);

    /// <summary>Win32 virtual key code, or 0 when the key has no VK mapping.</summary>
    public int VirtualKey => VkFromKeyName(KeyName);

    /// <summary>evdev key codes satisfying this hotkey's key part, or null when unmapped.</summary>
    public EvDevKeyCode[]? EvDevKeyCodes => KeyToEvDev.TryGetValue(KeyName, out EvDevKeyCode[]? codes) ? codes : null;

    /// <summary>All part names ("Ctrl", "Alt", "Shift", "Win", key) an evdev tracker must see pressed together.</summary>
    public IEnumerable<string> PartNames
    {
        get
        {
            if (Ctrl) yield return "Ctrl";
            if (Alt) yield return "Alt";
            if (Shift) yield return "Shift";
            if (Win) yield return "Win";
            yield return KeyName;
        }
    }

    public override string ToString() => Format(Ctrl, Alt, Shift, Win, KeyName);

    public static string Format(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        List<string> parts = [];
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(key);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Parses "Ctrl+Alt+C" (modifier order irrelevant, aliases accepted).
    /// Requires at least one modifier so plain keys can never be registered
    /// as global hotkeys and would swallow normal typing.
    /// Returns null for empty/invalid input; empty text means "disabled".
    /// </summary>
    public static GlobalHotkey? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        bool ctrl = false, alt = false, shift = false, win = false;
        string? key = null;

        foreach (string rawToken in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string token = rawToken.ToLowerInvariant();
            switch (token)
            {
                case "ctrl":
                case "control":
                    ctrl = true;
                    continue;
                case "alt":
                case "menu":
                    alt = true;
                    continue;
                case "shift":
                    shift = true;
                    continue;
                case "win":
                case "windows":
                case "meta":
                case "super":
                    win = true;
                    continue;
            }

            if (key != null)
            {
                return null; // more than one key part
            }

            key = NormalizeKeyName(rawToken);
            if (key == null)
            {
                return null; // unknown key token
            }
        }

        if (key == null || (!ctrl && !alt && !shift && !win))
        {
            return null; // needs at least one modifier
        }

        return new GlobalHotkey { Ctrl = ctrl, Alt = alt, Shift = shift, Win = win, KeyName = key };
    }

    /// <summary>Maps an Avalonia physical key press to a canonical key name, or null.</summary>
    public static string? NameFromAvaloniaKey(Key key)
    {
        int v = (int)key;

        if (v >= 0x41 && v <= 0x5A) // A-Z (Avalonia Key values match Win32 VK codes)
        {
            return ((char)v).ToString();
        }

        if (v >= 0x30 && v <= 0x39) // D0-D9
        {
            return ((char)v).ToString();
        }

        if (v >= 0x70 && v <= 0x87) // F1-F24
        {
            return "F" + (v - 0x6F);
        }

        return key switch
        {
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Escape => "Escape",
            Key.Back => "Backspace",
            Key.OemMinus => "Minus",
            Key.OemPlus => "Equals",
            Key.OemOpenBrackets => "LeftBracket",
            Key.OemCloseBrackets => "RightBracket",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemBackslash => "Backslash",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Slash",
            _ => null
        };
    }

    /// <summary>
    /// Maps an evdev key code to a tracked part name ("Ctrl", "Alt", "Shift",
    /// "Win" for either hand, canonical key name otherwise), or null when the
    /// code is irrelevant for all hotkeys.
    /// </summary>
    public static string? EvDevPartName(EvDevKeyCode code)
    {
        return EvDevToPartName.TryGetValue(code, out string? name) ? name : null;
    }

    private static string? NormalizeKeyName(string token)
    {
        string t = token.Trim();
        string upper = t.ToUpperInvariant();

        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z')
        {
            return upper;
        }

        if (upper.Length == 1 && char.IsAsciiDigit(upper[0]))
        {
            return upper;
        }

        if (t.Length == 1)
        {
            return PunctuationAliases.TryGetValue(t, out string? punctCanonical) ? punctCanonical : null;
        }

        if (upper.StartsWith('F') && int.TryParse(upper.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
        {
            return "F" + fn;
        }

        return SpecialKeyAliases.TryGetValue(upper, out string? specialCanonical) ? specialCanonical : null;
    }

    private static readonly Dictionary<string, string> SpecialKeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UP"] = "Up", ["DOWN"] = "Down", ["LEFT"] = "Left", ["RIGHT"] = "Right",
        ["HOME"] = "Home", ["END"] = "End",
        ["PAGEUP"] = "PageUp", ["PRIOR"] = "PageUp", ["PGUP"] = "PageUp",
        ["PAGEDOWN"] = "PageDown", ["NEXT"] = "PageDown", ["PGDN"] = "PageDown",
        ["INSERT"] = "Insert", ["INS"] = "Insert",
        ["DELETE"] = "Delete", ["DEL"] = "Delete",
        ["SPACE"] = "Space",
        ["ENTER"] = "Enter", ["RETURN"] = "Enter",
        ["TAB"] = "Tab",
        ["ESCAPE"] = "Escape", ["ESC"] = "Escape",
        ["BACKSPACE"] = "Backspace", ["BACK"] = "Backspace",
        ["MINUS"] = "Minus",
        ["EQUALS"] = "Equals", ["EQUAL"] = "Equals",
        ["LEFTBRACKET"] = "LeftBracket", ["OPENBRACKET"] = "LeftBracket", ["BRACKETLEFT"] = "LeftBracket",
        ["RIGHTBRACKET"] = "RightBracket", ["CLOSEDBRACKET"] = "RightBracket", ["BRACKETRIGHT"] = "RightBracket",
        ["SEMICOLON"] = "Semicolon",
        ["QUOTE"] = "Quote", ["APOSTROPHE"] = "Quote",
        ["BACKSLASH"] = "Backslash",
        ["COMMA"] = "Comma",
        ["PERIOD"] = "Period", ["DOT"] = "Period",
        ["SLASH"] = "Slash"
    };

    private static readonly Dictionary<string, string> PunctuationAliases = new(StringComparer.Ordinal)
    {
        ["-"] = "Minus", ["="] = "Equals",
        ["["] = "LeftBracket", ["]"] = "RightBracket",
        [";"] = "Semicolon", ["'"] = "Quote", ["\\"] = "Backslash",
        [","] = "Comma", ["."] = "Period", ["/"] = "Slash"
    };

    private static readonly Dictionary<string, int> KeyToVk = new(StringComparer.Ordinal)
    {
        ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Escape"] = 0x1B, ["Backspace"] = 0x08,
        ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E,
        ["Minus"] = 0xBD, ["Equals"] = 0xBB,
        ["LeftBracket"] = 0xDB, ["RightBracket"] = 0xDD,
        ["Semicolon"] = 0xBA, ["Quote"] = 0xDE, ["Backslash"] = 0xDC,
        ["Comma"] = 0xBC, ["Period"] = 0xBE, ["Slash"] = 0xBF
    };

    /// <summary>Virtual-key code for a canonical key name (letters/digits/F-keys computed, rest tabled).</summary>
    public static int VkFromKeyName(string key)
    {
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
        {
            return key[0];
        }

        if (key.Length == 1 && char.IsAsciiDigit(key[0]))
        {
            return key[0];
        }

        if (key.Length > 1 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
        {
            return 0x6F + fn;
        }

        return KeyToVk.TryGetValue(key, out int vk) ? vk : 0;
    }

    private static readonly Dictionary<string, EvDevKeyCode[]> KeyToEvDev = new(StringComparer.Ordinal)
    {
        ["Q"] = [EvDevKeyCode.KEY_Q], ["W"] = [EvDevKeyCode.KEY_W], ["E"] = [EvDevKeyCode.KEY_E],
        ["R"] = [EvDevKeyCode.KEY_R], ["T"] = [EvDevKeyCode.KEY_T], ["Y"] = [EvDevKeyCode.KEY_Y],
        ["U"] = [EvDevKeyCode.KEY_U], ["I"] = [EvDevKeyCode.KEY_I], ["O"] = [EvDevKeyCode.KEY_O],
        ["P"] = [EvDevKeyCode.KEY_P], ["A"] = [EvDevKeyCode.KEY_A], ["S"] = [EvDevKeyCode.KEY_S],
        ["D"] = [EvDevKeyCode.KEY_D], ["F"] = [EvDevKeyCode.KEY_F], ["G"] = [EvDevKeyCode.KEY_G],
        ["H"] = [EvDevKeyCode.KEY_H], ["J"] = [EvDevKeyCode.KEY_J], ["K"] = [EvDevKeyCode.KEY_K],
        ["L"] = [EvDevKeyCode.KEY_L], ["Z"] = [EvDevKeyCode.KEY_Z], ["X"] = [EvDevKeyCode.KEY_X],
        ["C"] = [EvDevKeyCode.KEY_C], ["V"] = [EvDevKeyCode.KEY_V], ["B"] = [EvDevKeyCode.KEY_B],
        ["N"] = [EvDevKeyCode.KEY_N], ["M"] = [EvDevKeyCode.KEY_M],
        ["1"] = [EvDevKeyCode.KEY_1], ["2"] = [EvDevKeyCode.KEY_2], ["3"] = [EvDevKeyCode.KEY_3],
        ["4"] = [EvDevKeyCode.KEY_4], ["5"] = [EvDevKeyCode.KEY_5], ["6"] = [EvDevKeyCode.KEY_6],
        ["7"] = [EvDevKeyCode.KEY_7], ["8"] = [EvDevKeyCode.KEY_8], ["9"] = [EvDevKeyCode.KEY_9],
        ["0"] = [EvDevKeyCode.KEY_0],
        ["Space"] = [EvDevKeyCode.KEY_SPACE], ["Enter"] = [EvDevKeyCode.KEY_ENTER],
        ["Tab"] = [EvDevKeyCode.KEY_TAB], ["Escape"] = [EvDevKeyCode.KEY_ESC],
        ["Backspace"] = [EvDevKeyCode.KEY_BACKSPACE],
        ["Up"] = [EvDevKeyCode.KEY_UP], ["Down"] = [EvDevKeyCode.KEY_DOWN],
        ["Left"] = [EvDevKeyCode.KEY_LEFT], ["Right"] = [EvDevKeyCode.KEY_RIGHT],
        ["Home"] = [EvDevKeyCode.KEY_HOME], ["End"] = [EvDevKeyCode.KEY_END],
        ["PageUp"] = [EvDevKeyCode.KEY_PAGEUP], ["PageDown"] = [EvDevKeyCode.KEY_PAGEDOWN],
        ["Insert"] = [EvDevKeyCode.KEY_INSERT], ["Delete"] = [EvDevKeyCode.KEY_DELETE],
        ["Minus"] = [EvDevKeyCode.KEY_MINUS], ["Equals"] = [EvDevKeyCode.KEY_EQUAL],
        ["LeftBracket"] = [EvDevKeyCode.KEY_LEFTBRACE], ["RightBracket"] = [EvDevKeyCode.KEY_RIGHTBRACE],
        ["Semicolon"] = [EvDevKeyCode.KEY_SEMICOLON], ["Quote"] = [EvDevKeyCode.KEY_APOSTROPHE],
        ["Backslash"] = [EvDevKeyCode.KEY_BACKSLASH], ["Comma"] = [EvDevKeyCode.KEY_COMMA],
        ["Period"] = [EvDevKeyCode.KEY_DOT], ["Slash"] = [EvDevKeyCode.KEY_SLASH],
        ["F1"] = [EvDevKeyCode.KEY_F1], ["F2"] = [EvDevKeyCode.KEY_F2], ["F3"] = [EvDevKeyCode.KEY_F3],
        ["F4"] = [EvDevKeyCode.KEY_F4], ["F5"] = [EvDevKeyCode.KEY_F5], ["F6"] = [EvDevKeyCode.KEY_F6],
        ["F7"] = [EvDevKeyCode.KEY_F7], ["F8"] = [EvDevKeyCode.KEY_F8], ["F9"] = [EvDevKeyCode.KEY_F9],
        ["F10"] = [EvDevKeyCode.KEY_F10], ["F11"] = [EvDevKeyCode.KEY_F11], ["F12"] = [EvDevKeyCode.KEY_F12],
        ["F13"] = [EvDevKeyCode.KEY_F13], ["F14"] = [EvDevKeyCode.KEY_F14], ["F15"] = [EvDevKeyCode.KEY_F15],
        ["F16"] = [EvDevKeyCode.KEY_F16], ["F17"] = [EvDevKeyCode.KEY_F17], ["F18"] = [EvDevKeyCode.KEY_F18],
        ["F19"] = [EvDevKeyCode.KEY_F19], ["F20"] = [EvDevKeyCode.KEY_F20], ["F21"] = [EvDevKeyCode.KEY_F21],
        ["F22"] = [EvDevKeyCode.KEY_F22], ["F23"] = [EvDevKeyCode.KEY_F23], ["F24"] = [EvDevKeyCode.KEY_F24]
    };

    /// <summary>Reverse lookup for the evdev event handler; covers both hands for modifiers.</summary>
    private static readonly Dictionary<EvDevKeyCode, string> EvDevToPartName = BuildEvDevReverseMap();

    private static Dictionary<EvDevKeyCode, string> BuildEvDevReverseMap()
    {
        Dictionary<EvDevKeyCode, string> map = new()
        {
            [EvDevKeyCode.KEY_LEFTCTRL] = "Ctrl",
            [EvDevKeyCode.KEY_RIGHTCTRL] = "Ctrl",
            [EvDevKeyCode.KEY_LEFTALT] = "Alt",
            [EvDevKeyCode.KEY_RIGHTALT] = "Alt",
            [EvDevKeyCode.KEY_LEFTSHIFT] = "Shift",
            [EvDevKeyCode.KEY_RIGHTSHIFT] = "Shift",
            [EvDevKeyCode.KEY_LEFTMETA] = "Win",
            [EvDevKeyCode.KEY_RIGHTMETA] = "Win"
        };

        foreach (KeyValuePair<string, EvDevKeyCode[]> entry in KeyToEvDev)
        {
            foreach (EvDevKeyCode code in entry.Value)
            {
                map[code] = entry.Key;
            }
        }

        return map;
    }
}
