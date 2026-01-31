using System;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using Photon.VR;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;

public class ComputerTerminalUI : MonoBehaviourPunCallbacks
{
    [Header("Monitor Output")]
    public TMP_Text screenText;

    [Header("Selection Styling")]
    public bool useColorHighlight = true;
    public Color selectedColor = new Color(0.3f, 1f, 0.3f, 1f); // light green
    public Color normalColor = Color.white;

    [Header("Limits")]
    public int roomCodeMaxChars = 12;
    public int nameMaxChars = 12;

    [Header("Current Values")]
    [SerializeField] private string roomCode = "";
    [SerializeField] private Color playerColor = new Color(1f, 0.5f, 0f, 1f); // orange-ish


    private float nextStatusRefreshTime = 0f;
    private const float statusRefreshInterval = 0.5f;
    private enum MenuOption { RoomCode, Name, Color }
    private MenuOption selected = MenuOption.RoomCode;

    // We keep separate edit buffers for fields that type text.
    private string editRoomCode = "";
    private string playerName;
    private string editName = "";

    // Terminal status / errors
    private string nameStatusLine = "";
    private float nameStatusUntilTime = 0f;
    private const float nameStatusDuration = 3f;

    // Color edit mode uses a channel + stepper.
    private enum ColorChannel { R, G, B }
    private ColorChannel colorChannel = ColorChannel.R;
    private const float colorStep = 0.05f;

    private void Start()
    {
        // Initialize edit buffers to current values so bottom area shows editable version.
        editRoomCode = roomCode;
        playerName = PlayerPrefs.GetString("Username", "Player");
        editName = playerName;
        Render();
    }
 
    private string SanitizeDisplayName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Player";

        s = s.Trim();

        // Keep within your max
        if (s.Length > nameMaxChars)
            s = s.Substring(0, nameMaxChars);

        return s;
    }

    private string SanitizeRoomCode(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        s = s.Trim().ToUpperInvariant();

        // Optional: remove spaces (room codes are typically no-spaces)
        s = s.Replace(" ", "");

        if (s.Length > roomCodeMaxChars)
            s = s.Substring(0, roomCodeMaxChars);

        return s;
    }
    private void TryJoinRoom(string code)
    {
        code = SanitizeRoomCode(code);
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        if (RoomSwitchService.Instance == null)
        {
            Debug.LogError("[ComputerTerminal] RoomSwitchService missing.");
            return;
        }

        RoomSwitchService.Instance.JoinPrivateRoom(code);
    }

    private string GetRoomStatusLine()
    {
        if (!PhotonNetwork.IsConnected)
            return "Status: Offline";
        if (!PhotonNetwork.IsConnectedAndReady)
            return "Status: Connecting...";
        if (!PhotonNetwork.InRoom)
            return "Status: Connected (not in room)";
        return $"Room: {PhotonNetwork.CurrentRoom.Name}  ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})";
    }


    private void Update()
    {
        if (Time.time >= nextStatusRefreshTime)
        {
            nextStatusRefreshTime = Time.time + statusRefreshInterval;
            Render();
        }

        if (!string.IsNullOrEmpty(nameStatusLine) && Time.time > nameStatusUntilTime)
        {
            nameStatusLine = "";
            Render();
        }
    }

    private void SetNameStatus(string msg)
    {
        nameStatusLine = msg ?? "";
        nameStatusUntilTime = Time.time + nameStatusDuration;
        Render();
    }

    private void SaveNameToPlayFab(string newName, Action<string> onSuccess, Action<string> onFail)
    {
        var req = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = newName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(
            req,
            result =>
            {
                Debug.Log($"[ComputerTerminal] PlayFab display name saved: {result.DisplayName}");
                onSuccess?.Invoke(result.DisplayName);
            },
            error =>
            {
                // Turn PlayFab error into something player-friendly
                string userMsg = error.ErrorMessage;

                // Common cases you may see:
                // - "Name not available" (if unique required)
                // - "Invalid input" / profanity / too short / invalid chars (depends on title settings)
                // We'll fall back to the provided message but keep it readable.
                if (string.IsNullOrEmpty(userMsg))
                    userMsg = "Name rejected.";

                Debug.LogError($"[ComputerTerminal] Name save failed. HTTP={error.HttpCode} Code={error.Error} Msg={error.ErrorMessage}\n{error.GenerateErrorReport()}");
                onFail?.Invoke(userMsg);
            }
        );
    }


    private bool TryGetChar(KeyboardKey.Key key, out char c)
    {
        c = '\0';

        // Letters
        if (key >= KeyboardKey.Key.A && key <= KeyboardKey.Key.Z)
        {
            c = (char)('A' + (key - KeyboardKey.Key.A));
            return true;
        }

        // Numbers
        if (key >= KeyboardKey.Key.Num0 && key <= KeyboardKey.Key.Num9)
        {
            c = (char)('0' + (key - KeyboardKey.Key.Num0));
            return true;
        }

        switch (key)
        {
            case KeyboardKey.Key.Space:
                c = ' ';
                return true;
            case KeyboardKey.Key.Dash:
                c = '-';
                return true;
            case KeyboardKey.Key.Underscore:
                c = '_';
                return true;
            default:
                return false;
        }
    }


    public void OnKeyPressed(KeyboardKey.Key key)
    {
        // Navigation keys always work
        Debug.Log($"[ComputerTerminalUI] Received key: {key} (selected={selected})");
        if (key == KeyboardKey.Key.Up)
        {
            selected = Prev(selected);
            Render();
            return;
        }
        if (key == KeyboardKey.Key.Down)
        {
            selected = Next(selected);
            Render();
            return;
        }

        // Handle based on selected menu item
        switch (selected)
        {
            case MenuOption.RoomCode:
                HandleRoomCode(key);
                break;

            case MenuOption.Name:
                HandleName(key);
                break;

            case MenuOption.Color:
                HandleColor(key);
                break;
        }
    }

    private void HandleRoomCode(KeyboardKey.Key key)
    {
        if (key == KeyboardKey.Key.Backspace)
        {
            if (editRoomCode.Length > 0)
                editRoomCode = editRoomCode.Substring(0, editRoomCode.Length - 1);
            Render();
            return;
        }

        if (key == KeyboardKey.Key.Enter)
        {
            roomCode = SanitizeRoomCode(editRoomCode);

            Debug.Log($"[ComputerTerminal] Join room with code: {roomCode}");
            TryJoinRoom(roomCode);

            Render();
            return;
        }

        if (TryGetChar(key, out char c))
        {
            if (editRoomCode.Length >= roomCodeMaxChars) return;

            // Optional: restrict room codes to A-Z 0-9 - _
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')
            {
                // Usually room codes are uppercase; choose what you want:
                c = char.ToUpperInvariant(c);
                editRoomCode += c;
                Render();
            }
        }
    }

    private void HandleName(KeyboardKey.Key key)
    {
        if (key == KeyboardKey.Key.Backspace)
        {
            if (editName.Length > 0)
                editName = editName.Substring(0, editName.Length - 1);
            Render();
            return;
        }

        if (key == KeyboardKey.Key.Enter)
        {
            var desired = SanitizeDisplayName(editName);

            // Optional: early local check
            if (string.IsNullOrWhiteSpace(desired))
            {
                SetNameStatus("Name cannot be empty ❌");
                return;
            }

            SetNameStatus("Saving name...");

            SaveNameToPlayFab(
                desired,
                savedName =>
                {
                    PhotonVRManager.SetUsername(savedName);
                    playerName = savedName;
                    editName = savedName;

                    SetNameStatus("Saved ✅");
                },
                failMsg =>
                {
                    SetNameStatus($"Name not allowed ❌ ({failMsg})");

                    // Optionally snap edit buffer back to current saved name
                    editName = playerName;
                }
            );

            return;
        }


        if (TryGetChar(key, out char c))
        {
            if (editName.Length >= nameMaxChars) return;

            // Basic filter: allow letters, numbers, space, dash, underscore
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
            {
                editName += c;
                Render();
            }
        }
    }

    private void HandleColor(KeyboardKey.Key key)
    {
        // For color we use:
        // Left/Right = change channel (R/G/B)
        // Enter = apply
        // Backspace = reset to default orange (optional)
        // Characters ignored.

        if (key == KeyboardKey.Key.Left)
        {
            colorChannel = PrevChannel(colorChannel);
            Render();
            return;
        }
        if (key == KeyboardKey.Key.Right)
        {
            colorChannel = NextChannel(colorChannel);
            Render();
            return;
        }

        if (key == KeyboardKey.Key.Backspace)
        {
            playerColor = new Color(1f, 0.5f, 0f, 1f);
            Render();
            return;
        }

        if (key == KeyboardKey.Key.Enter)
        {
            Debug.Log($"[ComputerTerminal] Color applied: RGB({playerColor.r:F2}, {playerColor.g:F2}, {playerColor.b:F2})");

            // Hook: if you have a player avatar script, call it here.
            // Example:
            // GetComponent<PlayerAppearance>()?.SetColor(playerColor);

            Render();
            return;
        }

        // Use Up/Down to adjust the current channel too (you said up/down selects menu;
        // but we already consume Up/Down for menu navigation globally, so we need another method).
        //
        // We'll use Num1/Num2 for -/+ as a simple placeholder, OR you can add dedicated keys later.
        // If you want Up/Down to adjust color only while Color is selected, tell me and we’ll change it.
        if (key == KeyboardKey.Key.Num1) AdjustColor(-colorStep);
        if (key == KeyboardKey.Key.Num2) AdjustColor(+colorStep);
    }

    private void AdjustColor(float delta)
    {
        float r = playerColor.r;
        float g = playerColor.g;
        float b = playerColor.b;

        switch (colorChannel)
        {
            case ColorChannel.R: r = Mathf.Clamp01(r + delta); break;
            case ColorChannel.G: g = Mathf.Clamp01(g + delta); break;
            case ColorChannel.B: b = Mathf.Clamp01(b + delta); break;
        }

        playerColor = new Color(r, g, b, 1f);
        Render();
    }

    private void Render()
    {
        if (screenText == null) return;

        // Top half: menu lines always visible
        var sb = new StringBuilder();
        sb.AppendLine(GetRoomStatusLine());
        sb.AppendLine(MenuLine(MenuOption.RoomCode, "Room Code"));
        sb.AppendLine(MenuLine(MenuOption.Name, "Name"));
        sb.AppendLine(MenuLine(MenuOption.Color, "Color"));
        sb.AppendLine("------------------------------");
        sb.AppendLine();

        // Bottom half: dynamic per selection
        switch (selected)
        {
            case MenuOption.RoomCode:
                sb.AppendLine($"Enter code (max {roomCodeMaxChars}):");
                sb.AppendLine($"{editRoomCode}_");
                sb.AppendLine();
                sb.AppendLine("Enter = Join (log)");
                sb.AppendLine("Backspace = delete");
                break;

            case MenuOption.Name:
                if (string.IsNullOrEmpty(nameStatusLine))
                {
                    sb.AppendLine($"Current: {playerName}");
                    sb.AppendLine($"Edit: {editName}_");
                }
                if (!string.IsNullOrEmpty(nameStatusLine))
                {
                    sb.AppendLine();
                    sb.AppendLine(nameStatusLine);
                }
                sb.AppendLine();
                sb.AppendLine("Enter = Save");
                sb.AppendLine("Backspace = delete");
                break;

            case MenuOption.Color:
                sb.AppendLine("Left/Right = select channel");
                sb.AppendLine("Num1 = -    Num2 = +");
                sb.AppendLine("Enter = Apply (log)");
                sb.AppendLine("Backspace = reset");
                sb.AppendLine();
                sb.AppendLine(ColorLine());
                break;
        }

        // Optional: If useColorHighlight is on, we’ll use TMP rich text for the selected line color.
        // That’s already baked into MenuLine via <color=#...> tags.
        screenText.text = sb.ToString();
    }

    private string MenuLine(MenuOption option, string label)
    {
        bool isSelected = (option == selected);
        string text = isSelected ? $"[ {label} ]" : $"  {label}  ";

        if (!useColorHighlight || !isSelected) return text;

        string hex = ColorUtility.ToHtmlStringRGBA(selectedColor);
        return $"<color=#{hex}>{text}</color>";
    }

    private string ColorLine()
    {
        // show which channel is selected
        string r = (colorChannel == ColorChannel.R) ? $"[R:{playerColor.r:F2}]" : $" R:{playerColor.r:F2} ";
        string g = (colorChannel == ColorChannel.G) ? $"[G:{playerColor.g:F2}]" : $" G:{playerColor.g:F2} ";
        string b = (colorChannel == ColorChannel.B) ? $"[B:{playerColor.b:F2}]" : $" B:{playerColor.b:F2} ";
        return $"{r}   {g}   {b}";
    }

    private static MenuOption Next(MenuOption o) => o switch
    {
        MenuOption.RoomCode => MenuOption.Name,
        MenuOption.Name => MenuOption.Color,
        MenuOption.Color => MenuOption.RoomCode,
        _ => MenuOption.RoomCode
    };

    private static MenuOption Prev(MenuOption o) => o switch
    {
        MenuOption.RoomCode => MenuOption.Color,
        MenuOption.Name => MenuOption.RoomCode,
        MenuOption.Color => MenuOption.Name,
        _ => MenuOption.RoomCode
    };

    private static ColorChannel NextChannel(ColorChannel c) => c switch
    {
        ColorChannel.R => ColorChannel.G,
        ColorChannel.G => ColorChannel.B,
        ColorChannel.B => ColorChannel.R,
        _ => ColorChannel.R
    };

    private static ColorChannel PrevChannel(ColorChannel c) => c switch
    {
        ColorChannel.R => ColorChannel.B,
        ColorChannel.G => ColorChannel.R,
        ColorChannel.B => ColorChannel.G,
        _ => ColorChannel.R
    };
}
